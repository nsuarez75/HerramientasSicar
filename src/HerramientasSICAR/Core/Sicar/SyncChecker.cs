#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Pasos con posible doble sincronización, según el manual SICAR (3.4.1):
// - 2.9: al sincronizar se prueban todos los pasos que no se salta un salto; un paso es ejecutable con
//   ilockAuto = 1, ilockExtSync = 1 y transAuto = 0. Si encaja más de uno, la sincronización no es única.
// - 2.2.5: las condiciones de PERM se suman con Y a las del paso; en PERM lo no programado vale 1.
// - 2.3: en un paso, ilockAuto y transAuto sin programar valen 0 (un paso manual nunca es ejecutable) e
//   ilockExtSync vale 1. ilockNoSync e ilockExtInt no participan.
// - 2.9: durante la sincronización #Sequence.pos_Pulse_OM_Sync vale 1 (así se bloquean los saltos).
// - Un par en el que un paso salta al otro (paso de espera → paso de decisión) no es doble sincronización.
// Para cada par de pasos se busca un estado de señales que los haga ejecutables a la vez y sin que ningún
// salto anterior se los salte. Además se excluye lo físicamente imposible (sensores -H/-W de un actuador a la
// vez, -ReleasePick y -ReleaseDrop de una misma estación, una señal con dos valores) y, en secuencias de
// robot, los pasos de servicio S223-S227.
using System.Text.RegularExpressions;
using HerramientasSICAR.Core.Lad;
using HerramientasSICAR.Core.Logic;

namespace HerramientasSICAR.Core.Sicar;

// Condiciones que el usuario ha decidido que nunca se dan a la vez, de dos en dos: cada señal con su valor
// («A = 1 · B = 0»: si A está a 1, B no está a 0). Sin valores, todas a 1: señales que nunca están a 1 a la vez.
public sealed record ExclusiveGroup(IReadOnlyList<string> Signals)
{
    // Cuándo y para qué par se decidió, solo para mostrarlo en la lista de decisiones.
    public string? Note { get; init; }

    // Valor de cada señal, en el mismo orden; null si todas valen 1.
    public IReadOnlyList<bool>? Values { get; init; }

    public bool Value(int index) => Values == null || index >= Values.Count || Values[index];

    private IEnumerable<string> Conditions() =>
        Signals.Select((s, i) => s + "=" + (Value(i) ? "1" : "0")).Order(StringComparer.Ordinal);

    public bool Equals(ExclusiveGroup? other) => other is not null && Conditions().SequenceEqual(other.Conditions());

    public override int GetHashCode() => Signals.Count;
}

// Dos pasos que pueden sincronizar a la vez, con un estado de señales que lo provoca.
public sealed record SyncConflict(SequenceStep StepA, SequenceStep StepB, IReadOnlyDictionary<string, bool> State);

// Una señal que un paso exige siempre con un valor y con la que el otro puede sincronizar al valor contrario.
// Es un hecho de los dos interlocks, no una propuesta: solo el proceso dice si separa los pasos.
public sealed record SyncDifference(SequenceStep Requiring, SequenceStep Other, string Signal, bool Value, bool OtherChecksIt);

public sealed partial class SyncChecker
{
    public const int FirstRobotServiceStep = 223;
    public const int LastRobotServiceStep = 227;

    public bool ExcludeHomeWork { get; init; } = true;

    // «…-ReleasePick» y «…-ReleaseDrop» de una misma estación nunca están a 1 a la vez.
    public bool ExcludeReleasePickDrop { get; init; } = true;

    public bool ExcludeDecodedValues { get; init; } = true;

    // null: se deduce (una secuencia con pasos S223-S227 es de robot).
    public bool? IsRobot { get; init; }

    // Señales que el programa calcula con una bobina (SignalDefinitions): se añade «señal ⇔ condiciones de
    // activación» para razonar sobre las señales de verdad (manual SICAR, 4.1) sin perder su nombre, que hace
    // falta para las reglas físicas (-H/-W).
    public IReadOnlyDictionary<string, LadExpression>? Definitions { get; init; }

