#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// De dónde salen los valores de las señales al evaluar: en la app, de valores simulados; en Ignition,
// de los tags. Un valor desconocido (sin dato o con mala calidad) es null.
using System.Globalization;
using HerramientasSICAR.Core.Lad;

namespace HerramientasSICAR.Core.Diagnostics;

public interface IValueSource
{
    object? Value(Operand operand);
}

// Valores por texto del operando ("Est-B1", #Sequence.stepN…). Las constantes literales se leen solas.
public sealed class DictionaryValueSource : IValueSource
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

    public DictionaryValueSource Set(string operand, object? value)
    {
        _values[operand] = value;
        return this;
    }

    public object? Value(Operand operand)
    {
        if (_values.TryGetValue(operand.Text, out var value))
        {
            return value;
        }
        return operand.Scope == OperandScope.Literal ? Literals.Parse(operand.Text) : null;
    }
}

// Constantes tal como las escribe TIA: 200, -3, 2.5, true, 16#FF, 2#1010, T#5s, T#1m30s, 'texto'.
public static class Literals
{
    public static object? Parse(string text)
    {
        var t = text.Trim();
        if (t.Length == 0)
        {
            return null;
        }
        if (bool.TryParse(t, out var b))
        {
            return b;
        }
        var hash = t.IndexOf('#');
        if (hash > 0)
        {
            var prefix = t[..hash].ToUpperInvariant();
            var body = t[(hash + 1)..].Replace("_", "");
            switch (prefix)
            {
                case "16":
                    return long.Parse(body, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                case "2":
                    return Convert.ToInt64(body, 2);
                case "8":
                    return Convert.ToInt64(body, 8);
                case "T":
                case "TIME":
                    return DurationMs(body);
            }
        }
        if (long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
        {
            return l;
        }
        if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
        {
            return d;
        }
        return t.Length >= 2 && t[0] == '\'' && t[^1] == '\'' ? t[1..^1] : t;
    }

    // T#1d2h3m4s5ms → milisegundos.
    private static long? DurationMs(string body)
    {
        long total = 0;
        var number = "";
        var s = body.ToLowerInvariant();
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (char.IsDigit(c) || c == '.')
            {
                number += c;
                continue;
            }
            if (number.Length == 0)
            {
                return null;
            }
            var value = double.Parse(number, CultureInfo.InvariantCulture);
            if (c == 'm' && i + 1 < s.Length && s[i + 1] == 's')
            {
                total += (long)value;
                i++;
            }
            else
            {
                total += (long)(value * c switch { 'd' => 86_400_000, 'h' => 3_600_000, 'm' => 60_000, 's' => 1000, _ => 0 });
            }
            number = "";
        }
        return number.Length == 0 ? total : null;
    }
}
