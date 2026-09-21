using System.Collections.Generic;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Layout;

public sealed class LayoutResult
{
    public string SchemaVersion { get; set; } = string.Empty;

    public string DocumentHash { get; set; } = string.Empty;

    public StandardRef StandardRef { get; set; } = new StandardRef();

    public TemplateRef TemplateRef { get; set; } = new TemplateRef();

    public string EngineVersion { get; set; } = string.Empty;

    public string MeasurementProfileHash { get; set; } = string.Empty;

    public List<LayoutPage> Pages { get; set; } = new List<LayoutPage>();

    public List<Diagnostic> Diagnostics { get; set; } = new List<Diagnostic>();

    public LayoutStatistics Statistics { get; set; } = new LayoutStatistics();
}
