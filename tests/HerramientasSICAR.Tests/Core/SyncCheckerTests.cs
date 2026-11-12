#nullable enable
using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HerramientasSICAR.Core.Lad;
using HerramientasSICAR.Core.Logic;
using HerramientasSICAR.Core.Sicar;
using static HerramientasSICAR.Core.Lad.LadExpression;

namespace HerramientasSICAR.Tests;

public class SyncCheckerTests
{
    private static Operand G(string name) => new(OperandScope.Global, [name], "\"" + name + "\"");

    private static ContactExpression C(string name, bool negated = false) => new(G(name), negated);

    private static readonly ContactExpression SyncPulse =
        new(new Operand(OperandScope.Local, ["Sequence", "pos_Pulse_OM_Sync"], "#Sequence.pos_Pulse_OM_Sync"), false);

    private static SequenceStep Step(int number, LadExpression? ilock, LadExpression? trans,
        LadExpression? extSync = null, IReadOnlyList<StepJump>? jumps = null)
    {
        var sections = new Dictionary<SectionKind, LadExpression>();
        if (ilock != null)
        {
            sections[SectionKind.IlockAuto] = ilock;
        }
        if (trans != null)
        {
            sections[SectionKind.TransAuto] = trans;
        }
        if (extSync != null)
        {
            sections[SectionKind.IlockExtSync] = extSync;
        }
        return new SequenceStep(number, SicarNumbering.StepLabel(number), number) { Sections = sections, Jumps = jumps ?? [] };
    }

    private static SequenceBlock Sequence(params SequenceStep[] steps) => new(7, "Mesa") { Steps = steps };

    [Fact]
    public void Conflicts_IndependentIlocks_ReportsPair()
    {
        var sequence = Sequence(
            Step(1, C("Arriba"), C("Pieza")),
            Step(5, C("Puerta"), C("Abajo")));
        var conflict = Assert.Single(new SyncChecker().Check(sequence));
        Assert.Equal((1, 5), (conflict.StepA.Number, conflict.StepB.Number));
        // El estado encontrado cumple los dos: arriba y con la puerta, sin pieza y sin estar abajo.
        Assert.True(conflict.State["\"Arriba\""]);
        Assert.True(conflict.State["\"Puerta\""]);
        Assert.False(conflict.State["\"Pieza\""]);
        Assert.False(conflict.State["\"Abajo\""]);
    }

    [Fact]
    public void Conflicts_TransitionFulfilled_StepNotExecutable()
    {
        // S001 sale con «Pieza» y S005 la necesita: no pueden ser candidatos a la vez.
        var sequence = Sequence(
            Step(1, C("Arriba"), C("Pieza")),
            Step(5, Series(C("Pieza"), C("Arriba")), C("Abajo")));
        Assert.Empty(new SyncChecker().Check(sequence));
    }

    [Fact]
    public void Conflicts_NoIlockAuto_StepNeverExecutable()
    {
        // Paso manual: ilockAuto sin programar vale 0 (manual SICAR 2.3).
        var sequence = Sequence(
            Step(1, C("Arriba"), C("Pieza")),
            Step(100, null, null));
        Assert.Empty(new SyncChecker().Check(sequence));
    }

    [Fact]
    public void Conflicts_NoTransAuto_ExecutableIfIlocksMet()
    {
        var sequence = Sequence(
            Step(1, C("Arriba"), C("Pieza")),
            Step(5, C("Abajo", negated: true), null));
        Assert.Single(new SyncChecker().Check(sequence));
    }

    [Fact]
    public void Conflicts_PermIlocks_AddedToEachStep()
    {
        var sequence = Sequence(
            Step(1, C("Arriba"), C("Pieza")),
            Step(5, C("Abajo"), C("Fin"))) with
        {
            // PERM exige no estar abajo: S005 ya no puede ser candidato.
            Permanent = new SequenceStep(0, "PERM", 1)
            {
                Sections = new Dictionary<SectionKind, LadExpression> { [SectionKind.IlockAuto] = C("Abajo", negated: true) },
            },
        };
        Assert.Empty(new SyncChecker().Check(sequence));
    }

    [Fact]
    public void Conflicts_IlockExtSync_TakesPart()
    {
        var sequence = Sequence(
            Step(1, C("Arriba"), C("Pieza"), extSync: C("RobotFuera")),
            Step(5, C("Abajo"), C("Fin"), extSync: C("RobotFuera", negated: true)));
        Assert.Empty(new SyncChecker().Check(sequence));
    }

