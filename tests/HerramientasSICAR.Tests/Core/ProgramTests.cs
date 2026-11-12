#nullable enable
using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HerramientasSICAR.Core.Lad;
using HerramientasSICAR.Core.Sicar;
using HerramientasSICAR.Core.SimaticMl;
using static HerramientasSICAR.Core.SimaticMl.SimaticMlBuilder;

namespace HerramientasSICAR.Tests;

public class ProgramTests
{
    // Red del FC983: llamada al FC995 con el número de OM y su rango de secuencias.
    private static Net OmCall(int om, int from, int to)
    {
        var n = new FlgNetBuilder();
        var number = n.Literal("Int", om.ToString());
        var start = n.Literal("Int", from.ToString());
        var end = n.Literal("Int", to.ToString());
        var call = n.Call("OpmodeDiagAddOn_FC");
        n.Rail((call, "en")).Operand(number, call, "noOPmode").Operand(start, call, "seqMBFrom").Operand(end, call, "seqMBTo");
        return LadNet(n, "OM" + om);
    }

    [Fact]
    public void Read_Fc983_ReturnsOmRanges()
    {
        var fc983 = BlockReader.Read(Block("FC", 983, "CallOpmodePanelDiagAddOn_FC", OmCall(1, 1, 10), OmCall(2, 11, 20)));
        var ranges = OperationModes.Read(fc983);
        Assert.Equal([new OperationModeRange(1, 1, 10), new OperationModeRange(2, 11, 20)], ranges);
        Assert.Equal(1, OperationModes.Of(ranges, 7));
        Assert.Equal(2, OperationModes.Of(ranges, 11));
        Assert.Null(OperationModes.Of(ranges, 50));
    }

    [Fact]
    public void Read_Call_KeepsParameters()
    {
        var fc983 = BlockReader.Read(Block("FC", 983, "X", OmCall(3, 21, 30)));
        var call = Assert.Single(fc983.Networks[0].Calls);
        Assert.Equal("OpmodeDiagAddOn_FC", call.Block);
        Assert.Equal("FC", call.BlockType);
        Assert.Equal("21", call.Inputs["seqmbfrom"].Operand!.Text);
    }

    [Fact]
    public void Build_CrossReferences_ListsWritesAndReads()
    {
        // FC de salidas: «Est-PiezaOK» = PP1 Y PP2. FB de secuencia: lee «Est-PiezaOK».
        var w = new FlgNetBuilder();
        var pp1 = w.Global("Est-PP1");
        var pp2 = w.Global("Est-PP2");
        var ok = w.Global("Est-PiezaOK");
        var c1 = w.Part("Contact");
        var c2 = w.Part("Contact");
        var coil = w.Part("Coil");
        w.Rail((c1, "in")).Operand(pp1, c1, "operand").Wire((c1, "out"), (c2, "in")).Operand(pp2, c2, "operand")
            .Wire((c2, "out"), (coil, "in")).Operand(ok, coil, "operand");
        var fc = BlockReader.Read(Block("FC", 1007, "Est_FC", LadNet(w, "Pieza OK")));

        var r = new FlgNetBuilder().Label("S005");
        var okRead = r.Global("Est-PiezaOK");
        var ilock = r.Local("Sequence", "ilockAuto");
        var c3 = r.Part("Contact");
        var coil2 = r.Part("Coil");
        r.Rail((c3, "in")).Operand(okRead, c3, "operand").Wire((c3, "out"), (coil2, "in")).Operand(ilock, coil2, "operand");
        var fb = BlockReader.Read(Block("FB", 1007, "Est_FB", LadNet(r, "S005 Bajar")));

        var refs = new CrossReferences([fc, fb]);
        var target = new Operand(OperandScope.Global, ["Est-PiezaOK"], "\"Est-PiezaOK\"");
        var write = Assert.Single(refs.WritersOf(target));
        Assert.Equal("Est_FC", write.Block.Name);
        Assert.Equal(AssignmentKind.Coil, write.Assignment.Kind);
        Assert.Equal(["Est_FB"], refs.ReadersOf(target).Select(x => x.Block.Name));
        Assert.Equal(["Est_FC"], refs.ReadersOf(new Operand(OperandScope.Global, ["Est-PP1"], "\"Est-PP1\"")).Select(x => x.Block.Name));
        // Las variables locales (#Sequence…) no son señales globales.
        Assert.Empty(refs.WritersOf(new Operand(OperandScope.Local, ["Sequence", "ilockAuto"], "#Sequence.ilockAuto")));
    }
}
