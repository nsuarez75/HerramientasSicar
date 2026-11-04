#nullable enable
using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HerramientasSICAR.Nucleo.Lad;
using HerramientasSICAR.Nucleo.Logic;
using HerramientasSICAR.Nucleo.Sicar;
using static HerramientasSICAR.Nucleo.Lad.LadExpression;

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
    public void Dos_pasos_con_interlocks_independientes_pueden_sincronizar_a_la_vez()
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
    public void Un_paso_cuya_transicion_ya_se_cumple_no_es_ejecutable()
    {
        // S001 sale con «Pieza» y S005 la necesita: no pueden ser candidatos a la vez.
        var sequence = Sequence(
            Step(1, C("Arriba"), C("Pieza")),
            Step(5, Series(C("Pieza"), C("Arriba")), C("Abajo")));
        Assert.Empty(new SyncChecker().Check(sequence));
    }

    [Fact]
    public void Un_paso_sin_ILOCK_A_programado_nunca_es_ejecutable()
    {
        // Paso manual: ilockAuto sin programar vale 0 (manual SICAR 2.3).
        var sequence = Sequence(
            Step(1, C("Arriba"), C("Pieza")),
            Step(100, null, null));
        Assert.Empty(new SyncChecker().Check(sequence));
    }

    [Fact]
    public void Un_paso_sin_TRANS_A_programado_es_ejecutable_si_cumple_sus_interlocks()
    {
        var sequence = Sequence(
            Step(1, C("Arriba"), C("Pieza")),
            Step(5, C("Abajo", negated: true), null));
        Assert.Single(new SyncChecker().Check(sequence));
    }

    [Fact]
    public void Los_interlocks_de_PERM_se_suman_a_los_de_cada_paso()
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
    public void ilockExtSync_participa_en_la_sincronizacion()
    {
        var sequence = Sequence(
            Step(1, C("Arriba"), C("Pieza"), extSync: C("RobotFuera")),
            Step(5, C("Abajo"), C("Fin"), extSync: C("RobotFuera", negated: true)));
        Assert.Empty(new SyncChecker().Check(sequence));
    }

    [Fact]
    public void Los_sensores_home_y_work_de_un_actuador_se_excluyen()
    {
        var sequence = Sequence(
            Step(1, C("Est-YA-H"), C("Pieza")),
            Step(5, C("Est-YA-W"), C("Fin")));
        Assert.Empty(new SyncChecker().Check(sequence));
        Assert.Single(new SyncChecker { ExcludeHomeWork = false }.Check(sequence));
    }

    [Fact]
    public void Dos_valores_distintos_de_una_misma_senal_se_excluyen()
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
    public void Un_salto_que_se_ejecuta_al_sincronizar_impide_probar_los_pasos_que_se_salta()
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
    public void Un_salto_bloqueado_con_pos_Pulse_OM_Sync_no_salta_nada_al_sincronizar()
    {
        var sequence = Sequence(
            Step(1, C("Arriba"), C("Pieza"), jumps: [new StepJump(10, Not(SyncPulse))]),
            Step(5, C("Pieza", negated: true), C("Abajo")),
            Step(10, C("Abajo"), C("Fin")));
        var pairs = new SyncChecker().Check(sequence).Select(c => (c.StepA.Number, c.StepB.Number)).ToList();
        Assert.Contains((1, 5), pairs);
    }

    [Fact]
    public void En_robots_no_se_comprueban_los_pasos_de_servicio()
    {
        var sequence = Sequence(
            Step(1, C("Arriba"), C("Pieza")),
            Step(224, C("Servicio"), C("FinServicio")));
        Assert.Empty(new SyncChecker().Check(sequence));
        Assert.Single(new SyncChecker { IsRobot = false }.Check(sequence));
    }

    [Fact]
    public void alwaysTrue_y_alwaysFalse_son_constantes()
    {
        var sequence = Sequence(
            Step(1, C("alwaysFalse"), C("Pieza")),
            Step(5, C("alwaysTrue"), C("Fin")));
        Assert.Empty(new SyncChecker().Check(sequence));
    }

    [Fact]
    public void Las_diferencias_dicen_que_senal_exige_cada_paso()
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
    public void Las_senales_calculadas_por_el_programa_se_sustituyen_por_su_logica()
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
    public void Solo_se_definen_las_senales_escritas_por_una_unica_bobina_sin_memoria()
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
    public void Un_grupo_excluyente_decidido_por_el_usuario_quita_el_falso_positivo()
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
    public void Dos_grupos_con_las_mismas_senales_son_el_mismo_aunque_cambie_el_orden()
    {
        Assert.Equal(new ExclusiveGroup(["\"A\"", "\"B\""]), new ExclusiveGroup(["\"B\"", "\"A\""]));
    }

    [Fact]
    public void La_formula_busca_soluciones_y_detecta_lo_imposible()
    {
        var a = BoolFormula.Var("A");
        Assert.NotNull(BoolFormula.And(a, BoolFormula.Var("B")).Solve());
        Assert.Null(BoolFormula.And(a, BoolFormula.Not(a)).Solve());
        Assert.True(BoolFormula.Or(BoolFormula.And(a, BoolFormula.Not(a)), BoolFormula.Var("C")).IsSatisfiable);
    }
}