    [Fact]
    public void Conflicts_HomeAndWorkSensors_Exclusive()
    {
        var sequence = Sequence(
            Step(1, C("Est-YA-H"), C("Pieza")),
            Step(5, C("Est-YA-W"), C("Fin")));
        Assert.Empty(new SyncChecker().Check(sequence));
        Assert.Single(new SyncChecker { ExcludeHomeWork = false }.Check(sequence));
    }

    [Fact]
    public void Conflicts_DifferentValuesSameSignal_Exclusive()
    {
        var code200 = new Comparison(CompareOperator.Equal, LadValue.Of(G("Rob-Code")), LadValue.Of(new Operand(OperandScope.Literal, [], "200")), "Int");
        var code210 = code200 with { Right = LadValue.Of(new Operand(OperandScope.Literal, [], "210")) };
        var sequence = Sequence(
            Step(1, code200, C("Pieza")),
            Step(5, C("Rob-ProcessCode=210"), C("Fin")),
            Step(10, code210, C("Fin2")));
        // S001 (== 200) y S010 (== 210) se excluyen; S005 usa otra señal y sí choca con los dos.
        var pairs = new SyncChecker().Check(sequence).Select(c => (c.StepA.Number, c.StepB.Number)).ToList();
        Assert.Equal([(1, 5), (5, 10)], pairs);
    }

    [Fact]
    public void Conflicts_ActiveJump_SkipsJumpedSteps()
    {
        // S001 salta a S010 siempre: S005 no se prueba nunca.
        var sequence = Sequence(
            Step(1, C("Arriba"), C("Pieza"), jumps: [new StepJump(10, Rail)]),
            Step(5, C("Pieza", negated: true), C("Abajo")),
            Step(10, C("Abajo"), C("Fin")));
        var pairs = new SyncChecker().Check(sequence).Select(c => (c.StepA.Number, c.StepB.Number)).ToList();
        Assert.DoesNotContain(pairs, p => p.Item1 == 5 || p.Item2 == 5);
    }

    [Fact]
    public void Conflicts_JumpBlockedBySyncPulse_SkipsNothing()
    {
        var sequence = Sequence(
            Step(1, C("Arriba"), C("Pieza"), jumps: [new StepJump(10, Not(SyncPulse))]),
            Step(5, C("Pieza", negated: true), C("Abajo")),
            Step(10, C("Abajo"), C("Fin")));
        var pairs = new SyncChecker().Check(sequence).Select(c => (c.StepA.Number, c.StepB.Number)).ToList();
        Assert.Contains((1, 5), pairs);
    }

    [Fact]
    public void Conflicts_RobotServiceSteps_NotChecked()
    {
        var sequence = Sequence(
            Step(1, C("Arriba"), C("Pieza")),
            Step(224, C("Servicio"), C("FinServicio")));
        Assert.Empty(new SyncChecker().Check(sequence));
        Assert.Single(new SyncChecker { IsRobot = false }.Check(sequence));
    }

    [Fact]
    public void Conflicts_AlwaysTrueAndFalse_AreConstants()
    {
        var sequence = Sequence(
            Step(1, C("alwaysFalse"), C("Pieza")),
            Step(5, C("alwaysTrue"), C("Fin")));
        Assert.Empty(new SyncChecker().Check(sequence));
    }

    [Fact]
    public void DifferencesOf_Pair_ListsSignalsPerStep()
    {
        var sequence = Sequence(
            Step(1, Series(C("Arriba"), C("Libre")), C("Pieza")),
            Step(5, C("Libre"), C("Fin")));
        var checker = new SyncChecker();
        var conflict = Assert.Single(checker.Check(sequence));
        var differences = checker.Differences(conflict);
        // S001 exige Arriba = 1 y Pieza = 0 (su transición); S005 exige Fin = 0. «Libre» la exigen los dos.
        Assert.Equal(["1:\"Arriba\"=True", "1:\"Pieza\"=False", "5:\"Fin\"=False"],
            differences.Select(d => $"{d.Requiring.Number}:{d.Signal}={d.Value}"));
        Assert.All(differences, d => Assert.False(d.OtherChecksIt));
    }

