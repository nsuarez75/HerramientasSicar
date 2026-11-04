#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Interpreta una red en STL (StatementList) siguiendo el resultado lógico instrucción a instrucción, y da lo mismo
// que el lector de LAD: qué escribe y con qué condición. Y antes que O, como en el PLC: A a, A b, O c = (a Y b) O c.
// Lo que no se puede seguir sin ejecutar (distribuidores JL, bucles, saltos hacia atrás, punteros) corta la lectura
// de esa red: se guarda lo leído hasta ahí.
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HerramientasSICAR.Nucleo.Lad;

namespace HerramientasSICAR.Nucleo.SimaticMl;

internal sealed partial class StlNetworkParser
{
    // Resultado lógico en construcción: términos unidos con O, cada uno con sus condiciones en serie.
    private sealed class Logic
    {
        private readonly List<List<LadExpression>> _terms = [];
        private bool _open;
        private bool _newTerm;

        public LadExpression Value => _terms.Count == 0
            ? LadExpression.Rail
            : LadExpression.Parallel(_terms.Select(LadExpression.Series));

        public void Check(LadExpression condition, bool or)
        {
            if (!_open)
            {
                _terms.Clear();
                _open = true;
                _newTerm = false;
                _terms.Add([condition]);
            }
            else if (or || _newTerm)
            {
                _newTerm = false;
                _terms.Add([condition]);
            }
            else
            {
                _terms[^1].Add(condition);
            }
        }

        // O sin operando: lo siguiente empieza un término nuevo.
        public void StartTerm() => _newTerm = _open;

        // Sustituye el resultado por otro (SET, CLR, NOT, flancos…).
        public void Replace(LadExpression value, bool open)
        {
            _terms.Clear();
            _terms.Add([value]);
            _open = open;
        }

        // Una asignación o un salto cierra el resultado: la siguiente consulta empieza otro.
        public void Close() => _open = false;
    }

    private sealed record Bracket(Logic Outer, bool Or);

    private sealed record Guard(string? Label, LadExpression Condition);

    private readonly List<Assignment> _assignments = [];
    private readonly List<BlockCall> _calls = [];
    private readonly List<StlLine> _lines = [];
    private readonly List<Guard> _guards = [];
    private readonly Stack<Bracket> _brackets = new();
    private Logic _logic = new();
    private LadValue _accu1 = LadValue.Of(Operand.Unknown("?"));
    private LadValue _accu2 = LadValue.Of(Operand.Unknown("?"));

    public StlNetworkParser(XElement statementList)
    {
        var statements = statementList.Elements().Where(e => e.Name.LocalName == "StlStatement").ToList();
        var labels = statements.Select(LabelOf).ToList();
        foreach (var statement in statements)
        {
            AddLine(statement);
        }

        // Un paso en STL lleva su etiqueta en la primera instrucción. Varias etiquetas son los pasos de reserva.
        if (labels.Count(l => l != null) == 1 && labels[0] != null)
        {
            Label = labels[0];
        }
        Interpret(statements, labels);
    }

    public string? Label { get; }
    public IReadOnlyList<Assignment> Assignments => _assignments;
    public IReadOnlyList<BlockCall> Calls => _calls;
    public IReadOnlyList<StlLine> Lines => _lines;

