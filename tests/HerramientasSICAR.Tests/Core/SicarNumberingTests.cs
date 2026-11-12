#nullable enable
using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HerramientasSICAR.Core.Sicar;

namespace HerramientasSICAR.Tests;

public class SicarNumberingTests
{
    [Theory]
    [InlineData(1, 1001)]
    [InlineData(7, 1007)]
    [InlineData(255, 1255)]
    public void BlockNumber_Sequence_Adds1000(int sequence, int block)
    {
        Assert.Equal(block, SicarNumbering.BlockNumber(sequence));
        Assert.Equal(sequence, SicarNumbering.SequenceFromBlock(block));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(256)]
    public void BlockNumber_OutOfRange_Throws(int sequence)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SicarNumbering.BlockNumber(sequence));
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(1256)]
    [InlineData(986)]
    public void SequenceFromBlock_OutOfRange_ReturnsNull(int block)
    {
        Assert.Null(SicarNumbering.SequenceFromBlock(block));
    }

    [Theory]
    [InlineData(1, "S001")]
    [InlineData(45, "S045")]
    [InlineData(228, "S228")]
    public void StepLabel_Step_ThreeDigits(int step, string label)
    {
        Assert.Equal(label, SicarNumbering.StepLabel(step));
        Assert.True(SicarNumbering.TryParseStepLabel(label, out var read));
        Assert.Equal(step, read);
    }

    [Theory]
    [InlineData("PERM")]
    [InlineData("S000")]
    [InlineData("S229")]
    [InlineData("S01")]
    [InlineData("S-01")]
    [InlineData("X001")]
    [InlineData(null)]
    public void TryParseStepLabel_NotStepLabel_ReturnsFalse(string? label)
    {
        Assert.False(SicarNumbering.TryParseStepLabel(label, out _));
    }

    [Fact]
    public void StepLabel_OutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SicarNumbering.StepLabel(229));
    }
}