    [Fact]
    public void Conflicts_ComputedSignals_ReplacedByLogic()
    {
        // Presencia = PP1 Y PP2; Ausencia = NO PP1 Y NO PP2 (bloque de referencias cruzadas): nunca a la vez.
        var definitions = new Dictionary<string, LadExpression>
        {
            ["\"Presencia\""] = Series(C("PP1"), C("PP2")),
            ["\"Ausencia\""] = Series(C("PP1", negated: true), C("PP2", negated: true)),
        };
        var sequence = Sequence(
            Step(1, C("Ausencia"), C("Cargada")),
            Step(25, C("Presencia"), C("Descargada")));

        Assert.Single(new SyncChecker().Check(sequence));
        Assert.Empty(new SyncChecker { Definitions = definitions }.Check(sequence));
    }

    [Fact]
    public void Build_SignalDefinitions_OnlySingleCoilWithoutMemory()
    {
        var a = new Operand(OperandScope.Global, ["A"], "\"A\"");
        var b = new Operand(OperandScope.Global, ["B"], "\"B\"");
        var m = new Operand(OperandScope.Global, ["M"], "\"M\"");
        var block = new PlcBlock(BlockKind.FC, 1, "X", "LAD")
        {
            Networks =
            [
                new Network(1, "LAD") { Assignments = [new Assignment(AssignmentKind.Coil, a, C("S1"))] },
                // B se escribe con SET y RESET: tiene memoria.
                new Network(2, "LAD") { Assignments = [new Assignment(AssignmentKind.Set, b, C("S2")), new Assignment(AssignmentKind.Reset, b, C("S3"))] },
                // M se retiene a sí misma.
                new Network(3, "LAD") { Assignments = [new Assignment(AssignmentKind.Coil, m, Parallel(C("S4"), C("M")))] },
            ],
        };
        var definitions = SignalDefinitions.Build([block]);
        Assert.Equal(["\"A\""], definitions.Keys);
    }

    [Fact]
    public void Conflicts_UserExclusiveGroup_RemovesFalsePositive()
    {
        // Robot en el código de decisión: el paso lo eligen prioridades que salen de un bloque llamado.
        var sequence = Sequence(
            Step(10, Series(C("Rob-Code=210"), C("PrioridadA")), C("Rob-Code=220")),
            Step(11, Series(C("Rob-Code=210"), C("PrioridadB")), C("Rob-Code=220")));

        var checker = new SyncChecker();
        var conflict = Assert.Single(checker.Check(sequence));
        // Solo las que exige a 1 uno de los pasos y el otro no; el código 210 lo exigen los dos.
        Assert.Equal(["\"PrioridadA\"", "\"PrioridadB\""], checker.ExclusionCandidates(conflict));

        var decided = new SyncChecker { ExclusiveGroups = [new ExclusiveGroup(["\"PrioridadA\"", "\"PrioridadB\""])] };
        Assert.Empty(decided.Check(sequence));
    }

    [Fact]
    public void ExclusionCandidates_OnlyOneSignal_ReturnsEmpty()
    {
        // Solo S010 exige una señal a 1 que S011 no exige: un grupo de una señal no dice nada.
        var sequence = Sequence(
            Step(10, Series(C("Rob-Code=210"), C("PrioridadA")), C("Rob-Code=220")),
            Step(11, C("Rob-Code=210"), C("Rob-Code=220")));

        var checker = new SyncChecker();
        var conflict = Assert.Single(checker.Check(sequence));

        Assert.Empty(checker.ExclusionCandidates(conflict));
    }

    [Fact]
    public void Equals_SameSignalsOtherOrder_ReturnsTrue()
    {
        Assert.Equal(new ExclusiveGroup(["\"A\"", "\"B\""]), new ExclusiveGroup(["\"B\"", "\"A\""]));
    }

    [Fact]
    public void Solve_Formula_FindsSolutionOrNull()
    {
        var a = BoolFormula.Var("A");
        Assert.NotNull(BoolFormula.And(a, BoolFormula.Var("B")).Solve());
        Assert.Null(BoolFormula.And(a, BoolFormula.Not(a)).Solve());
        Assert.True(BoolFormula.Or(BoolFormula.And(a, BoolFormula.Not(a)), BoolFormula.Var("C")).IsSatisfiable);
    }
}
