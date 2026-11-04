#nullable enable
using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HerramientasSICAR.Nucleo.Lad;
using HerramientasSICAR.Nucleo.Sicar;
using HerramientasSICAR.Nucleo.SimaticMl;
using static HerramientasSICAR.Nucleo.SimaticMl.SimaticMlBuilder;

namespace HerramientasSICAR.Tests;

public class SicarReaderTests
{
    // Red con contactos en serie que alimentan cada bobina #Sequence.<miembro> indicada.
    private static Net Paso(string label, string titulo, params (string Miembro, string[] Senales)[] bobinas) =>
        Paso(label, titulo, null, bobinas);

    private static Net Paso(string label, string titulo, (int Destino, string? Condicion)? salto,
        params (string Miembro, string[] Senales)[] bobinas)
    {
        var n = new FlgNetBuilder().Label(label);
        foreach (var (miembro, senales) in bobinas)
        {
            var anterior = (UId: 0, Pin: "");
            foreach (var senal in senales)
            {
                var operando = senal == "#motionButton" ? n.Local("Sequence", "motionButton") : n.Global(senal);
                var contacto = n.Part("Contact");
                n.Operand(operando, contacto, "operand");
                if (anterior.UId == 0)
                {
                    n.Rail((contacto, "in"));
                }
                else
                {
                    n.Wire(anterior, (contacto, "in"));
                }
                anterior = (contacto, "out");
            }
            var destino = n.Local("Sequence", miembro);
            var bobina = n.Part("Coil");
            n.Wire(anterior, (bobina, "in")).Operand(destino, bobina, "operand");
        }
        if (salto is { } s)
        {
            var paso = n.Literal("USInt", s.Destino.ToString());
            var destino = n.Local("Sequence", "stepNplus1");
            var move = n.Part("Move");
            if (s.Condicion == null)
            {
                n.Rail((move, "en"));
            }
            else
            {
                var operando = n.Global(s.Condicion);
                var contacto = n.Part("Contact");
                n.Rail((contacto, "in")).Operand(operando, contacto, "operand").Wire((contacto, "out"), (move, "en"));
            }
            n.Operand(paso, move, "in").ToOperand(move, "out1", destino);
        }
        return LadNet(n, label + " " + titulo, $"Title_espanol {titulo}\nTitle_english {titulo} (en)");
    }

    private static PlcBlock Secuencia() => BlockReader.Read(Block("FB", 1007, "Est-Mesa_FB",
        Stl("Branch distributor"),
        Paso("PERM", "Permanent step", ("ilockNoSync", ["Est-Fusible"])),
        Paso("S001", "Espera pieza", (5, null), ("ilockAuto", ["Est-Arriba"]), ("transAuto", ["Est-Pieza"])),
        Paso("S005", "Bajar mesa", (10, "Est-SinPieza"), ("ilockAuto", ["Est-Pieza", "Est-Puerta"]), ("transAuto", ["Est-Abajo"])),
        Paso("S010", "Subir mesa", ("ilockAuto", ["Est-Abajo"]), ("transAuto", ["Est-Arriba"])),
        Paso("S100", "Manual: bajar", ("ilockManual", ["#motionButton", "Est-Puerta"]), ("limitManual", ["Est-Abajo"]))));

    [Fact]
    public void Reconoce_un_FB_de_secuencia_y_no_lo_confunde_con_uno_de_mensajes()
    {
        var block = Secuencia();
        Assert.True(SicarReader.IsSequenceBlock(block));
        Assert.False(SicarReader.IsMessageBlock(block));
    }

    [Fact]
    public void Lee_los_pasos_con_su_numero_nombre_y_PERM_aparte()
    {
        var sequence = SicarReader.ReadSequence(Secuencia(), "en-US");
        Assert.Equal(7, sequence.Sequence);
        Assert.Equal([1, 5, 10, 100], sequence.Steps.Select(s => s.Number));
        Assert.NotNull(sequence.Permanent);
        Assert.True(sequence.Permanent!.IsPermanent);
        Assert.Equal("Bajar mesa", sequence.Step(5)!.Title("espanol"));
        Assert.Equal("Bajar mesa (en)", sequence.Step(5)!.Title("english"));
        Assert.Equal("Bajar mesa", sequence.Step(5)!.Title("deutsch"));
    }

    [Fact]
    public void Cada_seccion_guarda_su_logica()
    {
        var step = SicarReader.ReadSequence(Secuencia()).Step(5)!;
        Assert.Equal("\"Est-Pieza\" Y \"Est-Puerta\"", step.Sections[SectionKind.IlockAuto].ToString());
        Assert.Equal("\"Est-Abajo\"", step.Sections[SectionKind.TransAuto].ToString());
        Assert.False(step.Sections.ContainsKey(SectionKind.IlockManual));
        Assert.False(step.IsManual);

        var permanent = SicarReader.ReadSequence(Secuencia()).Permanent!;
        Assert.Equal("\"Est-Fusible\"", permanent.Sections[SectionKind.IlockNoSync].ToString());
    }

