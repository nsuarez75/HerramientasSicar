#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Bloque de programa leído del XML de TIA, con sus redes y lo que escribe cada una.
namespace HerramientasSICAR.Nucleo.Lad;

public enum AssignmentKind
{
    // Bobina normal (=).
    Coil,
    // Bobina o caja SET.
    Set,
    // Bobina o caja RESET.
    Reset,
    // Bobina de flanco positivo / negativo.
    RisingCoil,
    FallingCoil,
    // MOVE de un valor a un operando.
    Move,
    // Salida de otra caja o llamada escrita en un operando (ADD, TON.ET, salida de un FC…).
    BoxOutput,
    Jump,
    JumpNot,
}

// Lo que una red escribe en un operando y con qué condición.
public sealed record Assignment(AssignmentKind Kind, Operand Target, LadExpression Condition)
{
    // Para MOVE: el valor que se copia. Para salidas de caja: el pin de origen.
    public LadValue? Value { get; init; }

    // Caja o llamada que escribe (para salidas de caja).
    public string? Box { get; init; }

    // Dentro de un salto en STL: si no se pasa por ahí, la señal conserva su valor (tiene memoria).
    public bool Conditional { get; init; }
}

// Llamada a un FC o FB desde una red, con lo que entra en cada parámetro (nombres en minúsculas).
public sealed record BlockCall(string Block, string BlockType)
{
    public Operand? Instance { get; init; }

    public LadExpression Enable { get; init; } = LadExpression.Rail;

    public IReadOnlyDictionary<string, LadValue> Inputs { get; init; } = new Dictionary<string, LadValue>();
}

// Línea de una red en STL: instrucción como se escribe en TIA y su operando, si lo tiene.
public sealed record StlLine(string Instruction, Operand? Operand)
{
    public string? Label { get; init; }

    public string? Comment { get; init; }

    public override string ToString() => (Label != null ? Label + ": " : "") + Instruction + (Operand != null ? " " + Operand : "");
}

// Texto con una versión por idioma (cultura de TIA: es-ES, en-US…).
public sealed class MultilingualText
{
    public static readonly MultilingualText Empty = new(new Dictionary<string, string>());

    public MultilingualText(IReadOnlyDictionary<string, string> items)
    {
        Items = items;
    }

    public IReadOnlyDictionary<string, string> Items { get; }

    public bool IsEmpty => Items.Values.All(string.IsNullOrWhiteSpace);

    // El texto en la cultura pedida, si no el primero que no esté vacío.
    public string Get(string? culture = null)
    {
        if (culture != null && Items.TryGetValue(culture, out var text) && !string.IsNullOrWhiteSpace(text))
        {
            return text;
        }
        return Items.Values.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? "";
    }

    public override string ToString() => Get();
}

public sealed record Network(int Index, string Language)
{
    // Etiqueta de salto de la red (S001, PERM…).
    public string? Label { get; init; }

    public MultilingualText Title { get; init; } = MultilingualText.Empty;

    public MultilingualText Comment { get; init; } = MultilingualText.Empty;

    public IReadOnlyList<Assignment> Assignments { get; init; } = [];

    public IReadOnlyList<BlockCall> Calls { get; init; } = [];

    // Instrucciones de una red en STL, para mostrarlas.
    public IReadOnlyList<StlLine> Lines { get; init; } = [];

    // Texto de una red en SCL, para mostrarlo.
    public string? Source { get; init; }

    // Por qué no se ha podido interpretar la red, si es el caso.
    public string? Warning { get; init; }

    public bool IsLad => Language is "LAD" or "F_LAD";
}

public enum BlockKind
{
    FB,
    FC,
    OB,
    GlobalDB,
    InstanceDB,
    Other,
}

public sealed record PlcBlock(BlockKind Kind, int Number, string Name, string Language)
{
    public IReadOnlyList<Network> Networks { get; init; } = [];

    public MultilingualText Title { get; init; } = MultilingualText.Empty;

    // En un DB de instancia: el FB del que es instancia.
    public string? InstanceOf { get; init; }
}