    private void Interpret(List<XElement> statements, List<string?> labels)
    {
        string? skipTo = null;
        for (var i = 0; i < statements.Count; i++)
        {
            if (labels[i] is { } label)
            {
                _guards.RemoveAll(g => g.Label == label);
                if (skipTo == label)
                {
                    skipTo = null;
                }
            }
            if (skipTo != null)
            {
                continue;
            }

            var statement = statements[i];
            var token = statement.Child("StlToken")?.Attribute("Text")?.Value ?? "";
            var access = statement.Child("Access");
            var operand = access != null && access.Attribute("Scope")?.Value != "Call" ? OperandReader.Read(access) : null;

            switch (token)
            {
                case "A" or "AN" or "O" or "ON":
                    Check(token, operand);
                    break;
                case "X" or "XN" when operand != null:
                    var other = new ContactExpression(operand, token == "XN");
                    _logic.Replace(new XorExpression([_logic.Value, other]), open: true);
                    break;
                case "A_BRACK" or "AN_BRACK" or "O_BRACK" or "ON_BRACK":
                    _brackets.Push(new Bracket(_logic, token.StartsWith("O", StringComparison.Ordinal)));
                    _negatedBrackets.Push(token.EndsWith("N_BRACK", StringComparison.Ordinal));
                    _logic = new Logic();
                    break;
                case "BRACKET":
                    CloseBracket();
                    break;
                case "SET":
                    _logic.Replace(LadExpression.Rail, open: false);
                    break;
                case "CLR":
                    _logic.Replace(new Unconnected(), open: false);
                    break;
                case "NOT":
                    _logic.Replace(LadExpression.Not(_logic.Value), open: true);
                    break;
                case "Rise" or "Fall":
                    _logic.Replace(new EdgeBox(_logic.Value, token == "Rise"), open: true);
                    break;
                case "Assign" or "S" or "R" when operand != null:
                    var kind = token == "Assign" ? AssignmentKind.Coil : token == "S" ? AssignmentKind.Set : AssignmentKind.Reset;
                    Add(new Assignment(kind, operand, Guarded(_logic.Value)) { Conditional = _guards.Count > 0 });
                    _logic.Close();
                    break;
                case "L" when operand != null:
                    _accu2 = _accu1;
                    _accu1 = LadValue.Of(operand);
                    break;
                case "CAW":
                    _accu1 = _accu1 with { Swapped = !_accu1.Swapped };
                    break;
                case "T" when operand != null:
                    Add(new Assignment(AssignmentKind.Move, operand, Guarded(LadExpression.Rail)) { Value = _accu1, Conditional = _guards.Count > 0 });
                    break;
                case "JC" or "JCN" or "JU" when operand != null:
                    var target = labels.IndexOf(operand.Text, i + 1);
                    if (target < 0)
                    {
                        // Salto a otra red o hacia atrás: desde aquí no se puede seguir.
                        return;
                    }
                    if (token == "JU")
                    {
                        skipTo = operand.Text;
                    }
                    else
                    {
                        var jumps = _logic.Value;
                        _guards.Add(new Guard(operand.Text, token == "JC" ? LadExpression.Not(jumps) : jumps));
                        _logic.Close();
                    }
                    break;
                case "BEC":
                    _guards.Add(new Guard(null, LadExpression.Not(_logic.Value)));
                    _logic.Close();
                    break;
                case "BEU" or "BE" or "JL" or "LOOP":
                    return;
                case "CALL":
                    Call(access);
                    break;
                case "COMMENT" or "EMPTY_LINE" or "NOP_0" or "SAVE":
                    break;
                default:
                    var compare = Compare().Match(token);
                    if (compare.Success)
                    {
                        _logic.Check(new Comparison(OperatorOf(compare.Groups[1].Value), _accu2, _accu1, TypeOf(compare.Groups[2].Value)), or: false);
                    }
                    else if (token.Length > 0)
                    {
                        // Cálculo (ADD, MUL, RND…): lo que queda en el acumulador ya no es un operando conocido.
                        _accu1 = LadValue.Of(Operand.Unknown(token));
                    }
                    break;
            }
        }
    }

    private readonly Stack<bool> _negatedBrackets = new();

    private void Check(string token, Operand? operand)
    {
        if (operand == null)
        {
            // O suelto: separa términos.
            _logic.StartTerm();
            return;
        }
        _logic.Check(new ContactExpression(operand, token is "AN" or "ON"), or: token is "O" or "ON");
    }

    private void CloseBracket()
    {
        if (_brackets.Count == 0)
        {
            return;
        }
        var inner = _logic.Value;
        var bracket = _brackets.Pop();
        if (_negatedBrackets.Pop())
        {
            inner = LadExpression.Not(inner);
        }
        _logic = bracket.Outer;
        _logic.Check(inner, bracket.Or);
    }

