#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Modelo SICAR construido sobre los bloques leídos: secuencias con sus pasos y secciones, y bloques de
// mensajes con sus mensajes. Los nombres de las secciones son los de typeSeq (UDT999).
using HerramientasSICAR.Core.Lad;

namespace HerramientasSICAR.Core.Sicar;

public enum SectionKind
{
    // ilockManual: interlocks en manual; empieza por la tecla de movimiento.
    IlockManual,
    // limitManual: fin del movimiento en manual.
    LimitManual,
    // ilockAuto: interlocks en automático; participa en la sincronización.
    IlockAuto,
    // transAuto: transición al paso siguiente en automático.
    TransAuto,
    IlockNoSync,
    IlockExtSync,
    IlockExtInt,
    // A 0 provoca un fallo inmediato.
    SetError,
    // A 0 paran la vigilancia y el tiempo de espera.
    TwdRun,
    TwaRun,
    // A 0 termina el paso aunque se haya pedido parar en él.
    StopPosExternReached,
}

public static class Sections
{
    // Miembro de typeSeq de cada sección. ilocklNoSync es como lo escribe el manual de SICAR.
    private static readonly Dictionary<string, SectionKind> ByMember = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ilockManual"] = SectionKind.IlockManual,
        ["limitManual"] = SectionKind.LimitManual,
        ["ilockAuto"] = SectionKind.IlockAuto,
        ["transAuto"] = SectionKind.TransAuto,
        ["ilockNoSync"] = SectionKind.IlockNoSync,
        ["ilocklNoSync"] = SectionKind.IlockNoSync,
        ["ilockExtSync"] = SectionKind.IlockExtSync,
        ["ilockExtInt"] = SectionKind.IlockExtInt,
        ["setError"] = SectionKind.SetError,
        ["twdRun"] = SectionKind.TwdRun,
        ["twaRun"] = SectionKind.TwaRun,
        ["stopPosExternReached"] = SectionKind.StopPosExternReached,
    };

    public static SectionKind? FromMember(string member) => ByMember.TryGetValue(member, out var kind) ? kind : null;

    // Valor que toma la sección si el paso no la programa (manual de SICAR, 2.3).
    public static bool DefaultValue(SectionKind kind) =>
        kind is not (SectionKind.IlockManual or SectionKind.LimitManual or SectionKind.IlockAuto or SectionKind.TransAuto);

    // Secciones de manual; el resto son de automático.
    public static bool IsManual(SectionKind kind) => kind is SectionKind.IlockManual or SectionKind.LimitManual;
}

// Salto programado con un MOVE a stepNplus1.
public sealed record StepJump(int Target, LadExpression Condition);

public sealed record SequenceStep(int Number, string Label, int Network)
{
    public const string PermanentLabel = "PERM";

    // Idioma (espanol, english…) → nombre del paso.
    public IReadOnlyDictionary<string, string> Titles { get; init; } = new Dictionary<string, string>();

    // Lógica de cada sección que programa el paso; la última bobina de la red manda, como en el PLC.
    public IReadOnlyDictionary<SectionKind, LadExpression> Sections { get; init; } = new Dictionary<SectionKind, LadExpression>();

    public IReadOnlyList<StepJump> Jumps { get; init; } = [];

    // Vigilancia y tiempo de espera propios del paso (en décimas de segundo), si los programa.
    public string? WatchdogSetpoint { get; init; }

    public string? WaitingSetpoint { get; init; }

    public bool IsPermanent => Label == PermanentLabel;

    public bool IsManual => Sections.Keys.Any(Sicar.Sections.IsManual) && !Sections.ContainsKey(SectionKind.IlockAuto)
                            && !Sections.ContainsKey(SectionKind.TransAuto);

    public string Title(string language) =>
        Titles.TryGetValue(language, out var title) ? title : Titles.Values.FirstOrDefault() ?? "";
}

public sealed record SequenceBlock(int Sequence, string Name)
{
    public PlcBlock Block { get; init; } = null!;

    public SequenceStep? Permanent { get; init; }

    // Pasos en el orden de las redes del FB.
    public IReadOnlyList<SequenceStep> Steps { get; init; } = [];

    public SequenceStep? Step(int number) => Steps.FirstOrDefault(s => s.Number == number);

    // Pasos a los que puede ir la secuencia desde uno: sus saltos y, si ninguno salta siempre,
    // el siguiente de la lista (del último, el primero).
    public IReadOnlyList<int> NextSteps(int number)
    {
        var step = Step(number);
        if (step == null)
        {
            return [];
        }
        var next = step.Jumps.Select(j => j.Target).Distinct().ToList();
        if (step.Jumps.Any(j => j.Condition is PowerRail))
        {
            return next;
        }
        var automatic = Steps.Where(s => !s.IsManual).ToList();
        var index = automatic.IndexOf(step);
        if (index >= 0 && automatic.Count > 0)
        {
            var linear = automatic[(index + 1) % automatic.Count].Number;
            if (!next.Contains(linear))
            {
                next.Add(linear);
            }
        }
        return next;
    }
}

public sealed record SicarMessage(int Number, int Network)
{
    public IReadOnlyDictionary<string, string> Titles { get; init; } = new Dictionary<string, string>();

    // 1 = rojo (paro), 7 = naranja (aviso), 8 = azul (información).
    public int Category { get; init; } = 1;

    public LadExpression Condition { get; init; } = new Unconnected();

    public string Title(string language) =>
        Titles.TryGetValue(language, out var title) ? title : Titles.Values.FirstOrDefault() ?? "";
}

public sealed record MessageBlock(int Number, string Name)
{
    public PlcBlock Block { get; init; } = null!;

    public IReadOnlyList<SicarMessage> Messages { get; init; } = [];
}
