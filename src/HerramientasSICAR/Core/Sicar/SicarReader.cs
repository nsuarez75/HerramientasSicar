#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Reconoce los FB de secuencia y de mensajes SICAR entre los bloques leídos y construye su modelo.
using System.Text.RegularExpressions;
using HerramientasSICAR.Core.Lad;

namespace HerramientasSICAR.Core.Sicar;

public static partial class SicarReader
{
    // Un FB del rango SICAR es de secuencia si escribe en #Sequence.* y de mensajes si escribe en #message.no_N.
    public static bool IsSequenceBlock(PlcBlock block) =>
        IsSicarFb(block) && Writes(block).Any(a => IsMember(a.Target, "Sequence"));

    public static bool IsMessageBlock(PlcBlock block) =>
        IsSicarFb(block) && Writes(block).Any(a => MessageNumber(a.Target) != null);

    // FB del rango SICAR con formato de bloque de mensajes que no da mensajes: calcula una vez señales
    // que usan las secuencias (manual de SICAR, 4.1).
    public static bool IsCrossReferenceBlock(PlcBlock block) =>
        IsSicarFb(block) && !IsSequenceBlock(block) && !IsMessageBlock(block);

    public static SequenceBlock ReadSequence(PlcBlock block, string? editingCulture = null)
    {
        var sequence = SicarNumbering.SequenceFromBlock(block.Number)
            ?? throw new ArgumentException($"El bloque {block.Number} no está en el rango de secuencias SICAR.", nameof(block));

        SequenceStep? permanent = null;
        var steps = new List<SequenceStep>();
        foreach (var network in block.Networks.Where(n => n.Label != null))
        {
            int number;
            if (string.Equals(network.Label, SequenceStep.PermanentLabel, StringComparison.OrdinalIgnoreCase))
            {
                number = 0;
            }
            else if (!SicarNumbering.TryParseStepLabel(network.Label, out number))
            {
                continue;
            }

            // Los pasos de reserva van en una red STL sin nada programado.
            if (!network.IsLad && network.Assignments.Count == 0)
            {
                continue;
            }

            var step = ReadStep(network, number, editingCulture);
            if (number == 0)
            {
                permanent = step;
            }
            else
            {
                steps.Add(step);
            }
        }

        return new SequenceBlock(sequence, block.Name) { Block = block, Permanent = permanent, Steps = steps };
    }

    public static MessageBlock ReadMessages(PlcBlock block, string? editingCulture = null)
    {
        var messages = new List<SicarMessage>();
        foreach (var network in block.Networks)
        {
            // La última asignación a una salida de mensaje de la red es la que cuenta.
            var assignment = network.Assignments.LastOrDefault(a => a.Kind == AssignmentKind.Coil && MessageNumber(a.Target) != null);
            if (assignment == null)
            {
                continue;
            }
            messages.Add(new SicarMessage(MessageNumber(assignment.Target)!.Value, network.Index)
            {
                Titles = StepTitles.Parse(network.Comment, editingCulture),
                Category = CategoryOf(network.Comment, editingCulture),
                Condition = assignment.Condition,
            });
        }
        return new MessageBlock(block.Number, block.Name) { Block = block, Messages = messages };
    }

    private static SequenceStep ReadStep(Network network, int number, string? editingCulture)
    {
        var sections = new Dictionary<SectionKind, LadExpression>();
        var jumps = new List<StepJump>();
        string? watchdog = null;
        string? waiting = null;

        foreach (var assignment in network.Assignments)
        {
            if (!IsMember(assignment.Target, "Sequence"))
            {
                continue;
            }
            var member = assignment.Target.LastName;
            if (assignment.Kind == AssignmentKind.Coil && Sicar.Sections.FromMember(member) is { } kind)
            {
                sections[kind] = assignment.Condition;
            }
            else if (assignment.Kind == AssignmentKind.Move)
            {
                var value = assignment.Value?.Operand;
                switch (member.ToLowerInvariant())
                {
                    case "stepnplus1" when value is { Scope: OperandScope.Literal } && int.TryParse(value.Text, out var target):
                        jumps.Add(new StepJump(target, assignment.Condition));
                        break;
                    case "twdsetpoint":
                        watchdog = value?.Text;
                        break;
                    case "twasetpoint":
                        waiting = value?.Text;
                        break;
                }
            }
        }

        return new SequenceStep(number, network.Label!, network.Index)
        {
            Titles = StepTitles.Parse(network.Comment, editingCulture),
            Sections = sections,
            Jumps = jumps,
            WatchdogSetpoint = watchdog,
            WaitingSetpoint = waiting,
        };
    }

    // Categoría del mensaje: «Category_7» en el comentario. Sin categoría o con una no válida, la 1.
    private static int CategoryOf(MultilingualText comment, string? editingCulture)
    {
        var text = editingCulture != null ? comment.Get(editingCulture) : string.Join("\n", comment.Items.Values);
        var match = CategoryLine().Match(text);
        return match.Success && int.Parse(match.Groups[1].Value) is var category && category is 1 or 7 or 8 ? category : 1;
    }

    private static bool IsSicarFb(PlcBlock block) =>
        block.Kind == BlockKind.FB && SicarNumbering.SequenceFromBlock(block.Number) != null;

    private static IEnumerable<Assignment> Writes(PlcBlock block) => block.Networks.SelectMany(n => n.Assignments);

    // #Sequence.ilockAuto, #message.no_3…: variable local cuyo primer componente es el indicado.
    private static bool IsMember(Operand operand, string root) =>
        operand.Scope == OperandScope.Local && operand.Components.Count == 2 &&
        string.Equals(operand.Components[0], root, StringComparison.OrdinalIgnoreCase);

    private static int? MessageNumber(Operand operand)
    {
        if (!IsMember(operand, "message"))
        {
            return null;
        }
        var match = MessageMember().Match(operand.LastName);
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    // .NET Framework no tiene GeneratedRegex: se crea la primera vez que se usa.
    private static Regex MessageMember() => _MessageMember ??= new Regex(@"^no_(\d+)$", RegexOptions.IgnoreCase);

    private static Regex? _MessageMember;

    // .NET Framework no tiene GeneratedRegex: se crea la primera vez que se usa.
    private static Regex CategoryLine() => _CategoryLine ??= new Regex(@"Category_(\d+)", RegexOptions.IgnoreCase);

    private static Regex? _CategoryLine;
}
