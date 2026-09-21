using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Geometry;
using Justified.SpecificationReflow.AutoCAD.Contracts.Layout;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Ports;

public interface ITextMeasureService
{
    TextMeasurement Measure(IReadOnlyList<TextRun> runs, ResolvedStyle style, CancellationToken cancellationToken);
}

public sealed class RunMeasurement
{
    public RunMeasurement(double advance, Bounds2 inkBounds)
    {
        Advance = advance;
        InkBounds = inkBounds;
    }

    public double Advance { get; }

    public Bounds2 InkBounds { get; }
}

public sealed class TextMeasurement
{
    public TextMeasurement(IReadOnlyList<RunMeasurement> runs, IReadOnlyList<Diagnostic> diagnostics)
    {
        Runs = runs;
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<RunMeasurement> Runs { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    // 缺字形等错误以 E_FONT_MISSING 进入 Diagnostics；此时度量不可信，调用方必须检查。
    public bool Success => !Diagnostics.Any(diagnostic => diagnostic.Severity == Severity.Error);
}