    // Niveles de señales calculadas que se siguen hacia atrás.
    public int DefinitionDepth { get; init; } = 10;

    // Grupos que el usuario ha marcado como excluyentes (solo una señal a 1 a la vez).
    public IReadOnlyList<ExclusiveGroup> ExclusiveGroups { get; init; } = [];

    // Señales entre las que el usuario puede elegir un grupo excluyente para un par: las que un paso exige
    // a 1 y el otro no (si fueran excluyentes, los dos pasos no podrían sincronizar a la vez). Un grupo necesita
    // al menos dos señales: con una sola no hay nada que proponer.
    public IReadOnlyList<string> ExclusionCandidates(SyncConflict conflict, SequenceStep? permanent = null)
    {
        var signals = Differences(conflict, permanent)
            .Where(d => d.Value)
            .Select(d => d.Signal)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
        return signals.Count >= 2 ? signals : [];
    }

    public IReadOnlyList<SequenceStep> Candidates(SequenceBlock sequence)
    {
        var robot = IsRobot ?? sequence.Steps.Any(IsRobotServiceStep);
        return sequence.Steps.Where(s => !(robot && IsRobotServiceStep(s))).ToList();
    }

    public IReadOnlyList<SyncConflict> Check(SequenceBlock sequence)
    {
        var steps = Candidates(sequence);
        var conditions = steps.ToDictionary(s => s.Number,
            s => BoolFormula.And(SyncCondition(s, sequence.Permanent), FixConstants(BoolFormula.Not(Skipped(sequence, s)))));
        var conflicts = new List<SyncConflict>();
        for (var i = 0; i < steps.Count; i++)
        {
            if (!IsPossible(conditions[steps[i].Number]))
            {
                continue;
            }
            for (var j = i + 1; j < steps.Count; j++)
            {
                if (JumpsBetween(steps[i], steps[j]))
                {
                    continue;
                }
                var both = BoolFormula.And(conditions[steps[i].Number], conditions[steps[j].Number]);
                var state = WithConstraints(both).Solve();
                if (state != null)
                {
                    conflicts.Add(new SyncConflict(steps[i], steps[j], state));
                }
            }
        }
        return conflicts;
    }

    // Paso ejecutable al sincronizar: ilockAuto y ilockExtSync de PERM y del paso a 1 y transAuto del paso a 0,
    // con los valores por defecto del manual para lo no programado.
    public BoolFormula SyncCondition(SequenceStep step, SequenceStep? permanent = null) =>
        FixConstants(BoolFormula.And(
            permanent == null ? BoolFormula.True : Section(permanent, SectionKind.IlockAuto, true),
            permanent == null ? BoolFormula.True : Section(permanent, SectionKind.IlockExtSync, true),
            Section(step, SectionKind.IlockAuto, false),
            Section(step, SectionKind.IlockExtSync, true),
            BoolFormula.Not(Section(step, SectionKind.TransAuto, false))));

    // El paso no se prueba si un salto de un paso anterior lleva más allá de él en el ciclo de sincronización.
    public BoolFormula Skipped(SequenceBlock sequence, SequenceStep step)
    {
        var order = sequence.Steps.Select((s, i) => (s.Number, i)).ToDictionary(x => x.Number, x => x.i);
        var position = order[step.Number];
        return BoolFormula.Or(sequence.Steps.Take(position)
            .SelectMany(previous => previous.Jumps)
            .Where(jump => order.TryGetValue(jump.Target, out var target) && target > position)
            .Select(jump => BoolFormula.From(jump.Condition)));
    }

    public IReadOnlyList<SyncDifference> Differences(SyncConflict conflict, SequenceStep? permanent = null)
    {
        var a = SyncCondition(conflict.StepA, permanent);
        var b = SyncCondition(conflict.StepB, permanent);
        var ownA = OwnSignals(conflict.StepA);
        var ownB = OwnSignals(conflict.StepB);
        return [.. DifferencesOf(conflict.StepA, a, ownA, conflict.StepB, b, ownB),
            .. DifferencesOf(conflict.StepB, b, ownB, conflict.StepA, a, ownA)];
    }

