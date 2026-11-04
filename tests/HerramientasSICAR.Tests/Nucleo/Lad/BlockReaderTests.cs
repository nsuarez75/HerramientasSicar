#nullable enable
using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HerramientasSICAR.Nucleo.Lad;
using HerramientasSICAR.Nucleo.SimaticMl;
using static HerramientasSICAR.Nucleo.SimaticMl.SimaticMlBuilder;

namespace HerramientasSICAR.Tests.Lad;

public class BlockReaderTests
{
    // Paso típico de una secuencia: dos contactos en serie con uno negado a ilockAuto y un contacto a transAuto.
    private static PlcBlock PasoSencillo()
    {
        var n = new FlgNetBuilder().Label("S005");
        var a = n.Global("Est-B1");
        var b = n.Global("Est-B2");
        var ilock = n.Local("Sequence", "ilockAuto");
        var c = n.Global("Est-B3");
        var trans = n.Local("Sequence", "transAuto");
        var c1 = n.Part("Contact");
        var c2 = n.Negated("Contact");
        var coil1 = n.Part("Coil");
        var c3 = n.Part("Contact");
        var coil2 = n.Part("Coil");
        n.Rail((c1, "in"), (c3, "in"))
            .Operand(a, c1, "operand").Wire((c1, "out"), (c2, "in"))
            .Operand(b, c2, "operand").Wire((c2, "out"), (coil1, "in"))
            .Operand(ilock, coil1, "operand")
            .Operand(c, c3, "operand").Wire((c3, "out"), (coil2, "in"))
            .Operand(trans, coil2, "operand");
        return BlockReader.Read(Block("FB", 1005, "Est_FB",
            LadNet(n, "S005 Bajar mesa", "Title_espanol Bajar mesa\nTitle_english Lower table")));
    }

    [Fact]
    public void Lee_el_bloque_y_sus_redes()
    {
        var block = PasoSencillo();
        Assert.Equal(BlockKind.FB, block.Kind);
        Assert.Equal(1005, block.Number);
        Assert.Equal("Est_FB", block.Name);
        var network = Assert.Single(block.Networks);
        Assert.Equal("S005", network.Label);
        Assert.Equal("S005 Bajar mesa", network.Title.Get());
        Assert.True(network.IsLad);
    }

    [Fact]
    public void Los_contactos_en_serie_y_negados_alimentan_la_bobina()
    {
        var network = PasoSencillo().Networks[0];
        var ilock = network.Assignments.Single(a => a.Target.LastName == "ilockAuto");
        Assert.Equal(AssignmentKind.Coil, ilock.Kind);
        var series = Assert.IsType<SeriesExpression>(ilock.Condition);
        Assert.Equal([new ContactExpression(Op("Est-B1"), false), new ContactExpression(Op("Est-B2"), true)],
            series.Items.Cast<ContactExpression>());
        Assert.Equal("\"Est-B1\" Y NO \"Est-B2\"", ilock.Condition.ToString());

        var trans = network.Assignments.Single(a => a.Target.LastName == "transAuto");
        Assert.Equal(new ContactExpression(Op("Est-B3"), false), trans.Condition);
        Assert.Equal(OperandScope.Local, trans.Target.Scope);
        Assert.Equal("#Sequence.transAuto", trans.Target.Text);
        Assert.Equal("Sequence.transAuto", trans.Target.Path);
    }

    [Fact]
    public void Las_ramas_en_paralelo_se_leen_como_O()
    {
        var n = new FlgNetBuilder();
        var a = n.Global("Est-B1");
        var b = n.Global("Est-B2");
        var c = n.Global("Est-B3");
        var target = n.Global("Est-Listo");
        var c1 = n.Part("Contact");
        var c2 = n.Part("Contact");
        var or = n.Part("O", "<TemplateValue Name=\"Card\" Type=\"Cardinality\">2</TemplateValue>");
        var c3 = n.Part("Contact");
        var coil = n.Part("Coil");
        n.Rail((c1, "in"), (c2, "in"))
            .Operand(a, c1, "operand").Operand(b, c2, "operand")
            .Wire((c1, "out"), (or, "in1")).Wire((c2, "out"), (or, "in2"))
            .Wire((or, "out"), (c3, "in")).Operand(c, c3, "operand")
            .Wire((c3, "out"), (coil, "in")).Operand(target, coil, "operand");

        var assignment = Assert.Single(BlockReader.Read(Block("FC", 1, "X", LadNet(n))).Networks[0].Assignments);
        Assert.Equal("(\"Est-B1\" O \"Est-B2\") Y \"Est-B3\"", assignment.Condition.ToString());
        var series = Assert.IsType<SeriesExpression>(assignment.Condition);
        Assert.IsType<ParallelExpression>(series.Items[0]);
    }

