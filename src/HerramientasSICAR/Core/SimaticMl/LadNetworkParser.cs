#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Interpreta una red LAD (FlgNet) de un bloque exportado. Las Parts se indexan por UId y la lógica se
// reconstruye desde la topología de los Wires, nunca por el orden de las Parts. Cada bobina o salida se
// resuelve hacia atrás hasta la barra de alimentación. Basado en el lector de Sincro Checker
// (HerramientasSICAR), ampliado con comparadores, cajas y operandos tipados.
using System.Xml.Linq;
using HerramientasSICAR.Core.Lad;

namespace HerramientasSICAR.Core.SimaticMl;

internal sealed class LadNetworkParser
{
    private sealed class Node
    {
        public required int UId { get; init; }
        // Access, Part o Call.
        public required string Element { get; init; }
        // Nombre de la instrucción en minúsculas (contact, coil, eq…) o del bloque llamado.
        public string Name { get; set; } = "";
        public Operand? Operand { get; set; }
        public Operand? Instance { get; set; }
        public string? TemplateType { get; set; }
        public string BlockType { get; set; } = "";
        public HashSet<string> Negated { get; } = new(StringComparer.OrdinalIgnoreCase);
        // Parámetros InOut de una llamada: se cablean como entradas, pero el bloque también los escribe.
        public HashSet<string> InOut { get; } = new(StringComparer.Ordinal);
    }

    private readonly record struct Pin(int UId, string Name);

    // De dónde llega lo que entra en un pin.
    private sealed class Source
    {
        public bool PowerRail { get; init; }
        public bool Open { get; init; }
        public int? Operand { get; init; }
        public Pin? Output { get; init; }
    }

    private readonly Dictionary<int, Node> _nodes = [];
    private readonly Dictionary<(int, string), Source> _inputs = [];
    private readonly List<(Pin Output, int Operand)> _outputsToOperands = [];
    private readonly Dictionary<(int, string), LadExpression> _cache = [];
    private readonly HashSet<(int, string)> _inProgress = [];

    public LadNetworkParser(XElement flgNet)
    {
        Label = flgNet.Child("Labels")?.Descendants().FirstOrDefault(e => e.Name.LocalName == "Label")
            ?.Attribute("Name")?.Value;

        foreach (var part in flgNet.Child("Parts")?.Elements() ?? [])
        {
            RegisterPart(part);
        }
        foreach (var wire in flgNet.Child("Wires")?.Elements().Where(e => e.Name.LocalName == "Wire") ?? [])
        {
            RegisterWire(wire);
        }
    }

    public string? Label { get; }

    private void RegisterPart(XElement part)
    {
        var uid = UIdOf(part);
        var node = new Node { UId = uid, Element = part.Name.LocalName };
        switch (node.Element)
        {
            case "Access":
                node.Operand = OperandReader.Read(part);
                break;
            case "Part":
                node.Name = ((string?)part.Attribute("Name") ?? "").ToLowerInvariant();
                node.TemplateType = part.Elements().FirstOrDefault(e => e.Name.LocalName == "TemplateValue")?.Value;
                var instance = part.Child("Instance");
                if (instance != null)
                {
                    node.Instance = OperandReader.Read(instance);
                }
                foreach (var negated in part.Elements().Where(e => e.Name.LocalName == "Negated"))
                {
                    node.Negated.Add((string?)negated.Attribute("Name") ?? "");
                }
                break;
            case "Call":
                var info = part.Child("CallInfo");
                node.Name = (string?)info?.Attribute("Name") ?? "Call";
                node.BlockType = (string?)info?.Attribute("BlockType") ?? "";
                var callInstance = info?.Child("Instance");
                if (callInstance != null)
                {
                    node.Instance = OperandReader.Read(callInstance);
                }
                foreach (var negated in part.Descendants().Where(e => e.Name.LocalName == "Negated"))
                {
                    node.Negated.Add((string?)negated.Attribute("Name") ?? "");
                }
                foreach (var parameter in info?.Elements().Where(e => e.Name.LocalName == "Parameter" && (string?)e.Attribute("Section") == "InOut") ?? [])
                {
                    node.InOut.Add(((string?)parameter.Attribute("Name") ?? "").ToLowerInvariant());
                }
                break;
            default:
                return;
        }
        _nodes[uid] = node;
    }

