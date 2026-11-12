#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Construye bloques SimaticML inventados con la misma forma que los que exporta TIA V19: los usan los tests y el simulador.
using System.Text;
using System.Xml.Linq;

namespace HerramientasSICAR.Core.SimaticMl;

internal sealed class FlgNetBuilder
{
    private readonly StringBuilder _parts = new();
    private readonly StringBuilder _wires = new();
    private int _uid = 20;
    private string? _label;

    public FlgNetBuilder Label(string label)
    {
        _label = label;
        return this;
    }

    public int Global(params string[] components) => Access("GlobalVariable", Symbol(components));

    public int Local(params string[] components) => Access("LocalVariable", Symbol(components));

    public int Literal(string type, string value) =>
        Access("LiteralConstant", $"<Constant><ConstantType>{type}</ConstantType><ConstantValue>{value}</ConstantValue></Constant>");

    public int GlobalConstant(string name) => Access("GlobalConstant", $"<Constant Name=\"{name}\" />");

    public int Part(string name, string inner = "")
    {
        var uid = ++_uid;
        _parts.AppendLine($"<Part Name=\"{name}\" UId=\"{uid}\">{inner}</Part>");
        return uid;
    }

    public int Negated(string name, string pin = "operand") => Part(name, $"<Negated Name=\"{pin}\" />");

    public int Timer(string name, params string[] instance) =>
        Part(name, $"<Instance Scope=\"GlobalVariable\" UId=\"{++_uid}\">{Symbol(instance)}</Instance>");

    public int Call(string block, string blockType = "FC")
    {
        var uid = ++_uid;
        _parts.AppendLine($"<Call UId=\"{uid}\"><CallInfo Name=\"{block}\" BlockType=\"{blockType}\" /></Call>");
        return uid;
    }

    public int CallWithInOut(string block, string parameter)
    {
        var uid = ++_uid;
        _parts.AppendLine($"<Call UId=\"{uid}\"><CallInfo Name=\"{block}\" BlockType=\"FB\"><Parameter Name=\"{parameter}\" Section=\"InOut\" Type=\"DWord\" /></CallInfo></Call>");
        return uid;
    }

    // Cable desde la barra de alimentación a uno o varios pines.
    public FlgNetBuilder Rail(params (int UId, string Pin)[] targets)
    {
        _wires.AppendLine($"<Wire UId=\"{++_uid}\"><Powerrail />{string.Concat(targets.Select(Name))}</Wire>");
        return this;
    }

    // Cable de la salida de una instrucción a uno o varios pines.
    public FlgNetBuilder Wire((int UId, string Pin) from, params (int UId, string Pin)[] targets)
    {
        _wires.AppendLine($"<Wire UId=\"{++_uid}\">{Name(from)}{string.Concat(targets.Select(Name))}</Wire>");
        return this;
    }

    // Cable de un operando a un pin.
    public FlgNetBuilder Operand(int operand, int uid, string pin)
    {
        _wires.AppendLine($"<Wire UId=\"{++_uid}\"><IdentCon UId=\"{operand}\" />{Name((uid, pin))}</Wire>");
        return this;
    }

    // Cable de una salida de caja a un operando.
    public FlgNetBuilder ToOperand(int uid, string pin, int operand)
    {
        _wires.AppendLine($"<Wire UId=\"{++_uid}\">{Name((uid, pin))}<IdentCon UId=\"{operand}\" /></Wire>");
        return this;
    }

    public string Build()
    {
        var labels = _label == null ? "" : $"<Labels><LabelDeclaration UId=\"1\"><Label Name=\"{_label}\" /></LabelDeclaration></Labels>";
        return "<FlgNet xmlns=\"http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5\">" +
               labels + "<Parts>" + _parts + "</Parts><Wires>" + _wires + "</Wires></FlgNet>";
    }

    private int Access(string scope, string inner)
    {
        var uid = ++_uid;
        _parts.AppendLine($"<Access Scope=\"{scope}\" UId=\"{uid}\">{inner}</Access>");
        return uid;
    }

    private static string Symbol(string[] components) =>
        "<Symbol>" + string.Concat(components.Select(c => $"<Component Name=\"{c}\" />")) + "</Symbol>";

    private static string Name((int UId, string Pin) pin) => $"<NameCon UId=\"{pin.UId}\" Name=\"{pin.Pin}\" />";
}

internal static class SimaticMlBuilder
{
    public sealed record Net(string Language, string Source, string Title = "", string Comment = "");

    public static Net LadNet(FlgNetBuilder net, string title = "", string comment = "") => new("LAD", net.Build(), title, comment);

    public static Net Stl(string title = "", string comment = "") => new("STL", "<StatementList />", title, comment);

    public static XDocument Block(string kind, int number, string name, params Net[] networks)
    {
        var units = new StringBuilder();
        var id = 10;
        foreach (var net in networks)
        {
            units.Append($"""
                <SW.Blocks.CompileUnit ID="{++id}" CompositionName="CompileUnits">
                  <AttributeList>
                    <NetworkSource>{net.Source}</NetworkSource>
                    <ProgrammingLanguage>{net.Language}</ProgrammingLanguage>
                  </AttributeList>
                  <ObjectList>
                    {Text("Comment", net.Comment)}
                    {Text("Title", net.Title)}
                  </ObjectList>
                </SW.Blocks.CompileUnit>
                """);
        }
        return XDocument.Parse($"""
            <Document>
              <Engineering version="V19" />
              <SW.Blocks.{kind} ID="0">
                <AttributeList>
                  <Name>{name}</Name>
                  <Number>{number}</Number>
                  <ProgrammingLanguage>LAD</ProgrammingLanguage>
                </AttributeList>
                <ObjectList>{units}</ObjectList>
              </SW.Blocks.{kind}>
            </Document>
            """);
    }

    // Igual que TIA: el texto va en en-US y las otras culturas quedan vacías.
    private static string Text(string composition, string text) => $"""
        <MultilingualText CompositionName="{composition}">
          <ObjectList>
            <MultilingualTextItem CompositionName="Items"><AttributeList><Culture>es-ES</Culture><Text /></AttributeList></MultilingualTextItem>
            <MultilingualTextItem CompositionName="Items"><AttributeList><Culture>en-US</Culture><Text>{System.Security.SecurityElement.Escape(text)}</Text></AttributeList></MultilingualTextItem>
          </ObjectList>
        </MultilingualText>
        """;
}
