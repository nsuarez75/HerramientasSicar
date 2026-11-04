#nullable enable
using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using HerramientasSICAR.Nucleo.Lad;
using HerramientasSICAR.Nucleo.Symbols;

namespace HerramientasSICAR.Tests.Symbols;

public class SymbolTests
{
    private static string Comment(string text) => $"""
        <ObjectList><MultilingualText CompositionName="Comment"><ObjectList>
          <MultilingualTextItem CompositionName="Items"><AttributeList><Culture>en-US</Culture><Text>{text}</Text></AttributeList></MultilingualTextItem>
        </ObjectList></MultilingualText></ObjectList>
        """;

    private static XDocument Tabla() => XDocument.Parse($"""
        <Document>
          <SW.Tags.PlcTagTable ID="0">
            <AttributeList><Name>Estacion</Name></AttributeList>
            <ObjectList>
              <SW.Tags.PlcTag ID="1" CompositionName="Tags">
                <AttributeList><DataTypeName>Bool</DataTypeName><LogicalAddress>%M680.2</LogicalAddress><Name>Est-Pieza</Name></AttributeList>
                {Comment("Pieza presente")}
              </SW.Tags.PlcTag>
              <SW.Tags.PlcTag ID="2" CompositionName="Tags">
                <AttributeList><DataTypeName>"typeRobotIn"</DataTypeName><LogicalAddress>%I100.0</LogicalAddress><Name>Rob-IN</Name></AttributeList>
                {Comment("Entradas del robot")}
              </SW.Tags.PlcTag>
              <SW.Tags.PlcTag ID="3" CompositionName="Tags">
                <AttributeList><DataTypeName>Bool</DataTypeName><LogicalAddress>%Q4.0</LogicalAddress><Name>Est-YA</Name></AttributeList>
              </SW.Tags.PlcTag>
              <SW.Tags.PlcUserConstant ID="4" CompositionName="UserConstants">
                <AttributeList><DataTypeName>Int</DataTypeName><Name>NIVEL_MAX</Name><Value>250</Value></AttributeList>
              </SW.Tags.PlcUserConstant>
            </ObjectList>
          </SW.Tags.PlcTagTable>
        </Document>
        """);

    // Bool, Bool, Int (a byte par), Bool, Byte, DInt: offsets 0, 1, 16, 32, 40, 48.
    private static XDocument Udt() => XDocument.Parse("""
        <Document>
          <SW.Types.PlcStruct ID="0">
            <AttributeList>
              <Interface><Sections xmlns="http://www.siemens.com/automation/Openness/SW/Interface/v5">
                <Section Name="None">
                  <Member Name="soAuto" Datatype="Bool"><Comment><MultiLanguageText Lang="en-US">Robot en automático</MultiLanguageText></Comment></Member>
                  <Member Name="soError" Datatype="Bool" />
                  <Member Name="Code" Datatype="Int" />
                  <Member Name="soMot" Datatype="Bool" />
                  <Member Name="Prog" Datatype="Byte" />
                  <Member Name="Count" Datatype="DInt" />
                </Section>
              </Sections></Interface>
              <Name>typeRobotIn</Name>
            </AttributeList>
          </SW.Types.PlcStruct>
        </Document>
        """);

    private static SymbolTable Table()
    {
        var (tags, constants) = SymbolReader.ReadTagTable(Tabla());
        return new SymbolTable(tags, constants, [SymbolReader.ReadType(Udt())]);
    }

    private static Operand G(params string[] components) =>
        new(OperandScope.Global, components, string.Join(".", components.Select(c => "\"" + c + "\"")));

    [Fact]
    public void Lee_las_variables_y_constantes_de_una_tabla()
    {
        var (tags, constants) = SymbolReader.ReadTagTable(Tabla());
        Assert.Equal(["Est-Pieza", "Rob-IN", "Est-YA"], tags.Select(t => t.Name));
        Assert.Equal("typeRobotIn", tags[1].DataType);
        Assert.Equal("%M680.2", tags[0].Address);
        Assert.Equal("Pieza presente", tags[0].Comment.Get("en-US"));
        Assert.Equal("250", Assert.Single(constants).Value);
    }

    [Fact]
    public void Da_comentario_y_direccion_de_una_marca()
    {
        var info = Table().Find(G("Est-Pieza"))!;
        Assert.Equal("%M680.2", info.Address);
        Assert.Equal("Pieza presente", info.Comment.Get("en-US"));
    }

    [Fact]
    public void Las_salidas_no_guardan_direccion()
    {
        var info = Table().Find(G("Est-YA"))!;
        Assert.Null(info.Address);
    }

    [Theory]
    [InlineData("soAuto", "%I100.0")]
    [InlineData("soError", "%I100.1")]
    [InlineData("Code", "%I102.0")]
    [InlineData("soMot", "%I104.0")]
    [InlineData("Prog", "%I105.0")]
    [InlineData("Count", "%I106.0")]
    public void Calcula_la_direccion_de_cada_miembro_de_una_variable_UDT(string member, string address)
    {
        Assert.Equal(address, Table().Find(G("Rob-IN", member))!.Address);
    }

    [Fact]
    public void Los_miembros_de_una_variable_UDT_traen_el_comentario_del_UDT()
    {
        var info = Table().Find(G("Rob-IN", "soAuto"))!;
        Assert.Equal("Rob-IN.soAuto", info.Name);
        Assert.Equal("Robot en automático", info.Comment.Get("en-US"));
    }

    [Fact]
    public void Las_constantes_dan_su_valor_a_los_comparadores()
    {
        var constant = new Operand(OperandScope.GlobalConstant, ["NIVEL_MAX"], "\"NIVEL_MAX\"");
        Assert.Equal(250L, Table().ConstantValue(constant));
    }

    [Theory]
    [InlineData("Bool", 1)]
    [InlineData("Int", 16)]
    [InlineData("Real", 32)]
    [InlineData("String[10]", 96)]
    [InlineData("Array[1..10] of Bool", 16)]
    [InlineData("Array[0..2] of Int", 48)]
    public void Tamano_de_los_tipos_con_acceso_estandar(string type, int bits)
    {
        Assert.Equal(bits, MemoryLayout.SizeOf(type, new Dictionary<string, PlcType>()));
    }
}
