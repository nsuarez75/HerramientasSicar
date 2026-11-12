#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
// Valor de la lógica de un paso con un estado de señales concreto (el que encuentra la comprobación, o el que el
// usuario cambia a mano para ver otros caminos). Lo que el estado no dice queda sin saber (null) y se arrastra:
// una serie con un contacto abierto no conduce aunque haya otros sin saber.
using HerramientasSICAR.Core.Lad;

namespace HerramientasSICAR.Core.Sicar;

public static class SyncState
{
    // Señal que representa una instrucción, con el mismo nombre que usa la comprobación; null si no es una
    // instrucción (cable, serie, paralelo…).
    public static string? Signal(LadExpression leaf) => leaf switch
    {
        ContactExpression c => c.Operand.Text,
        PowerRail or Unconnected or SeriesExpression or ParallelExpression or NotExpression or XorExpression => null,
        _ => leaf.ToString(),
    };

    public static bool? Value(LadExpression expression, IReadOnlyDictionary<string, bool> state) => expression switch
    {
        PowerRail => true,
        Unconnected => false,
        SeriesExpression s => And(s.Items.Select(i => Value(i, state))),
        ParallelExpression p => Or(p.Items.Select(i => Value(i, state))),
        NotExpression n => Not(Value(n.Inner, state)),
        XorExpression x => Xor(x.Items.Select(i => Value(i, state))),
        ContactExpression c => Not(SignalValue(c.Operand.Text, state), c.Negated),
        _ => SignalValue(expression.ToString() ?? "", state),
    };

    // Si el paso es candidato al sincronizar con ese estado: ilockAuto y ilockExtSync de PERM y del paso a 1 y
    // transAuto a 0, con los valores por defecto del manual para lo no programado (como SyncChecker.SyncCondition).
    public static bool? IsCandidate(SequenceStep step, SequenceStep? permanent, IReadOnlyDictionary<string, bool> state) =>
        And(new[]
        {
            permanent == null ? true : Section(permanent, SectionKind.IlockAuto, true, state),
            permanent == null ? true : Section(permanent, SectionKind.IlockExtSync, true, state),
            Section(step, SectionKind.IlockAuto, false, state),
            Section(step, SectionKind.IlockExtSync, true, state),
            Not(Section(step, SectionKind.TransAuto, false, state)),
        });

    private static bool? Section(SequenceStep step, SectionKind kind, bool whenMissing, IReadOnlyDictionary<string, bool> state) =>
        step.Sections.TryGetValue(kind, out var logic) ? Value(logic, state) : whenMissing;

    private static bool? SignalValue(string signal, IReadOnlyDictionary<string, bool> state) =>
        SyncChecker.FixedValue(signal) ?? (state.TryGetValue(signal, out var value) ? value : null);

    private static bool? Not(bool? value, bool negate = true) => negate ? !value : value;

    private static bool? And(IEnumerable<bool?> values)
    {
        var list = values.ToList();
        return list.Any(v => v == false) ? false : list.Any(v => v == null) ? null : true;
    }

    private static bool? Or(IEnumerable<bool?> values)
    {
        var list = values.ToList();
        return list.Any(v => v == true) ? true : list.Any(v => v == null) ? null : false;
    }

    private static bool? Xor(IEnumerable<bool?> values)
    {
        var list = values.ToList();
        return list.Any(v => v == null) ? null : list.Count(v => v == true) % 2 == 1;
    }
}
