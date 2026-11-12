#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Operando de una instrucción tal como viene en el XML de TIA: una variable (global o local),
// una constante, una etiqueta de salto o una dirección absoluta.
namespace HerramientasSICAR.Core.Lad;

public enum OperandScope
{
    // Variable de la tabla de PLC o miembro de un DB global: "Señal", "DB".Miembro.
    Global,
    // Variable de la interfaz del bloque: #Sequence.ilockAuto.
    Local,
    // Valor escrito tal cual: 5, true, T#2s.
    Literal,
    // Constante con nombre de la tabla de PLC o del bloque.
    GlobalConstant,
    LocalConstant,
    Label,
    // Dirección absoluta (%M10.0) sin símbolo.
    Address,
    Unknown,
}

public sealed record Operand(OperandScope Scope, IReadOnlyList<string> Components, string Text)
{
    public static Operand Unknown(string text) => new(OperandScope.Unknown, [], text);

    public bool IsVariable => Scope is OperandScope.Global or OperandScope.Local;

    public bool IsConstant => Scope is OperandScope.Literal or OperandScope.GlobalConstant or OperandScope.LocalConstant;

    // Último componente sin índices: ilockAuto en #Sequence.ilockAuto.
    public string LastName
    {
        get
        {
            if (Components.Count == 0)
            {
                return Text;
            }
            var last = Components[^1];
            var bracket = last.IndexOf('[');
            return bracket < 0 ? last : last[..bracket];
        }
    }

    // Componentes como ruta con puntos y sin comillas: ++X-IN.soAutoOn, Sequence.ilockAuto.
    public string Path => Components.Count == 0 ? Text : string.Join(".", Components);

    // Dos operandos son el mismo si apuntan a lo mismo, aunque vengan de redes distintas.
    public bool Equals(Operand? other) =>
        other is not null && Scope == other.Scope && Text == other.Text && Components.SequenceEqual(other.Components);

    public override int GetHashCode() => (Scope, Text).GetHashCode();

    public override string ToString() => Text;
}