    private IEnumerable<SyncDifference> DifferencesOf(SequenceStep step, BoolFormula condition, ISet<string> own,
        SequenceStep other, BoolFormula otherCondition, ISet<string> otherOwn)
    {
        foreach (var signal in own.OrderBy(s => s, StringComparer.Ordinal))
        {
            foreach (var value in new[] { true, false })
            {
                var requires = !IsPossible(BoolFormula.And(condition, Literal(signal, !value)));
                if (requires && IsPossible(BoolFormula.And(otherCondition, Literal(signal, !value))))
                {
                    yield return new SyncDifference(step, other, signal, value, otherOwn.Contains(signal));
                }
            }
        }
    }

    // Valor de una señal que no es libre durante la sincronización; null si es libre.
    public static bool? FixedValue(string variable)
    {
        var name = variable.Trim('"', '#');
        if (name.Equals("alwaysTrue", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (name.Equals("alwaysFalse", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return name.Equals("Sequence.pos_Pulse_OM_Sync", StringComparison.OrdinalIgnoreCase) ? true : null;
    }

    // Un paso de espera comparte interlocks con el paso de decisión al que salta: si se sincroniza en el de
    // espera, su salto lleva al paso que toca. Por eso un par en el que uno salta al otro no es doble sincronización.
    public static bool JumpsBetween(SequenceStep a, SequenceStep b) =>
        a.Jumps.Any(j => j.Target == b.Number) || b.Jumps.Any(j => j.Target == a.Number);

    private static bool IsRobotServiceStep(SequenceStep step) => step.Number is >= FirstRobotServiceStep and <= LastRobotServiceStep;

    private BoolFormula Section(SequenceStep step, SectionKind kind, bool whenMissing) =>
        step.Sections.TryGetValue(kind, out var logic)
            ? BoolFormula.From(logic)
            : whenMissing ? BoolFormula.True : BoolFormula.False;

    private bool IsPossible(BoolFormula formula) => WithConstraints(FixConstants(formula)).IsSatisfiable;

    private static BoolFormula Literal(string signal, bool value) =>
        value ? BoolFormula.Var(signal) : BoolFormula.Not(BoolFormula.Var(signal));

    private ISet<string> OwnSignals(SequenceStep step)
    {
        var signals = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kind in new[] { SectionKind.IlockAuto, SectionKind.IlockExtSync, SectionKind.TransAuto })
        {
            if (step.Sections.TryGetValue(kind, out var logic))
            {
                BoolFormula.From(logic).CollectVariables(signals);
            }
        }
        signals.RemoveWhere(s => FixedValue(s).HasValue);
        return signals;
    }

    private static BoolFormula FixConstants(BoolFormula formula)
    {
        foreach (var variable in formula.Variables())
        {
            if (FixedValue(variable) is { } value)
            {
                formula = formula.Assign(variable, value);
            }
        }
        return formula;
    }

    // Lo que el programa y la física imponen: cada señal calculada vale sus condiciones de activación, un
    // actuador no está en home y en work a la vez y una señal no tiene dos valores.
    private BoolFormula WithConstraints(BoolFormula formula)
    {
        var constraints = new List<BoolFormula>();
        if (Definitions != null)
        {
            // Se siguen hacia atrás las señales calculadas, nivel a nivel, y se fijan las constantes que aparezcan.
            var done = new HashSet<string>(StringComparer.Ordinal);
            var pending = formula.Variables().ToList();
            for (var level = 0; level < DefinitionDepth && pending.Count > 0; level++)
            {
                var next = new List<string>();
                foreach (var name in pending.Where(done.Add))
                {
                    if (Definitions.TryGetValue(name, out var definition))
                    {
                        var logic = FixConstants(BoolFormula.From(definition));
                        constraints.Add(BoolFormula.Equivalent(BoolFormula.Var(name), logic));
                        next.AddRange(logic.Variables());
                    }
                }
                pending = next;
            }
        }
        var variables = BoolFormula.And([formula, .. constraints]).Variables();
        if (ExcludeHomeWork)
        {
            constraints.AddRange(SamePrefixPairs(variables, HomeWork(), "H", "W"));
        }
        if (ExcludeReleasePickDrop)
        {
            constraints.AddRange(SamePrefixPairs(variables, ReleasePickDrop(), "Pick", "Drop"));
        }
        if (ExcludeDecodedValues)
        {
            var groups = variables
                .Select(v => (Variable: v, Match: DecodedValue().Match(v.Trim('"'))))
                .Where(x => x.Match.Success)
                .GroupBy(x => x.Match.Groups[1].Value.Trim().Trim('"').ToUpperInvariant())
                .Select(g => g.GroupBy(x => x.Match.Groups[2].Value.Trim()).Select(v => v.First().Variable).ToList())
                .Where(g => g.Count > 1);
            foreach (var group in groups)
            {
                for (var i = 0; i < group.Count; i++)
                {
                    for (var j = i + 1; j < group.Count; j++)
                    {
                        constraints.Add(NotBoth(group[i], group[j]));
                    }
                }
            }
        }
        foreach (var group in ExclusiveGroups)
        {
            var present = group.Signals.Select((s, i) => Literal(s, group.Value(i)))
                .Where((_, i) => variables.Contains(group.Signals[i])).ToList();
            for (var i = 0; i < present.Count; i++)
            {
                for (var j = i + 1; j < present.Count; j++)
                {
                    constraints.Add(BoolFormula.Not(BoolFormula.And(present[i], present[j])));
                }
            }
        }
        return BoolFormula.And([formula, .. constraints]);
    }

    private static BoolFormula NotBoth(string a, string b) => BoolFormula.Not(BoolFormula.And(BoolFormula.Var(a), BoolFormula.Var(b)));

    // Pares de señales con el mismo nombre delante (grupo 1 de la expresión) que acaban en «first» y en «second»
    // (grupo 2): nunca están a 1 a la vez.
    private static IEnumerable<BoolFormula> SamePrefixPairs(IEnumerable<string> variables, Regex pattern, string first, string second) =>
        variables
            .Select(v => (Variable: v, Match: pattern.Match(v.Trim('"'))))
            .Where(x => x.Match.Success)
            .GroupBy(x => x.Match.Groups[1].Value.ToUpperInvariant())
            .Select(g => (First: g.FirstOrDefault(x => x.Match.Groups[2].Value.Equals(first, StringComparison.OrdinalIgnoreCase)).Variable,
                Second: g.FirstOrDefault(x => x.Match.Groups[2].Value.Equals(second, StringComparison.OrdinalIgnoreCase)).Variable))
            .Where(x => x.First != null && x.Second != null)
            .Select(x => NotBoth(x.First!, x.Second!));

    // «++2040+FIX04-ReleasePick» / «++2040+FIX04-ReleaseDrop»: lo de delante es la estación.
    private static Regex ReleasePickDrop() => _ReleasePickDrop ??= new Regex(@"^(.+)-Release(Pick|Drop)$", RegexOptions.IgnoreCase);

    private static Regex? _ReleasePickDrop;

    // «…-YA-H» / «…-YA-W»: sensores de home y work de un mismo actuador.
    // .NET Framework no tiene GeneratedRegex: se crea la primera vez que se usa.
    private static Regex HomeWork() => _HomeWork ??= new Regex(@"^(.*-Y[A-Z0-9]+)-([HW])$", RegexOptions.IgnoreCase);

    private static Regex? _HomeWork;

    // «…-ProcessCode=651» (bits decodificados de un valor) o «x == 5» (comparadores).
    // .NET Framework no tiene GeneratedRegex: se crea la primera vez que se usa.
    private static Regex DecodedValue() => _DecodedValue ??= new Regex(@"^(.+?)\s*==?\s*([^=<>]+?)$");

    private static Regex? _DecodedValue;
}
