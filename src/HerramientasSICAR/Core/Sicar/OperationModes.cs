#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Zonas operacionales (OM) del PLC. En SICAR, el FC983 llama al FC995 (OpmodeDiagAddOn_FC) una vez por OM
// con noOPmode (número de OM), mwOPmode (palabra de marcas donde empiezan sus señales) y seqMBFrom/seqMBTo (rango de
// números de secuencia y bloque de mensajes). El FC995 está cerrado: sus escrituras en esa palabra no se ven.
using HerramientasSICAR.Core.Lad;

namespace HerramientasSICAR.Core.Sicar;

public sealed record OperationModeRange(int Number, int From, int To)
{
    // Byte de la palabra de marcas de la OM (10 = %MW10); null si la llamada no lo da como constante.
    public int? MarkerWord { get; init; }

    public bool Contains(int sequenceOrMessageBlock) => sequenceOrMessageBlock >= From && sequenceOrMessageBlock <= To;
}

public static class OperationModes
{
    public const string AssignmentBlock = "OpmodeDiagAddOn_FC";

    // Rangos de cada OM leídos de las llamadas del FC983. Las llamadas sin valores constantes se ignoran.
    public static IReadOnlyList<OperationModeRange> Read(PlcBlock fc983)
    {
        var ranges = new List<OperationModeRange>();
        foreach (var call in fc983.Networks.SelectMany(n => n.Calls).Where(IsAssignment))
        {
            if (Int(call, "noopmode") is { } number && Int(call, "seqmbfrom") is { } from && Int(call, "seqmbto") is { } to)
            {
                ranges.Add(new OperationModeRange(number, from, to) { MarkerWord = Int(call, "mwopmode") });
            }
        }
        return ranges.OrderBy(r => r.Number).ToList();
    }

    // Llamada del FC983 que escribe las señales de una OM, vista como una escritura de esa señal.
    public static SignalWrite? WriteOf(PlcBlock fc983, OperationModeRange range, Operand signal)
    {
        foreach (var network in fc983.Networks)
        {
            if (network.Calls.FirstOrDefault(c => IsAssignment(c) && Int(c, "noopmode") == range.Number) is { } call)
            {
                return new SignalWrite(fc983, network, new Assignment(AssignmentKind.BoxOutput, signal, call.Enable) { Box = call.Block });
            }
        }
        return null;
    }

    // OM de una secuencia o bloque de mensajes (número 1-255); null si no está en ninguna (se muestra con «*»).
    public static int? Of(IEnumerable<OperationModeRange> ranges, int sequenceOrMessageBlock) =>
        ranges.FirstOrDefault(r => r.Contains(sequenceOrMessageBlock))?.Number;

    private static bool IsAssignment(BlockCall call) =>
        call.Block.Equals(AssignmentBlock, StringComparison.OrdinalIgnoreCase) ||
        (call.Inputs.ContainsKey("noopmode") && call.Inputs.ContainsKey("seqmbfrom"));

    private static int? Int(BlockCall call, string pin) =>
        call.Inputs.TryGetValue(pin, out var value) && value.Operand is { Scope: OperandScope.Literal } literal &&
        int.TryParse(literal.Text, out var number)
            ? number
            : null;
}
