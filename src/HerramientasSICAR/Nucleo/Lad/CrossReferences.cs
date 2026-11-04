#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Referencias cruzadas de todo el programa: dónde se escribe y dónde se lee cada señal global. Es lo que usa la
// cadena para saltar de una condición que falta a lo que la escribe.
namespace HerramientasSICAR.Nucleo.Lad;

public sealed record SignalWrite(PlcBlock Block, Network Network, Assignment Assignment);

public sealed record SignalRead(PlcBlock Block, Network Network);

public sealed class CrossReferences
{
    private readonly Dictionary<string, List<SignalWrite>> _writes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<SignalRead>> _reads = new(StringComparer.Ordinal);

    public CrossReferences(IEnumerable<PlcBlock> blocks)
    {
        foreach (var block in blocks)
        {
            foreach (var network in block.Networks)
            {
                var read = new HashSet<string>(StringComparer.Ordinal);
                foreach (var assignment in network.Assignments)
                {
                    if (assignment.Target.Scope == OperandScope.Global && assignment.Kind is not (AssignmentKind.Jump or AssignmentKind.JumpNot))
                    {
                        Add(_writes, assignment.Target.Text, new SignalWrite(block, network, assignment));
                    }
                    Collect(assignment.Condition, read);
                    if (assignment.Value?.Operand is { Scope: OperandScope.Global } source)
                    {
                        read.Add(source.Text);
                    }
                }
                foreach (var call in network.Calls)
                {
                    Collect(call.Enable, read);
                    foreach (var input in call.Inputs.Values)
                    {
                        Collect(input, read);
                    }
                }
                foreach (var operand in read)
                {
                    Add(_reads, operand, new SignalRead(block, network));
                }
            }
        }
    }

    public IReadOnlyList<SignalWrite> WritersOf(Operand operand) => _writes.GetValueOrDefault(operand.Text) ?? [];

    public IReadOnlyList<SignalRead> ReadersOf(Operand operand) => _reads.GetValueOrDefault(operand.Text) ?? [];

    private static void Collect(LadExpression expression, HashSet<string> read)
    {
        foreach (var leaf in expression.Leaves())
        {
            switch (leaf)
            {
                case ContactExpression { Operand.Scope: OperandScope.Global } c:
                    read.Add(c.Operand.Text);
                    break;
                case EdgeContact { Operand.Scope: OperandScope.Global } e:
                    read.Add(e.Operand.Text);
                    break;
                case Comparison c:
                    Collect(c.Left, read);
                    Collect(c.Right, read);
                    break;
                case BoxOutput b:
                    foreach (var input in b.Inputs.Values)
                    {
                        Collect(input, read);
                    }
                    break;
            }
        }
    }

    private static void Collect(LadValue value, HashSet<string> read)
    {
        if (value.Operand is { Scope: OperandScope.Global } operand)
        {
            read.Add(operand.Text);
        }
        else if (value.Expression != null)
        {
            Collect(value.Expression, read);
        }
    }

    private static void Add<T>(Dictionary<string, List<T>> map, string key, T item)
    {
        if (!map.TryGetValue(key, out var list))
        {
            map[key] = list = [];
        }
        list.Add(item);
    }
}
