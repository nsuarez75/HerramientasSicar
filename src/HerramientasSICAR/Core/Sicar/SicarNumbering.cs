#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Convenios de numeración de SICAR: la secuencia N vive en el FC, el FB y el DB 1000+N,
// y sus pasos van de S001 a S228. Los bloques de mensajes comparten el mismo rango de números.
using System.Globalization;

namespace HerramientasSICAR.Core.Sicar;

public static class SicarNumbering
{
    public const int FirstSequence = 1;
    public const int LastSequence = 255;
    public const int BlockOffset = 1000;
    public const int FirstStep = 1;
    public const int LastStep = 228;

    public static bool IsSequenceNumber(int sequence) => sequence is >= FirstSequence and <= LastSequence;

    public static bool IsStepNumber(int step) => step is >= FirstStep and <= LastStep;

    // Número de FC, FB y DB de una secuencia (la 7 → 1007).
    public static int BlockNumber(int sequence)
    {
        if (!IsSequenceNumber(sequence))
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence,
                $"La secuencia tiene que estar entre {FirstSequence} y {LastSequence}.");
        }
        return sequence + BlockOffset;
    }

    // Secuencia a la que corresponde un número de bloque; null si está fuera del rango 1001-1255.
    public static int? SequenceFromBlock(int block)
    {
        var sequence = block - BlockOffset;
        return IsSequenceNumber(sequence) ? sequence : null;
    }

    // Etiqueta del paso en el FB (el 5 → S005).
    public static string StepLabel(int step)
    {
        if (!IsStepNumber(step))
        {
            throw new ArgumentOutOfRangeException(nameof(step), step,
                $"El paso tiene que estar entre {FirstStep} y {LastStep}.");
        }
        return "S" + step.ToString("000", CultureInfo.InvariantCulture);
    }

    // Lee una etiqueta de paso (S005). No acepta PERM ni etiquetas de otro formato.
    public static bool TryParseStepLabel(string? label, out int step)
    {
        step = 0;
        if (label is not { Length: 4 } || label[0] is not ('S' or 's'))
        {
            return false;
        }
        if (!int.TryParse(label.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            || !IsStepNumber(value))
        {
            return false;
        }
        step = value;
        return true;
    }
}
