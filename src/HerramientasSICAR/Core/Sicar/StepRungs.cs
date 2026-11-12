#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Escalones de un paso para mostrarlo: uno por sección programada, en el orden del DiagAddOn, y uno por salto.
using HerramientasSICAR.Core.Lad;

namespace HerramientasSICAR.Core.Sicar;

// Escalón: la lógica, ya dispuesta en la rejilla, y lo que alimenta (ilockAuto, → S015…).
public sealed record LadRung(string Coil, LadExpression Logic, LadLayout Layout);

public static class StepRungs
{
    private static readonly SectionKind[] Order =
    [
        SectionKind.IlockManual, SectionKind.LimitManual, SectionKind.IlockAuto, SectionKind.IlockNoSync,
        SectionKind.IlockExtSync, SectionKind.IlockExtInt, SectionKind.SetError, SectionKind.TransAuto,
        SectionKind.TwdRun, SectionKind.TwaRun, SectionKind.StopPosExternReached,
    ];

    // Las secciones que cuentan al sincronizar (manual SICAR 2.9).
    private static readonly SectionKind[] SyncSections = [SectionKind.IlockAuto, SectionKind.IlockExtSync, SectionKind.TransAuto];

    public static string Label(SectionKind kind) => kind switch
    {
        SectionKind.IlockManual => "ilockManual",
        SectionKind.LimitManual => "limitManual",
        SectionKind.IlockAuto => "ilockAuto",
        SectionKind.TransAuto => "transAuto",
        SectionKind.IlockNoSync => "ilockNoSync",
        SectionKind.IlockExtSync => "ilockExtSync",
        SectionKind.IlockExtInt => "ilockExtInt",
        SectionKind.SetError => "setError",
        SectionKind.TwdRun => "twdRun",
        SectionKind.TwaRun => "twaRun",
        _ => "stopPosExternReached",
    };

    public static IReadOnlyList<LadRung> Build(SequenceStep step) => Build(step, Order);

    // Solo lo que decide si el paso es candidato al sincronizar: ilockAuto, ilockExtSync, transAuto y los saltos.
    public static IReadOnlyList<LadRung> ForSync(SequenceStep step) => Build(step, SyncSections);

    private static IReadOnlyList<LadRung> Build(SequenceStep step, IEnumerable<SectionKind> kinds)
    {
        var rungs = new List<LadRung>();
        foreach (var kind in kinds)
        {
            if (step.Sections.TryGetValue(kind, out var logic))
            {
                rungs.Add(new LadRung(Label(kind), logic, LadLayout.Build(logic)));
            }
        }
        rungs.AddRange(step.Jumps.Select(j => new LadRung("→ " + SicarNumbering.StepLabel(Math.Min(Math.Max(j.Target, SicarNumbering.FirstStep), SicarNumbering.LastStep)),
            j.Condition, LadLayout.Build(j.Condition))));
        return rungs;
    }
}
