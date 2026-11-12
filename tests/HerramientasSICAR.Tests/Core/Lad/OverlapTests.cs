#nullable enable
using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HerramientasSICAR.Core.Lad;
using HerramientasSICAR.Core.SimaticMl;
using HerramientasSICAR.Core.Symbols;
using static HerramientasSICAR.Core.SimaticMl.SimaticMlBuilder;

namespace HerramientasSICAR.Tests.Lad;

public class OverlapTests
{
    private static SymbolTable Table() => new(
        [
            new PlcTag("Rob-Interface", "DWord", "%MD1000"),
            new PlcTag("Rob-Code=1", "Bool", "%M1001.0"),
            new PlcTag("Rob-Fuera", "Bool", "%M1004.0"),
            new PlcTag("Rob-Salida", "Bool", "%Q1001.0"),
        ], [], []);

    private static Operand G(string name) => new(OperandScope.Global, [name], "\"" + name + "\"");

    [Fact]
    public void Overlapping_BitInsideDword_ReturnsDword()
    {
        Assert.Equal(["Rob-Interface"], Table().Overlapping(G("Rob-Code=1")).Select(t => t.Name));
    }

    [Fact]
    public void Overlapping_OutsideOrOtherArea_ReturnsEmpty()
    {
        Assert.Empty(Table().Overlapping(G("Rob-Fuera")));
        Assert.Empty(Table().Overlapping(G("Rob-Salida")));
    }

    [Fact]
    public void Read_InOutParameter_CountsAsWrite()
    {
        var n = new FlgNetBuilder();
        var target = n.Global("Rob-Interface");
        var call = n.CallWithInOut("Robot_FB", "programInterface1");
        n.Rail((call, "en")).Operand(target, call, "programInterface1");

        var block = BlockReader.Read(Block("FC", 1, "X", LadNet(n)));

        var write = Assert.Single(new CrossReferences([block]).WritersOf(G("Rob-Interface")));
        Assert.Equal("Robot_FB", write.Assignment.Box);
    }
}
