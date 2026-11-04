#nullable enable
using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HerramientasSICAR.Nucleo.Lad;
using HerramientasSICAR.Nucleo.Sicar;
using static HerramientasSICAR.Nucleo.Lad.LadExpression;

namespace HerramientasSICAR.Tests.Lad;

public class LadLayoutTests
{
    private static LadExpression Contacto(string nombre) => new ContactExpression(new Operand(OperandScope.Global, [nombre], nombre), false);

    [Fact]
    public void Las_ramas_que_salen_del_mismo_cable_comparten_lo_anterior()
    {
        var cable = LadExpression.Rail;
        for (var i = 0; i < 40; i++)
        {
            var (a, b) = (Contacto("A" + i), Contacto("B" + i));
            cable = LadExpression.Junction([LadExpression.Series(cable, a), LadExpression.Series(cable, b)]);
        }

        Assert.Equal(80, cable.Leaves().Count());
    }

    [Fact]
    public void Dos_contactos_iguales_en_ramas_distintas_no_se_juntan()
    {
        var una = LadExpression.Series(Contacto("A"), Contacto("B"));
        var otra = LadExpression.Series(Contacto("A"), Contacto("C"));

        Assert.Equal(4, LadExpression.Junction([una, otra]).Leaves().Count());
    }
    private static ContactExpression C(string name, bool negated = false) =>
        new(new Operand(OperandScope.Global, [name], "\"" + name + "\""), negated);

    private static string Names(LadLayout layout) =>
        string.Join(" ", layout.Cells.Select(c => $"{c.Leaf}@{c.Column},{c.Row}"));

    [Fact]
    public void Lo_que_va_en_serie_queda_en_una_fila()
    {
        var layout = LadLayout.Build(Series(C("A"), C("B"), C("C")));
        Assert.Equal((3, 1), (layout.Columns, layout.Rows));
        Assert.Equal("\"A\"@0,0 \"B\"@1,0 \"C\"@2,0", Names(layout));
        Assert.Empty(layout.Wires);
    }

    [Fact]
    public void Las_ramas_en_paralelo_van_una_debajo_de_otra_y_se_unen_por_los_lados()
    {
        var layout = LadLayout.Build(Series(Parallel(Series(C("A"), C("B")), C("C")), C("D")));

        Assert.Equal((3, 2), (layout.Columns, layout.Rows));
        Assert.Equal("\"A\"@0,0 \"B\"@1,0 \"C\"@0,1 \"D\"@2,0", Names(layout));
        // La rama corta se alarga hasta el final y las dos se unen a izquierda y derecha.
        Assert.Contains(new LadWire(1, 1, 2, 1), layout.Wires);
        Assert.Contains(new LadWire(0, 0, 0, 1), layout.Wires);
        Assert.Contains(new LadWire(2, 0, 2, 1), layout.Wires);
    }

    [Fact]
    public void Un_paralelo_dentro_de_otro_cuenta_todas_sus_filas()
    {
        var layout = LadLayout.Build(Parallel(Series(C("A"), Parallel(C("B"), C("C"))), C("D")));
        Assert.Equal((2, 3), (layout.Columns, layout.Rows));
        Assert.Contains(layout.Cells, c => c is { Row: 2, Leaf: ContactExpression { Operand.Components: ["D"] } });
    }

    [Fact]
    public void Un_NOT_de_un_contacto_es_un_contacto_negado()
    {
        var layout = LadLayout.Build(Not(C("A")));
        Assert.Equal(new ContactExpression(C("A").Operand, true), Assert.Single(layout.Cells).Leaf);
    }

    [Fact]
    public void Un_NOT_de_varias_instrucciones_se_dibuja_detras_de_ellas()
    {
        var layout = LadLayout.Build(Not(Series(C("A"), C("B"))));
        Assert.Equal(3, layout.Columns);
        Assert.Equal("NOT", layout.Cells[^1].Leaf.ToString());
    }

    [Fact]
    public void Una_condicion_siempre_cierta_no_tiene_instrucciones()
    {
        var layout = LadLayout.Build(Rail);
        Assert.Equal(0, layout.Columns);
        Assert.Empty(layout.Cells);
    }

    [Fact]
    public void Un_paso_da_un_escalon_por_seccion_y_por_salto_en_orden()
    {
        var step = new SequenceStep(5, "S005", 3)
        {
            Sections = new Dictionary<SectionKind, LadExpression>
            {
                [SectionKind.TransAuto] = C("Fin"),
                [SectionKind.IlockAuto] = C("Pieza"),
            },
            Jumps = [new StepJump(15, C("Saltar"))],
        };

        Assert.Equal(["ilockAuto", "transAuto", "→ S015"], StepRungs.Build(step).Select(r => r.Coil));
    }
}
