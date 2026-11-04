#nullable enable
using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HerramientasSICAR.Nucleo.Lad;
using HerramientasSICAR.Nucleo.SimaticMl;
using HerramientasSICAR.Nucleo.Symbols;
using static HerramientasSICAR.Nucleo.SimaticMl.SimaticMlBuilder;

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
    public void Un_bit_dentro_de_una_doble_palabra_se_solapa_con_ella()
    {
        Assert.Equal(["Rob-Interface"], Table().Overlapping(G("Rob-Code=1")).Select(t => t.Name));
    }

    [Fact]
    public void No_se_solapa_lo_que_esta_fuera_ni_en_otra_zona()
    {
        Assert.Empty(Table().Overlapping(G("Rob-Fuera")));
        Assert.Empty(Table().Overlapping(G("Rob-Salida")));
    }

    [Fact]
    public void Un_parametro_InOut_de_una_llamada_cuenta_como_escritura()
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