    private LadExpression Guarded(LadExpression value) =>
        _guards.Count == 0 ? value : LadExpression.Series(_guards.Select(g => g.Condition).Append(value));

    private void Add(Assignment assignment) => _assignments.Add(assignment);

    private void Call(XElement? access)
    {
        var info = access?.Child("CallInfo");
        if (info == null)
        {
            return;
        }
        var name = (string?)info.Attribute("Name") ?? "Call";
        var inputs = new Dictionary<string, LadValue>();
        foreach (var parameter in info.Elements().Where(e => e.Name.LocalName == "Parameter"))
        {
            var argument = parameter.Child("Access");
            if (argument == null)
            {
                continue;
            }
            var operand = OperandReader.Read(argument);
            var pin = (string?)parameter.Attribute("Name") ?? "";
            if ((string?)parameter.Attribute("Section") is "Input" or "InOut")
            {
                inputs[pin.ToLowerInvariant()] = LadValue.Of(operand);
            }
            if ((string?)parameter.Attribute("Section") is "Output" or "InOut")
            {
                Add(new Assignment(AssignmentKind.BoxOutput, operand, Guarded(LadExpression.Rail))
                {
                    Value = LadValue.Of(Operand.Unknown(pin)),
                    Box = name,
                    Conditional = _guards.Count > 0,
                });
            }
        }
        _calls.Add(new BlockCall(name, (string?)info.Attribute("BlockType") ?? "") { Inputs = inputs, Enable = Guarded(LadExpression.Rail) });
    }

    private void AddLine(XElement statement)
    {
        var token = statement.Child("StlToken")?.Attribute("Text")?.Value ?? "";
        var label = LabelOf(statement);
        var comment = statement.Descendants().FirstOrDefault(e => e.Name.LocalName == "LineComment")?.Value.Trim();
        if (token is "EMPTY_LINE" or "" && label == null && comment == null)
        {
            return;
        }
        var access = statement.Child("Access");
        var operand = access != null && access.Attribute("Scope")?.Value != "Call" ? OperandReader.Read(access) : null;
        var callName = access?.Child("CallInfo")?.Attribute("Name")?.Value;
        var text = callName != null ? "CALL " + callName : Readable(token);
        _lines.Add(new StlLine(text, operand) { Label = label, Comment = string.IsNullOrEmpty(comment) ? null : comment });
    }

    private static string? LabelOf(XElement statement) =>
        statement.Child("LabelDeclaration")?.Child("Label")?.Attribute("Name")?.Value;

    // Como se ve en el editor de TIA.
    private static string Readable(string token) => token switch
    {
        "Assign" => "=",
        "A_BRACK" => "A(",
        "AN_BRACK" => "AN(",
        "O_BRACK" => "O(",
        "ON_BRACK" => "ON(",
        "BRACKET" => ")",
        "Rise" => "FP",
        "Fall" => "FN",
        "COMMENT" or "EMPTY_LINE" => "",
        "NOP_0" => "NOP 0",
        _ => Compare().Match(token) is { Success: true } m ? Symbol(m.Groups[1].Value) + m.Groups[2].Value : token,
    };

    private static string Symbol(string name) => Comparison.Symbol(OperatorOf(name));

    private static CompareOperator OperatorOf(string name) => name switch
    {
        "EQ" => CompareOperator.Equal,
        "NE" => CompareOperator.NotEqual,
        "GT" => CompareOperator.Greater,
        "GE" => CompareOperator.GreaterOrEqual,
        "LT" => CompareOperator.Less,
        _ => CompareOperator.LessOrEqual,
    };

    private static string TypeOf(string suffix) => suffix switch
    {
        "I" => "Int",
        "D" => "DInt",
        _ => "Real",
    };

    // .NET Framework no tiene GeneratedRegex: se crea la primera vez que se usa.
    private static Regex Compare() => _Compare ??= new Regex("^(EQ|NE|GT|GE|LT|LE)_(I|D|R)$");

    private static Regex? _Compare;
}
