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

public class SicarReaderTests
{
    // Red con contactos en serie que alimentan cada bobina #Sequence.<miembro> indicada.
    private static Net StepNetwork(string label, string title, params (string Member, string[] Signals)[] coils) =>
        StepNetwork(label, title, null, coils);

    private static Net StepNetwork(string label, string title, (int Target, string? Condition)? jump,
        params (string Member, string[] Signals)[] coils)
    {
        var n = new FlgNetBuilder().Label(label);
        foreach (var (member, signals) in coils)
        {
            var previousFile = (UId: 0, Pin: "");
            foreach (var signal in signals)
            {
                var operand = signal == "#motionButton" ? n.Local("Sequence", "motionButton") : n.Global(signal);
                var contact = n.Part("Contact");
                n.Operand(operand, contact, "operand");
                if (previousFile.UId == 0)
                {
                    n.Rail((contact, "in"));
                }
                else
                {
                    n.Wire(previousFile, (contact, "in"));
                }
                previousFile = (contact, "out");
            }
            var target = n.Local("Sequence", member);
            var coil = n.Part("Coil");
            n.Wire(previousFile, (coil, "in")).Operand(target, coil, "operand");
        }
        if (jump is { } s)
        {
            var step = n.Literal("USInt", s.Target.ToString());
            var target = n.Local("Sequence", "stepNplus1");
            var move = n.Part("Move");
            if (s.Condition == null)
            {
                n.Rail((move, "en"));
            }
            else
            {
                var operand = n.Global(s.Condition);
                var contact = n.Part("Contact");
                n.Rail((contact, "in")).Operand(operand, contact, "operand").Wire((contact, "out"), (move, "en"));
            }
            n.Operand(step, move, "in").ToOperand(move, "out1", target);
        }
        return LadNet(n, label + " " + title, $"Title_espanol {title}\nTitle_english {title} (en)");
    }

    private static PlcBlock SequenceBlock() => BlockReader.Read(Block("FB", 1007, "Est-Mesa_FB",
        Stl("Branch distributor"),
        StepNetwork("PERM", "Permanent step", ("ilockNoSync", ["Est-Fusible"])),
        StepNetwork("S001", "Espera pieza", (5, null), ("ilockAuto", ["Est-Arriba"]), ("transAuto", ["Est-Pieza"])),
        StepNetwork("S005", "Bajar mesa", (10, "Est-SinPieza"), ("ilockAuto", ["Est-Pieza", "Est-Puerta"]), ("transAuto", ["Est-Abajo"])),
        StepNetwork("S010", "Subir mesa", ("ilockAuto", ["Est-Abajo"]), ("transAuto", ["Est-Arriba"])),
        StepNetwork("S100", "Manual: bajar", ("ilockManual", ["#motionButton", "Est-Puerta"]), ("limitManual", ["Est-Abajo"]))));

    [Fact]
    public void IsSequenceBlock_SequenceOrMessages_Distinguishes()
    {
        var block = SequenceBlock();
        Assert.True(SicarReader.IsSequenceBlock(block));
        Assert.False(SicarReader.IsMessageBlock(block));
    }

    [Fact]
    public void ReadSequence_Block_ReadsStepsAndPerm()
    {
        var sequence = SicarReader.ReadSequence(SequenceBlock(), "en-US");
        Assert.Equal(7, sequence.Sequence);
        Assert.Equal([1, 5, 10, 100], sequence.Steps.Select(s => s.Number));
        Assert.NotNull(sequence.Permanent);
        Assert.True(sequence.Permanent!.IsPermanent);
        Assert.Equal("Bajar mesa", sequence.Step(5)!.Title("espanol"));
        Assert.Equal("Bajar mesa (en)", sequence.Step(5)!.Title("english"));
        Assert.Equal("Bajar mesa", sequence.Step(5)!.Title("deutsch"));
    }

    [Fact]
    public void ReadSequence_Sections_KeepLogic()
    {
        var step = SicarReader.ReadSequence(SequenceBlock()).Step(5)!;
        Assert.Equal("\"Est-Pieza\" Y \"Est-Puerta\"", step.Sections[SectionKind.IlockAuto].ToString());
        Assert.Equal("\"Est-Abajo\"", step.Sections[SectionKind.TransAuto].ToString());
        Assert.False(step.Sections.ContainsKey(SectionKind.IlockManual));
        Assert.False(step.IsManual);

        var permanent = SicarReader.ReadSequence(SequenceBlock()).Permanent!;
        Assert.Equal("\"Est-Fusible\"", permanent.Sections[SectionKind.IlockNoSync].ToString());
    }