    [Fact]
    public void Los_comparadores_guardan_sus_dos_valores_y_el_tipo()
    {
        var n = new FlgNetBuilder();
        var code = n.Global("Rob-ProcessCode");
        var value = n.Literal("Int", "200");
        var target = n.Global("Rob-ProcessCode=200");
        var eq = n.Part("Eq", "<TemplateValue Name=\"SrcType\" Type=\"Type\">Int</TemplateValue>");
        var coil = n.Part("Coil");
        n.Rail((eq, "pre"))
            .Operand(code, eq, "in1").Operand(value, eq, "in2")
            .Wire((eq, "out"), (coil, "in")).Operand(target, coil, "operand");

        var assignment = Assert.Single(BlockReader.Read(Block("FC", 1, "X", LadNet(n))).Networks[0].Assignments);
        var comparison = Assert.IsType<Comparison>(assignment.Condition);
        Assert.Equal(CompareOperator.Equal, comparison.Operator);
        Assert.Equal("\"Rob-ProcessCode\"", comparison.Left.Operand!.Text);
        Assert.Equal(OperandScope.Literal, comparison.Right.Operand!.Scope);
        Assert.Equal("200", comparison.Right.Operand.Text);
        Assert.Equal("Int", comparison.DataType);
        Assert.Equal("\"Rob-ProcessCode\" == 200", assignment.Condition.ToString());
    }

    [Fact]
    public void Un_comparador_en_serie_con_contactos_queda_dentro_de_la_serie()
    {
        var n = new FlgNetBuilder();
        var a = n.Global("Est-B1");
        var level = n.Global("Est-Nivel");
        var limit = n.GlobalConstant("NIVEL_MAX");
        var target = n.Global("Est-Lleno");
        var c1 = n.Part("Contact");
        var ge = n.Part("Ge", "<TemplateValue Name=\"SrcType\" Type=\"Type\">Real</TemplateValue>");
        var coil = n.Part("Coil");
        n.Rail((c1, "in")).Operand(a, c1, "operand")
            .Wire((c1, "out"), (ge, "pre")).Operand(level, ge, "in1").Operand(limit, ge, "in2")
            .Wire((ge, "out"), (coil, "in")).Operand(target, coil, "operand");

        var assignment = Assert.Single(BlockReader.Read(Block("FC", 1, "X", LadNet(n))).Networks[0].Assignments);
        Assert.Equal("\"Est-B1\" Y \"Est-Nivel\" >= \"NIVEL_MAX\"", assignment.Condition.ToString());
        Assert.Equal(OperandScope.GlobalConstant, ((Comparison)((SeriesExpression)assignment.Condition).Items[1]).Right.Operand!.Scope);
    }

    [Fact]
    public void Set_y_reset_salen_como_dos_escrituras_de_la_misma_senal()
    {
        var n = new FlgNetBuilder();
        var on = n.Global("Est-Marcha");
        var off = n.Global("Est-Paro");
        var target = n.Global("Est-EnMarcha");
        var c1 = n.Part("Contact");
        var c2 = n.Part("Contact");
        var sr = n.Part("Sr");
        n.Rail((c1, "in"), (c2, "in"))
            .Operand(on, c1, "operand").Operand(off, c2, "operand")
            .Wire((c1, "out"), (sr, "s")).Wire((c2, "out"), (sr, "r1"))
            .Operand(target, sr, "operand");

        var assignments = BlockReader.Read(Block("FC", 1, "X", LadNet(n))).Networks[0].Assignments;
        Assert.Equal(2, assignments.Count);
        Assert.All(assignments, a => Assert.Equal("Est-EnMarcha", a.Target.LastName));
        Assert.Equal("\"Est-Marcha\"", assignments.Single(a => a.Kind == AssignmentKind.Set).Condition.ToString());
        Assert.Equal("\"Est-Paro\"", assignments.Single(a => a.Kind == AssignmentKind.Reset).Condition.ToString());
    }

    [Fact]
    public void Las_bobinas_set_y_reset_sueltas_se_reconocen()
    {
        var n = new FlgNetBuilder();
        var a = n.Global("Est-B1");
        var target = n.Global("Est-Memoria");
        var c1 = n.Part("Contact");
        var reset = n.Part("RCoil");
        n.Rail((c1, "in")).Operand(a, c1, "operand").Wire((c1, "out"), (reset, "in")).Operand(target, reset, "operand");

        var assignment = Assert.Single(BlockReader.Read(Block("FC", 1, "X", LadNet(n))).Networks[0].Assignments);
        Assert.Equal(AssignmentKind.Reset, assignment.Kind);
    }