    private void RegisterWire(XElement wire)
    {
        var connections = wire.Elements().ToList();
        if (connections.Count == 0)
        {
            return;
        }

        Source source;
        IEnumerable<XElement> targets;
        if (connections.Any(c => c.Name.LocalName == "Powerrail"))
        {
            // Todo lo que cuelga de un cable con Powerrail se alimenta de la barra izquierda.
            source = new Source { PowerRail = true };
            targets = connections.Where(c => c.Name.LocalName != "Powerrail");
        }
        else
        {
            // La primera conexión es el origen y el resto son destinos.
            var first = connections[0];
            targets = connections.Skip(1);
            source = first.Name.LocalName switch
            {
                "IdentCon" => new Source { Operand = UIdOf(first) },
                "NameCon" => new Source { Output = new Pin(UIdOf(first), PinName(first)) },
                _ => new Source { Open = true },
            };
        }

        foreach (var target in targets)
        {
            switch (target.Name.LocalName)
            {
                case "NameCon":
                    _inputs[(UIdOf(target), PinName(target))] = source;
                    break;
                case "IdentCon" when source.Output.HasValue:
                    _outputsToOperands.Add((source.Output.Value, UIdOf(target)));
                    break;
            }
        }
    }

    public IEnumerable<Assignment> Assignments()
    {
        foreach (var node in _nodes.Values.Where(n => n.Element != "Access").OrderBy(n => n.UId))
        {
            var kind = node.Element == "Part" ? CoilKind(node.Name) : null;
            if (kind != null)
            {
                var target = OperandAt(node.UId, "operand");
                if (target != null)
                {
                    yield return new Assignment(kind.Value, target, Input(node.UId, "in"));
                }
                continue;
            }

            if (node.Name is "sr" or "rs")
            {
                var target = OperandAt(node.UId, "operand");
                if (target == null)
                {
                    continue;
                }
                var (set, reset) = node.Name == "sr" ? ("s", "r1") : ("s1", "r");
                yield return new Assignment(AssignmentKind.Set, target, Input(node.UId, set));
                yield return new Assignment(AssignmentKind.Reset, target, Input(node.UId, reset));
                continue;
            }

            if (node.Name is "jump" or "jumpnot")
            {
                var label = OperandAt(node.UId, "label") ?? Operand.Unknown("?");
                yield return new Assignment(node.Name == "jump" ? AssignmentKind.Jump : AssignmentKind.JumpNot,
                    label, Input(node.UId, "in"));
                continue;
            }

            if (node.Name == "returnvalue")
            {
                continue;
            }

            // Salidas de cajas y llamadas escritas en operandos (MOVE, ADD, TON.ET, salida de un FC…).
            var condition = _inputs.ContainsKey((node.UId, "en")) ? Input(node.UId, "en") : LadExpression.Rail;
            foreach (var pin in node.InOut)
            {
                if (OperandAt(node.UId, pin) is { } inOut)
                {
                    yield return new Assignment(AssignmentKind.BoxOutput, inOut, condition)
                    {
                        Value = LadValue.Of(Operand.Unknown(pin)),
                        Box = node.Name,
                    };
                }
            }
            var isMove = node.Name is "move" or "s_move";
            foreach (var (output, operandUid) in _outputsToOperands.Where(o => o.Output.UId == node.UId))
            {
                if (!_nodes.TryGetValue(operandUid, out var targetNode) || targetNode.Operand == null)
                {
                    continue;
                }
                yield return new Assignment(isMove ? AssignmentKind.Move : AssignmentKind.BoxOutput,
                    targetNode.Operand, condition)
                {
                    Value = isMove ? ValueAt(node.UId, "in") : LadValue.Of(Operand.Unknown(output.Name)),
                    Box = node.Element == "Call" ? node.Name : node.Name.ToUpperInvariant(),
                };
            }
        }
    }

    public IEnumerable<BlockCall> Calls() =>
        _nodes.Values.Where(n => n.Element == "Call").OrderBy(n => n.UId).Select(n => new BlockCall(n.Name, n.BlockType)
        {
            Instance = n.Instance,
            Enable = _inputs.ContainsKey((n.UId, "en")) ? Input(n.UId, "en") : LadExpression.Rail,
            Inputs = _inputs.Keys
                .Where(k => k.Item1 == n.UId && k.Item2 != "en")
                .ToDictionary(k => k.Item2, k => ValueAt(n.UId, k.Item2)),
        });

    private static AssignmentKind? CoilKind(string name) => name switch
    {
        "coil" => AssignmentKind.Coil,
        "scoil" => AssignmentKind.Set,
        "rcoil" => AssignmentKind.Reset,
        "pcoil" => AssignmentKind.RisingCoil,
        "ncoil" => AssignmentKind.FallingCoil,
        _ => null,
    };

    private Operand? OperandAt(int uid, string pin) =>
        _inputs.TryGetValue((uid, pin), out var source) && source.Operand is { } operandUid &&
        _nodes.TryGetValue(operandUid, out var node)
            ? node.Operand
            : null;

