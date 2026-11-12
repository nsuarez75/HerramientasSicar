#nullable enable
using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Carpeta de exportación inventada, con la misma forma que deja el exportador: OM1 (bloques 2-10) con una
// secuencia de robot cuyos pasos S010 y S011 solo se distinguen por dos prioridades, y un bloque de mensajes.
using System.Xml.Linq;
using HerramientasSICAR.Core.SimaticMl;
using static HerramientasSICAR.Core.SimaticMl.SimaticMlBuilder;

namespace HerramientasSICAR.Tests;

internal static class ProgramFixture
{
    public static void Write(string folder)
    {
        foreach (var sub in new[] { "Bloques", "Variables", "Tipos" })
        {
            Directory.CreateDirectory(Path.Combine(folder, sub));
        }

        Save(folder, "Bloques", "Rob_FB", Block("FB", 1005, "Rob_FB",
            StepNetwork("PERM", "Permanente", ["Rob-Seguridad"], null),
            StepNetwork("S010", "Descargar A", ["Rob-Code=210", "Rob-PrioridadA"], "Rob-Code=220"),
            StepNetwork("S011", "Descargar B", ["Rob-Code=210", "Rob-PrioridadB"], "Rob-Code=220")));
        Save(folder, "Bloques", "Rob_IDB", InstanceDb(1005, "Rob_IDB", "Rob_FB"));
        Save(folder, "Bloques", "Zona_Mensajes_IDB", InstanceDb(1002, "Zona_Mensajes_IDB", "Zona_Mensajes_FB"));
        Save(folder, "Bloques", "Zona_Mensajes_FB", Block("FB", 1002, "Zona_Mensajes_FB", MessageNetwork()));
        Save(folder, "Bloques", "CallOpmodePanelDiagAddOn_FC", Block("FC", 983, "CallOpmodePanelDiagAddOn_FC", OmCall()));
        File.WriteAllText(Path.Combine(folder, "Variables", "Rob.xml"), """
            <Document>
              <SW.Tags.PlcTagTable ID="0">
                <AttributeList><Name>Rob</Name></AttributeList>
                <ObjectList>
                  <SW.Tags.PlcTag ID="1" CompositionName="Tags">
                    <AttributeList><DataTypeName>Bool</DataTypeName><LogicalAddress>%M10.0</LogicalAddress><Name>Rob-PrioridadA</Name></AttributeList>
                    <ObjectList><MultilingualText CompositionName="Comment"><ObjectList>
                      <MultilingualTextItem CompositionName="Items"><AttributeList><Culture>en-US</Culture><Text>Prioridad de la estación A</Text></AttributeList></MultilingualTextItem>
                    </ObjectList></MultilingualText></ObjectList>
                  </SW.Tags.PlcTag>
                  <SW.Tags.PlcTag ID="2" CompositionName="Tags">
                    <AttributeList><DataTypeName>Bool</DataTypeName><LogicalAddress>%M21.3</LogicalAddress><Name>OM1_RequestEmptyLine</Name></AttributeList>
                  </SW.Tags.PlcTag>
                  <SW.Tags.PlcTag ID="3" CompositionName="Tags">
                    <AttributeList><DataTypeName>Bool</DataTypeName><LogicalAddress>%M22.0</LogicalAddress><Name>OM2_Fuera</Name></AttributeList>
                  </SW.Tags.PlcTag>
                </ObjectList>
              </SW.Tags.PlcTagTable>
            </Document>
            """);
    }

    private static XDocument InstanceDb(int number, string name, string block) => XDocument.Parse($"""
        <Document>
          <SW.Blocks.InstanceDB ID="0">
            <AttributeList><InstanceOfName>{block}</InstanceOfName><Name>{name}</Name><Number>{number}</Number><ProgrammingLanguage>DB</ProgrammingLanguage></AttributeList>
          </SW.Blocks.InstanceDB>
        </Document>
        """);

    internal static void Save(string folder, string sub, string name, XDocument document) =>
        document.Save(Path.Combine(folder, sub, name + ".xml"));

    internal static Net StepNetwork(string label, string title, string[] ilock, string? trans)
    {
        var n = new FlgNetBuilder().Label(label);
        Coil(n, ilock, "ilockAuto");
        if (trans != null)
        {
            Coil(n, [trans], "transAuto");
        }
        return LadNet(n, label + " " + title, $"Title_espanol {title}\nTitle_english {title} (en)");
    }

    private static void Coil(FlgNetBuilder n, string[] signals, string member)
    {
        var previous = (UId: 0, Pin: "");
        foreach (var signal in signals)
        {
            var operand = n.Global(signal);
            var contact = n.Part("Contact");
            n.Operand(operand, contact, "operand");
            if (previous.UId == 0)
            {
                n.Rail((contact, "in"));
            }
            else
            {
                n.Wire(previous, (contact, "in"));
            }
            previous = (contact, "out");
        }
        var target = n.Local("Sequence", member);
        var coil = n.Part("Coil");
        n.Wire(previous, (coil, "in")).Operand(target, coil, "operand");
    }

    private static Net MessageNetwork()
    {
        var n = new FlgNetBuilder();
        var operand = n.Global("Zona-SetaOK");
        var target = n.Local("Message", "no_1");
        var contact = n.Negated("Contact");
        var coil = n.Part("Coil");
        n.Rail((contact, "in")).Operand(operand, contact, "operand").Wire((contact, "out"), (coil, "in")).Operand(target, coil, "operand");
        return LadNet(n, "Seta", "Title_espanol Seta pulsada\nCategory_1");
    }

    private static Net OmCall()
    {
        var n = new FlgNetBuilder();
        var number = n.Literal("Int", "1");
        var from = n.Literal("Int", "2");
        var to = n.Literal("Int", "10");
        var word = n.Literal("Int", "20");
        var call = n.Call("OpmodeDiagAddOn_FC");
        n.Rail((call, "en")).Operand(number, call, "noOPmode").Operand(from, call, "seqMBFrom").Operand(to, call, "seqMBTo").Operand(word, call, "mwOPmode");
        return LadNet(n, "OM1");
    }
}
