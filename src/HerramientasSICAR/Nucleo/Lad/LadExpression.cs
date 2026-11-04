#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Lógica que alimenta una bobina, reconstruida desde los cables de la red LAD. Se conserva la forma
// serie/paralelo de la red para poder dibujarla, y cada hoja sabe qué es (contacto, comparador, caja…).
namespace HerramientasSICAR.Nucleo.Lad;

public abstract record LadExpression
{
    // Barra de alimentación izquierda: siempre 1.
    public static readonly LadExpression Rail = new PowerRail();

    public static LadExpression Series(IEnumerable<LadExpression> items)
    {
        var list = new List<LadExpression>();
        foreach (var item in items)
        {
            switch (item)
            {
                case PowerRail:
                    continue;
                case SeriesExpression series:
                    list.AddRange(series.Items);
                    break;
                default:
                    list.Add(item);
                    break;
            }
        }
        return list.Count switch
        {
            0 => Rail,
            1 => list[0],
            _ => new SeriesExpression(list),
        };
    }

    public static LadExpression Series(params LadExpression[] items) => Series((IEnumerable<LadExpression>)items);

    public static LadExpression Parallel(IEnumerable<LadExpression> items)
    {
        var list = new List<LadExpression>();
        foreach (var item in items)
        {
            if (item is PowerRail)
            {
                // Una rama que sale de la barra sin nada en medio deja pasar siempre.
                return Rail;
            }
            if (item is ParallelExpression parallel)
            {
                list.AddRange(parallel.Items);
            }
            else
            {
                list.Add(item);
            }
        }
        return list.Count switch
        {
            0 => new Unconnected(),
            1 => list[0],
            _ => new ParallelExpression(list),
        };
    }

    public static LadExpression Parallel(params LadExpression[] items) => Parallel((IEnumerable<LadExpression>)items);

    // Unión de ramas en una red: lo que salió del mismo cable es el mismo objeto y queda como serie común. Sin esto, cada
    // columna de contactos en paralelo duplicaría todo lo anterior. Dos contactos iguales dibujados en ramas distintas son
    // objetos distintos y se mantienen, para que el dibujo siga siendo el de TIA.
    public static LadExpression Junction(IEnumerable<LadExpression> items)
    {
        var branches = items.Select(i => i is SeriesExpression s ? s.Items : [i]).ToList();
        var limit = branches.Count == 0 ? 0 : branches.Min(b => b.Count) - 1;
        var shared = 0;
        while (shared < limit && branches.All(b => ReferenceEquals(b[shared], branches[0][shared])))
        {
            shared++;
        }
        var rests = branches.Select(b => Series(b.Skip(shared)));
        return shared == 0 ? Parallel(rests) : Series([.. branches[0].Take(shared), Parallel(rests)]);
    }

    public static LadExpression Not(LadExpression inner) => inner is NotExpression not ? not.Inner : new NotExpression(inner);

    // Hojas de la expresión en orden de dibujo.
    public IEnumerable<LadExpression> Leaves()
    {
        switch (this)
        {
            case SeriesExpression s:
                foreach (var leaf in s.Items.SelectMany(i => i.Leaves()))
                {
                    yield return leaf;
                }
                break;
            case ParallelExpression p:
                foreach (var leaf in p.Items.SelectMany(i => i.Leaves()))
                {
                    yield return leaf;
                }
                break;
            case NotExpression n:
                foreach (var leaf in n.Inner.Leaves())
                {
                    yield return leaf;
                }
                break;
            case XorExpression x:
                foreach (var leaf in x.Items.SelectMany(i => i.Leaves()))
                {
                    yield return leaf;
                }
                break;
            case EdgeBox e:
                foreach (var leaf in e.Input.Leaves())
                {
                    yield return leaf;
                }
                break;
            default:
                yield return this;
                break;
        }
    }
}

public sealed record PowerRail : LadExpression
{
    public override string ToString() => "1";
}

// Entrada sin cable: no conduce nunca.
public sealed record Unconnected : LadExpression
{
    public override string ToString() => "<sin conexión>";
}

public sealed record SeriesExpression(IReadOnlyList<LadExpression> Items) : LadExpression
{
    public override string ToString() =>
        string.Join(" Y ", Items.Select(i => i is ParallelExpression ? "(" + i + ")" : i.ToString()));
}

public sealed record ParallelExpression(IReadOnlyList<LadExpression> Items) : LadExpression
{
    public override string ToString() => string.Join(" O ", Items);
}

public sealed record NotExpression(LadExpression Inner) : LadExpression
{
    public override string ToString() => Inner is ContactExpression ? "NO " + Inner : "NO (" + Inner + ")";
}

public sealed record XorExpression(IReadOnlyList<LadExpression> Items) : LadExpression
{
    public override string ToString() => "XOR(" + string.Join(", ", Items) + ")";
}

// Contacto normal (abierto) o negado (cerrado) sobre un bit.
public sealed record ContactExpression(Operand Operand, bool Negated) : LadExpression
{
    public override string ToString() => (Negated ? "NO " : "") + Operand;
}

// Contacto de flanco (P/N) sobre un bit.
public sealed record EdgeContact(Operand Operand, bool Rising) : LadExpression
{
    public override string ToString() => (Rising ? "P(" : "N(") + Operand + ")";
}

// Detección de flanco del resultado anterior (P_TRIG / N_TRIG en la red).
public sealed record EdgeBox(LadExpression Input, bool Rising) : LadExpression
{
    public override string ToString() => (Rising ? "P(" : "N(") + Input + ")";
}

public enum CompareOperator
{
    Equal,
    NotEqual,
    Greater,
    GreaterOrEqual,
    Less,
    LessOrEqual,
}

// Valor que entra en un comparador o en una caja: un operando o la salida de otra instrucción.
public sealed record LadValue(Operand? Operand, LadExpression? Expression)
{
    public static LadValue Of(Operand operand) => new(operand, null);

    public static LadValue Of(LadExpression expression) => new(null, expression);

    // Con los bytes intercambiados (CAW en STL): el robot manda sus palabras al revés que el PLC.
    public bool Swapped { get; init; }

    public override string ToString()
    {
        var text = Operand?.ToString() ?? Expression?.ToString() ?? "?";
        return Swapped ? "CAW(" + text + ")" : text;
    }
}

public sealed record Comparison(CompareOperator Operator, LadValue Left, LadValue Right, string DataType) : LadExpression
{
    public static string Symbol(CompareOperator op) => op switch
    {
        CompareOperator.Equal => "==",
        CompareOperator.NotEqual => "<>",
        CompareOperator.Greater => ">",
        CompareOperator.GreaterOrEqual => ">=",
        CompareOperator.Less => "<",
        _ => "<=",
    };

    public override string ToString() => $"{Left} {Symbol(Operator)} {Right}";
}

// Salida booleana de una caja que no es lógica pura: temporizador, contador, llamada a FB…
// Se guardan sus entradas para mostrarlas (IN, PT, ET…).
public sealed record BoxOutput(string Box, Operand? Instance, string Pin, IReadOnlyDictionary<string, LadValue> Inputs)
    : LadExpression
{
    public override string ToString() => (Instance?.ToString() ?? Box) + "." + Pin.ToUpperInvariant();
}

// Algo que no se sabe interpretar; se muestra con su texto.
public sealed record UnknownExpression(string Text) : LadExpression
{
    public override string ToString() => Text;
}
