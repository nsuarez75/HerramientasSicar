#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Símbolos del PLC: variables de las tablas de variables, constantes de usuario y tipos de datos (UDT).
using HerramientasSICAR.Nucleo.Lad;

namespace HerramientasSICAR.Nucleo.Symbols;

// Variable de una tabla de variables del PLC: «++X-IN», tipo "typeRobotIn", %I10000.0.
public sealed record PlcTag(string Name, string DataType, string Address)
{
    public MultilingualText Comment { get; init; } = MultilingualText.Empty;
}

public sealed record PlcConstant(string Name, string DataType, string Value)
{
    public MultilingualText Comment { get; init; } = MultilingualText.Empty;
}

// Miembro de un UDT o de una estructura; Members solo si es una Struct anónima.
public sealed record TypeMember(string Name, string DataType)
{
    public MultilingualText Comment { get; init; } = MultilingualText.Empty;

    public IReadOnlyList<TypeMember> Members { get; init; } = [];
}

public sealed record PlcType(string Name, IReadOnlyList<TypeMember> Members);

// Lo que se muestra al tocar una señal: nombre, tipo, comentario y, solo si es marca o entrada, dirección.
public sealed record SymbolInfo(string Name, string DataType)
{
    public string? Address { get; init; }

    public MultilingualText Comment { get; init; } = MultilingualText.Empty;
}