    // Lo que entra en un pin de valor: el operando si va cableado directamente, si no la expresión.
    private LadValue ValueAt(int uid, string pin)
    {
        var operand = OperandAt(uid, pin);
        return operand != null ? LadValue.Of(operand) : LadValue.Of(Input(uid, pin));
    }

    private LadExpression Input(int uid, string pin)
    {
        LadExpression expression = _inputs.TryGetValue((uid, pin), out var source)
            ? Evaluate(source)
            : new Unconnected();
        if (_nodes.TryGetValue(uid, out var node) && node.Negated.Contains(pin))
        {
            expression = LadExpression.Not(expression);
        }
        return expression;
    }

    private LadExpression Evaluate(Source source)
    {
        if (source.PowerRail)
        {
            return LadExpression.Rail;
        }
        if (source.Open)
        {
            return new Unconnected();
        }
        if (source.Operand is { } operandUid)
        {
            // Un operando booleano cableado directamente a un pin lógico se trata como contacto.
            var operand = _nodes.TryGetValue(operandUid, out var node) && node.Operand != null
                ? node.Operand
                : Operand.Unknown("?");
            return new ContactExpression(operand, false);
        }
        return Output(source.Output!.Value.UId, source.Output.Value.Name);
    }

    private LadExpression Output(int uid, string pin)
    {
        var key = (uid, pin);
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }
        if (!_inProgress.Add(key))
        {
            return new UnknownExpression("<bucle>");
        }
        var result = ComputeOutput(uid, pin);
        _inProgress.Remove(key);
        _cache[key] = result;
        return result;
    }

    private LadExpression ComputeOutput(int uid, string pin)
    {
        if (!_nodes.TryGetValue(uid, out var node))
        {
            return new UnknownExpression("?" + uid);
        }

        var operand = OperandAt(uid, "operand") ?? Operand.Unknown("?");
        switch (node.Name)
        {
            case "contact":
                return LadExpression.Series(Input(uid, "in"), new ContactExpression(operand, node.Negated.Contains("operand")));
            case "pcontact":
            case "ncontact":
                return LadExpression.Series(Input(uid, "in"), new EdgeContact(operand, node.Name == "pcontact"));
            case "pbox":
            case "nbox":
                return new EdgeBox(Input(uid, "in"), node.Name == "pbox");
            case "not":
                return LadExpression.Not(Input(uid, "in"));
            case "o":
                return LadExpression.Junction(NumberedInputs(uid, "in"));
            case "a":
                return LadExpression.Series(NumberedInputs(uid, "in"));
            case "x":
                return new XorExpression(NumberedInputs(uid, "in"));
            case "coil":
            case "scoil":
            case "rcoil":
            case "pcoil":
            case "ncoil":
                // Las bobinas en serie dejan pasar el resultado.
                return Input(uid, "in");
            case "eq":
            case "ne":
            case "gt":
            case "ge":
            case "lt":
            case "le":
                var comparison = new Comparison(CompareOperatorOf(node.Name), ValueAt(uid, "in1"), ValueAt(uid, "in2"),
                    node.TemplateType ?? "");
                return LadExpression.Series(Input(uid, "pre"), comparison);
        }

        if (pin == "eno")
        {
            return Input(uid, "en");
        }

        // Temporizadores, contadores, llamadas…: la salida es opaca y se guardan sus entradas.
        var inputs = _inputs.Keys
            .Where(k => k.Item1 == uid && k.Item2 != "en")
            .OrderBy(k => k.Item2, StringComparer.Ordinal)
            .ToDictionary(k => k.Item2, k => ValueAt(uid, k.Item2));
        return new BoxOutput(node.Element == "Call" ? node.Name : node.Name.ToUpperInvariant(), node.Instance, pin, inputs);
    }

    private static CompareOperator CompareOperatorOf(string name) => name switch
    {
        "eq" => CompareOperator.Equal,
        "ne" => CompareOperator.NotEqual,
        "gt" => CompareOperator.Greater,
        "ge" => CompareOperator.GreaterOrEqual,
        "lt" => CompareOperator.Less,
        _ => CompareOperator.LessOrEqual,
    };

    private List<LadExpression> NumberedInputs(int uid, string prefix) =>
        _inputs.Keys
            .Where(k => k.Item1 == uid && k.Item2.StartsWith(prefix, StringComparison.Ordinal) &&
                        int.TryParse(k.Item2.Substring(prefix.Length), out _))
            .OrderBy(k => int.Parse(k.Item2.Substring(prefix.Length)))
            .Select(k => Input(uid, k.Item2))
            .ToList();

    private static int UIdOf(XElement e) => int.Parse((string)e.Attribute("UId")!);

    private static string PinName(XElement e) => ((string?)e.Attribute("Name") ?? "").ToLowerInvariant();
}
