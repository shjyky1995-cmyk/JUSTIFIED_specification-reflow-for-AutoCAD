using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Geometry;
using Justified.SpecificationReflow.AutoCAD.Contracts.Layout;
using Justified.SpecificationReflow.AutoCAD.Contracts.Ports;

namespace Justified.SpecificationReflow.AutoCAD.LayoutEngine;

internal sealed class MeasureOutcome
{
    private MeasureOutcome(Diagnostic? error, double advance, Bounds2 ink, IReadOnlyList<Diagnostic> warnings)
    {
        Error = error;
        Advance = advance;
        Ink = ink;
        Warnings = warnings;
    }

    public Diagnostic? Error { get; }

    public double Advance { get; }

    public Bounds2 Ink { get; }

    public IReadOnlyList<Diagnostic> Warnings { get; }

    public static MeasureOutcome Fail(Diagnostic error) => new MeasureOutcome(error, 0, new Bounds2(), new Diagnostic[0]);

    public static MeasureOutcome Ok(double advance, Bounds2 ink, IReadOnlyList<Diagnostic> warnings) => new MeasureOutcome(null, advance, ink, warnings);
}

// 同一次排版内缓存整段候选的测量。接受一行之前测量的是整行，不是各字宽度相加。
internal sealed class CachedMeasure
{
    private readonly ITextMeasureService _service;
    private readonly Dictionary<string, MeasureOutcome> _cache = new Dictionary<string, MeasureOutcome>(StringComparer.Ordinal);

    public CachedMeasure(ITextMeasureService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public MeasureOutcome Measure(string text, ResolvedStyle style, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return MeasureOutcome.Fail(Cancelled());
        var key = Key(style, text);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        TextMeasurement measurement;
        try
        {
            measurement = _service.Measure(
                new[] { new TextRun { Text = text, Semantic = RunSemantic.Normal } },
                style,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return MeasureOutcome.Fail(Cancelled());
        }
        catch (Exception error)
        {
            return MeasureOutcome.Fail(Problem(DiagnosticCodes.ECadEnv, "测量失败：" + error.GetType().Name + "。"));
        }

        if (measurement == null)
            return MeasureOutcome.Fail(Problem(DiagnosticCodes.ECadEnv, "测量没有返回结果。"));
        if (!measurement.Success)
        {
            Diagnostic? failure = null;
            if (measurement.Diagnostics != null)
            {
                foreach (var diagnostic in measurement.Diagnostics)
                {
                    if (diagnostic != null && diagnostic.Severity == Severity.Error)
                    {
                        failure = diagnostic;
                        break;
                    }
                }
            }

            return MeasureOutcome.Fail(failure ?? Problem(DiagnosticCodes.ECadEnv, "测量失败。"));
        }

        if (measurement.Runs == null || measurement.Runs.Count != 1 || measurement.Runs[0] == null)
            return MeasureOutcome.Fail(Problem(DiagnosticCodes.ECadEnv, "整行测量必须返回对应的一条度量。"));

        var run = measurement.Runs[0];
        if (!Finite(run.Advance) || run.Advance < 0 || !Finite(run.InkBounds.MinX) || !Finite(run.InkBounds.MinY) || !Finite(run.InkBounds.MaxX) || !Finite(run.InkBounds.MaxY))
            return MeasureOutcome.Fail(Problem(DiagnosticCodes.ECadEnv, "测量返回了非法宽度或字形边界。"));
        if (run.InkBounds.MaxX < run.InkBounds.MinX || run.InkBounds.MaxY < run.InkBounds.MinY)
            return MeasureOutcome.Fail(Problem(DiagnosticCodes.ECadEnv, "测量返回的字形边界上下颠倒。"));

        var warnings = new List<Diagnostic>();
        if (measurement.Diagnostics != null)
        {
            foreach (var diagnostic in measurement.Diagnostics)
            {
                if (diagnostic != null && diagnostic.Severity == Severity.Warning) warnings.Add(diagnostic);
            }
        }

        var outcome = MeasureOutcome.Ok(run.Advance, run.InkBounds, warnings);
        _cache[key] = outcome;
        return outcome;
    }

    private static string Key(ResolvedStyle style, string text)
    {
        var font = style.Font;
        return string.Join("|", new[]
        {
            style.TextHeight.ToString("R", CultureInfo.InvariantCulture),
            style.WidthFactor.ToString("R", CultureInfo.InvariantCulture),
            style.ObliqueAngle.ToString("R", CultureInfo.InvariantCulture),
            font == null ? string.Empty : font.FileIdentity ?? string.Empty,
            font == null ? string.Empty : font.BigFont ?? string.Empty,
            text
        });
    }

    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static Diagnostic Cancelled()
    {
        return new Diagnostic
        {
            Code = DiagnosticCodes.Cancelled,
            Severity = Severity.Error,
            Stage = DiagnosticStage.Layout,
            Message = "排版已取消。"
        };
    }

    private static Diagnostic Problem(string code, string message)
    {
        return new Diagnostic
        {
            Code = code,
            Severity = Severity.Error,
            Stage = code == DiagnosticCodes.Cancelled ? DiagnosticStage.Layout : DiagnosticStage.Measure,
            Message = message
        };
    }
}
