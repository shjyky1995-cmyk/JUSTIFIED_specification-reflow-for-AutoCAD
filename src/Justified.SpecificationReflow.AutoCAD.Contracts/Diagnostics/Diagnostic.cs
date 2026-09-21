using System.Collections.Generic;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;

public sealed class Diagnostic
{
    public string Code { get; set; } = string.Empty;

    public Severity Severity { get; set; }

    public DiagnosticStage Stage { get; set; }

    public string Message { get; set; } = string.Empty;

    public SourceRef? SourceRef { get; set; }

    public Dictionary<string, string>? Details { get; set; }
}

public enum Severity
{
    Error,
    Warning,
    Info
}

public enum DiagnosticStage
{
    Parse,
    Protocol,
    Standard,
    Measure,
    Layout,
    Render
}