    [Fact]
    public void Un_paso_solo_con_secciones_de_manual_es_manual()
    {
        var step = SicarReader.ReadSequence(Secuencia()).Step(100)!;
        Assert.True(step.IsManual);
        Assert.Equal("#Sequence.motionButton Y \"Est-Puerta\"", step.Sections[SectionKind.IlockManual].ToString());
    }

    [Fact]
    public void Los_saltos_y_el_paso_siguiente()
    {
        var sequence = SicarReader.ReadSequence(Secuencia());

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
    public void Las_secciones_no_programadas_toman_su_valor_por_defecto()
    {
        Assert.False(Sections.DefaultValue(SectionKind.IlockAuto));
        Assert.False(Sections.DefaultValue(SectionKind.TransAuto));
        Assert.True(Sections.DefaultValue(SectionKind.IlockNoSync));
        Assert.True(Sections.DefaultValue(SectionKind.SetError));
        Assert.Equal(SectionKind.IlockNoSync, Sections.FromMember("ilocklNoSync"));
    }

    private static Net Mensaje(int numero, string texto, string? categoria, params string[] senales)
    {
        var n = new FlgNetBuilder();
        var anterior = (UId: 0, Pin: "");
        foreach (var senal in senales)
        {
            var operando = n.Global(senal);
            var contacto = n.Negated("Contact");
            n.Operand(operando, contacto, "operand");
            if (anterior.UId == 0)
            {
                n.Rail((contacto, "in"));
            }
            else
            {
                n.Wire(anterior, (contacto, "in"));
            }
            anterior = (contacto, "out");
        }
        var destino = n.Local("Message", "no_" + numero);
        var bobina = n.Part("Coil");
        n.Wire(anterior, (bobina, "in")).Operand(destino, bobina, "operand");
        var comentario = $"Title_espanol {texto}" + (categoria == null ? "" : "\n" + categoria);
        return LadNet(n, texto, comentario);
    }

    [Fact]
    public void Lee_los_mensajes_con_su_numero_texto_y_categoria()
    {
        var block = BlockReader.Read(Block("FB", 1002, "Zona1_Mensajes_FB",
            Mensaje(1, "Seta de emergencia pulsada", "Category_1", "Zona-SetaOK"),
            Mensaje(2, "Puerta abierta", "Category_7", "Zona-PuertaCerrada"),
            Mensaje(3, "Mantenimiento pendiente", "Category_8", "Zona-MantOK"),
            Mensaje(4, "Sin categoría", null, "Zona-Otro"),
            Mensaje(5, "Categoría no válida", "Category_3", "Zona-Otro2")));

        Assert.True(SicarReader.IsMessageBlock(block));
        Assert.False(SicarReader.IsSequenceBlock(block));

        var messages = SicarReader.ReadMessages(block).Messages;
        Assert.Equal([1, 2, 3, 4, 5], messages.Select(m => m.Number));
        Assert.Equal([1, 7, 8, 1, 1], messages.Select(m => m.Category));
        Assert.Equal("Puerta abierta", messages[1].Title("espanol"));
        Assert.Equal("NO \"Zona-PuertaCerrada\"", messages[1].Condition.ToString());
    }

    [Fact]
    public void Un_FB_SICAR_que_solo_calcula_senales_es_de_referencias_cruzadas()
    {
        var n = new FlgNetBuilder();
        var a = n.Global("Est-PP01");
        var target = n.Global("Est-PiezaOK");
        var contact = n.Part("Contact");
        var coil = n.Part("Coil");
        n.Rail((contact, "in")).Operand(a, contact, "operand").Wire((contact, "out"), (coil, "in")).Operand(target, coil, "operand");
        var block = BlockReader.Read(Block("FB", 1003, "Zona1_Resumen_FB", LadNet(n, "Presencia pieza")));

        Assert.True(SicarReader.IsCrossReferenceBlock(block));
        Assert.False(SicarReader.IsCrossReferenceBlock(Secuencia()));
    }

    [Fact]
    public void Un_FB_fuera_del_rango_SICAR_no_es_secuencia_ni_mensajes()
    {
        var block = BlockReader.Read(Block("FB", 2000, "Otro_FB",
            Paso("S001", "Paso", ("ilockAuto", ["Est-B1"]))));
        Assert.False(SicarReader.IsSequenceBlock(block));
        Assert.False(SicarReader.IsMessageBlock(block));
    }
}