    [Fact]
    public void El_move_a_stepNplus1_guarda_el_paso_destino_y_su_condicion()
    {
        var n = new FlgNetBuilder();
        var cond = n.Global("Est-Pieza");
        var step = n.Literal("USInt", "15");
        var target = n.Local("Sequence", "stepNplus1");
        var c1 = n.Part("Contact");
        var move = n.Part("Move", "<TemplateValue Name=\"Card\" Type=\"Cardinality\">1</TemplateValue>");
        n.Rail((c1, "in")).Operand(cond, c1, "operand")
            .Wire((c1, "out"), (move, "en")).Operand(step, move, "in")
            .ToOperand(move, "out1", target);

        var assignment = Assert.Single(BlockReader.Read(Block("FB", 1001, "X", LadNet(n))).Networks[0].Assignments);
        Assert.Equal(AssignmentKind.Move, assignment.Kind);
        Assert.Equal("stepNplus1", assignment.Target.LastName);
        Assert.Equal("15", assignment.Value!.Operand!.Text);
        Assert.Equal("\"Est-Pieza\"", assignment.Condition.ToString());
    }

    [Fact]
    public void La_salida_de_un_temporizador_es_una_caja_con_sus_entradas()
    {
        var n = new FlgNetBuilder();
        var a = n.Global("Est-B1");
        var time = n.Literal("Time", "T#5s");
        var target = n.Global("Est-Retardo");
        var c1 = n.Part("Contact");
        var ton = n.Timer("TON", "Est-Tiempo_DB");
        var coil = n.Part("Coil");
        n.Rail((c1, "in")).Operand(a, c1, "operand")
            .Wire((c1, "out"), (ton, "IN")).Operand(time, ton, "PT")
            .Wire((ton, "Q"), (coil, "in")).Operand(target, coil, "operand");

        var assignment = Assert.Single(BlockReader.Read(Block("FC", 1, "X", LadNet(n))).Networks[0].Assignments);
        var box = Assert.IsType<BoxOutput>(assignment.Condition);
        Assert.Equal("TON", box.Box);
        Assert.Equal("q", box.Pin);
        Assert.Equal("\"Est-Tiempo_DB\"", box.Instance!.Text);
        Assert.Equal("\"Est-B1\"", box.Inputs["in"].ToString());
        Assert.Equal("T#5s", box.Inputs["pt"].Operand!.Text);
    }

    [Fact]
    public void Las_salidas_de_una_llamada_se_registran_como_escrituras()
    {
        var n = new FlgNetBuilder();
        var output = n.Global("Est-Datos");
        var call = n.Call("LeerDatos_FC");
        n.Rail((call, "en")).ToOperand(call, "Data", output);

        var assignment = Assert.Single(BlockReader.Read(Block("FC", 1, "X", LadNet(n))).Networks[0].Assignments);
        Assert.Equal(AssignmentKind.BoxOutput, assignment.Kind);
        Assert.Equal("LeerDatos_FC", assignment.Box);
        Assert.Equal("Est-Datos", assignment.Target.LastName);
    }

    [Fact]
    public void Una_entrada_sin_cable_no_conduce()
    {
        var n = new FlgNetBuilder();
        var target = n.Global("Est-Salida");
        var coil = n.Part("Coil");
        n.Operand(target, coil, "operand");

        var assignment = Assert.Single(BlockReader.Read(Block("FC", 1, "X", LadNet(n))).Networks[0].Assignments);
        Assert.IsType<Unconnected>(assignment.Condition);
    }

    [Fact]
    public void Las_redes_que_no_son_LAD_se_listan_sin_interpretar()
    {
        var block = BlockReader.Read(Block("FB", 1001, "X", Stl("Branch distributor")));
        var network = Assert.Single(block.Networks);
        Assert.Equal("STL", network.Language);
        Assert.False(network.IsLad);
        Assert.Empty(network.Assignments);
    }

    [Fact]
    public void Un_archivo_que_no_es_un_bloque_se_rechaza()
    {
        var doc = System.Xml.Linq.XDocument.Parse("<Document><SW.Tags.PlcTagTable ID=\"0\" /></Document>");
        Assert.Throws<InvalidDataException>(() => BlockReader.Read(doc));
    }

    private static Operand Op(string name) => new(OperandScope.Global, [name], "\"" + name + "\"");
}