    [Fact]
    public void ReadSequence_OnlyManualSections_IsManual()
    {
        var step = SicarReader.ReadSequence(SequenceBlock()).Step(100)!;
        Assert.True(step.IsManual);
        Assert.Equal("#Sequence.motionButton Y \"Est-Puerta\"", step.Sections[SectionKind.IlockManual].ToString());
    }

    [Fact]
    public void ReadSequence_Jumps_ReadsTargets()
    {
        var sequence = SicarReader.ReadSequence(SequenceBlock());

        // Salto incondicional: solo va a ese paso.
        var jump = Assert.Single(sequence.Step(1)!.Jumps);
        Assert.Equal(5, jump.Target);
        Assert.Equal([5], sequence.NextSteps(1));

        // Salto con condición: ese paso o el siguiente de la lista.
        Assert.Equal("\"Est-SinPieza\"", sequence.Step(5)!.Jumps[0].Condition.ToString());
        Assert.Equal([10], sequence.NextSteps(5));

        // Sin salto: el siguiente; el último automático vuelve al primero (los manuales no cuentan).
        Assert.Equal([1], sequence.NextSteps(10));
    }

    [Fact]
    public void DefaultValue_UnprogrammedSection_ReturnsDefault()
    {
        Assert.False(Sections.DefaultValue(SectionKind.IlockAuto));
        Assert.False(Sections.DefaultValue(SectionKind.TransAuto));
        Assert.True(Sections.DefaultValue(SectionKind.IlockNoSync));
        Assert.True(Sections.DefaultValue(SectionKind.SetError));
        Assert.Equal(SectionKind.IlockNoSync, Sections.FromMember("ilocklNoSync"));
    }

    private static Net MessageNetwork(int number, string text, string? category, params string[] signals)
    {
        var n = new FlgNetBuilder();
        var previousFile = (UId: 0, Pin: "");
        foreach (var signal in signals)
        {
            var operand = n.Global(signal);
            var contact = n.Negated("Contact");
            n.Operand(operand, contact, "operand");
            if (previousFile.UId == 0)
            {
                n.Rail((contact, "in"));
            }
            else
            {
                n.Wire(previousFile, (contact, "in"));
            }
            previousFile = (contact, "out");
        }
        var target = n.Local("Message", "no_" + number);
        var coil = n.Part("Coil");
        n.Wire(previousFile, (coil, "in")).Operand(target, coil, "operand");
        var comment = $"Title_espanol {text}" + (category == null ? "" : "\n" + category);
        return LadNet(n, text, comment);
    }

    [Fact]
    public void ReadMessages_Block_ReadsNumberTextAndCategory()
    {
        var block = BlockReader.Read(Block("FB", 1002, "Zona1_Mensajes_FB",
            MessageNetwork(1, "Seta de emergencia pulsada", "Category_1", "Zona-SetaOK"),
            MessageNetwork(2, "Puerta abierta", "Category_7", "Zona-PuertaCerrada"),
            MessageNetwork(3, "Mantenimiento pendiente", "Category_8", "Zona-MantOK"),
            MessageNetwork(4, "Sin categoría", null, "Zona-Otro"),
            MessageNetwork(5, "Categoría no válida", "Category_3", "Zona-Otro2")));

        Assert.True(SicarReader.IsMessageBlock(block));
        Assert.False(SicarReader.IsSequenceBlock(block));

        var messages = SicarReader.ReadMessages(block).Messages;
        Assert.Equal([1, 2, 3, 4, 5], messages.Select(m => m.Number));
        Assert.Equal([1, 7, 8, 1, 1], messages.Select(m => m.Category));
        Assert.Equal("Puerta abierta", messages[1].Title("espanol"));
        Assert.Equal("NO \"Zona-PuertaCerrada\"", messages[1].Condition.ToString());
    }

    [Fact]
    public void Classify_SignalOnlyFb_IsCrossReference()
    {
        var n = new FlgNetBuilder();
        var a = n.Global("Est-PP01");
        var target = n.Global("Est-PiezaOK");
        var contact = n.Part("Contact");
        var coil = n.Part("Coil");
        n.Rail((contact, "in")).Operand(a, contact, "operand").Wire((contact, "out"), (coil, "in")).Operand(target, coil, "operand");
        var block = BlockReader.Read(Block("FB", 1003, "Zona1_Resumen_FB", LadNet(n, "Presencia pieza")));

        Assert.True(SicarReader.IsCrossReferenceBlock(block));
        Assert.False(SicarReader.IsCrossReferenceBlock(SequenceBlock()));
    }

    [Fact]
    public void Classify_OutOfRangeFb_IsNeither()
    {
        var block = BlockReader.Read(Block("FB", 2000, "Otro_FB",
            StepNetwork("S001", "Paso", ("ilockAuto", ["Est-B1"]))));
        Assert.False(SicarReader.IsSequenceBlock(block));
        Assert.False(SicarReader.IsMessageBlock(block));
    }
}
