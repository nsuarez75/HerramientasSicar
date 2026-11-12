#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Señales que el programa calcula con una sola bobina normal: «PresenciaPiezaOK = PP01 Y PP02 Y PP03».
// Son las de los bloques de referencias cruzadas (manual SICAR, 4.1) y las que se programan igual en un FC.
// Sirven para razonar sobre las señales de verdad: así se sabe que presencia y ausencia de pieza no pueden
// estar a la vez. Una señal con SET/RESET, escrita en varios sitios, desde una caja o dentro de un salto de STL
// tiene memoria o no es combinacional y no se sustituye.
namespace HerramientasSICAR.Core.Lad;

public static class SignalDefinitions
{
    // Texto del operando global → lógica de su bobina.
    public static IReadOnlyDictionary<string, LadExpression> Build(IEnumerable<PlcBlock> blocks)
    {
        var writes = blocks
            .SelectMany(b => b.Networks)
            .SelectMany(n => n.Assignments)
            .Where(a => a.Target.Scope == OperandScope.Global && a.Kind is not (AssignmentKind.Jump or AssignmentKind.JumpNot))
            .GroupBy(a => a.Target.Text, StringComparer.Ordinal);

        var definitions = new Dictionary<string, LadExpression>(StringComparer.Ordinal);
        foreach (var group in writes)
        {
            var list = group.ToList();
            if (list.Count == 1 && list[0] is { Kind: AssignmentKind.Coil, Conditional: false } && !Uses(list[0].Condition, group.Key))
            {
                definitions[group.Key] = list[0].Condition;
            }
        }
        return definitions;
    }

    // La bobina se lee a sí misma (retención): tiene memoria y no se puede sustituir.
    private static bool Uses(LadExpression expression, string operand) =>
        expression.Leaves().Any(l => l is ContactExpression c && c.Operand.Text == operand);
}
