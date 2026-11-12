#nullable enable
using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HerramientasSICAR.Core.Lad;
using HerramientasSICAR.Core.Programs;
using HerramientasSICAR.Core.Sicar;

namespace HerramientasSICAR.Tests;

public sealed class PlcProgramTests : IDisposable
{
    private readonly string _dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "herramientas-sicar-program-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Load_OmSignal_WrittenByFc995()
    {
        ProgramFixture.Write(_dir);
        var program = PlcProgram.Load(_dir, "en-US");

        var source = Assert.Single(program.WritersOf(new Operand(OperandScope.Global, ["OM1_RequestEmptyLine"], "\"OM1_RequestEmptyLine\"")));
        Assert.Equal("CallOpmodePanelDiagAddOn_FC", source.Write.Block.Name);
        Assert.Equal("OpmodeDiagAddOn_FC", source.Write.Assignment.Box);
        Assert.Equal("%MW20", source.Through!.Address);
    }

    [Fact]
    public void Load_OutsideOmWord_NotWrittenByFc995()
    {
        ProgramFixture.Write(_dir);
        var program = PlcProgram.Load(_dir, "en-US");

        Assert.Empty(program.WritersOf(new Operand(OperandScope.Global, ["OM2_Fuera"], "\"OM2_Fuera\"")));
    }

    [Fact]
    public void Load_ExportedFolder_ReadsAll()
    {
        ProgramFixture.Write(_dir);

        var program = PlcProgram.Load(_dir, "en-US");

        Assert.Equal(5, program.Blocks.Count);
        var sequence = Assert.Single(program.Sequences);
        Assert.Equal(5, sequence.Sequence);
        Assert.NotNull(sequence.Permanent);
        Assert.Equal([10, 11], sequence.Steps.Select(s => s.Number));
        Assert.Equal(1, Assert.Single(Assert.Single(program.MessageBlocks).Messages).Number);
        Assert.Equal(1, program.OperationModeOf(5));
        Assert.Equal(["english", "espanol"], program.TextLanguages);
        Assert.Empty(program.Warnings);
        var signal = new Operand(OperandScope.Global, ["Rob-PrioridadA"], "\"Rob-PrioridadA\"");
        Assert.Equal("%M10.0", program.Symbols.Find(signal)!.Address);
    }

    [Fact]
    public void SyncChecker_UserDecisions_AppliesThem()
    {
        ProgramFixture.Write(_dir);
        var program = PlcProgram.Load(_dir, "en-US");
        var sequence = program.Sequences[0];

        Assert.Single(program.SyncChecker([]).Check(sequence));
        var group = new ExclusiveGroup(["\"Rob-PrioridadA\"", "\"Rob-PrioridadB\""]);
        Assert.Empty(program.SyncChecker([group]).Check(sequence));
    }

    [Fact]
    public void Load_WrongEditingLanguage_Warns()
    {
        ProgramFixture.Write(_dir);
        var program = PlcProgram.Load(_dir, "es-ES");
        Assert.Contains(program.Warnings, w => w.Contains("es-ES"));
        Assert.Empty(program.TextLanguages);
    }

    [Fact]
    public void Load_NoOperationModes_Warns()
    {
        ProgramFixture.Write(_dir);
        File.Delete(Path.Combine(_dir, "Bloques", "CallOpmodePanelDiagAddOn_FC.xml"));
        var program = PlcProgram.Load(_dir, "en-US");
        Assert.Contains(program.Warnings, w => w.Contains("FC983"));
        Assert.Null(program.OperationModeOf(5));
    }

    [Fact]
    public void Load_DamagedFile_WarnsAndContinues()
    {
        ProgramFixture.Write(_dir);
        File.WriteAllText(Path.Combine(_dir, "Bloques", "Roto.xml"), "<Document>");
        var program = PlcProgram.Load(_dir, "en-US");
        Assert.Contains(program.Warnings, w => w.Contains("Roto.xml"));
        Assert.Single(program.Sequences);
    }

    [Fact]
    public void Load_NoBlocks_Throws()
    {
        Assert.Throws<InvalidDataException>(() => PlcProgram.Load(_dir, "en-US"));
    }
}
