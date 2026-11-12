#nullable enable
using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HerramientasSICAR.Core.Lad;
using HerramientasSICAR.Core.Sicar;
using static HerramientasSICAR.Core.Lad.LadExpression;

namespace HerramientasSICAR.Tests.Lad;

public class LadLayoutTests
{
    private static LadExpression Contact(string name) => new ContactExpression(new Operand(OperandScope.Global, [name], name), false);

    [Fact]
    public void Build_BranchesFromSameWire_SharePrefix()
    {
        var cable = LadExpression.Rail;
        for (var i = 0; i < 40; i++)
        {
            var (a, b) = (Contact("A" + i), Contact("B" + i));
            cable = LadExpression.Junction([LadExpression.Series(cable, a), LadExpression.Series(cable, b)]);
        }

        Assert.Equal(80, cable.Leaves().Count());
    }

    [Fact]
    public void Build_SameContactInBranches_KeepsSeparate()
    {
        var one = LadExpression.Series(Contact("A"), Contact("B"));
        var other = LadExpression.Series(Contact("A"), Contact("C"));

        Assert.Equal(4, LadExpression.Junction([one, other]).Leaves().Count());
    }
    private static ContactExpression C(string name, bool negated = false) =>
        new(new Operand(OperandScope.Global, [name], "\"" + name + "\""), negated);

    private static string Names(LadLayout layout) =>
        string.Join(" ", layout.Cells.Select(c => $"{c.Leaf}@{c.Column},{c.Row}"));

    [Fact]
    public void Build_Series_OneRow()
    {
        var layout = LadLayout.Build(Series(C("A"), C("B"), C("C")));
        Assert.Equal((3, 1), (layout.Columns, layout.Rows));
        Assert.Equal("\"A\"@0,0 \"B\"@1,0 \"C\"@2,0", Names(layout));
        Assert.Empty(layout.Wires);
    }

    [Fact]
    public void Build_Parallel_StacksAndJoins()
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
    public void Build_NestedParallel_CountsAllRows()
    {
        var layout = LadLayout.Build(Parallel(Series(C("A"), Parallel(C("B"), C("C"))), C("D")));
        Assert.Equal((2, 3), (layout.Columns, layout.Rows));
        Assert.Contains(layout.Cells, c => c is { Row: 2, Leaf: ContactExpression { Operand.Components: ["D"] } });
    }

    [Fact]
    public void Build_NotOfContact_NegatedContact()
    {
        var layout = LadLayout.Build(Not(C("A")));
        Assert.Equal(new ContactExpression(C("A").Operand, true), Assert.Single(layout.Cells).Leaf);
    }

    [Fact]
    public void Build_NotOfSeveral_DrawnAfter()
    {
        var layout = LadLayout.Build(Not(Series(C("A"), C("B"))));
        Assert.Equal(3, layout.Columns);
        Assert.Equal("NOT", layout.Cells[^1].Leaf.ToString());
    }

    [Fact]
    public void Build_AlwaysTrue_NoInstructions()
    {
        var layout = LadLayout.Build(Rail);
        Assert.Equal(0, layout.Columns);
        Assert.Empty(layout.Cells);
    }

    [Fact]
    public void ForStep_Step_OneRungPerSectionAndJump()
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
