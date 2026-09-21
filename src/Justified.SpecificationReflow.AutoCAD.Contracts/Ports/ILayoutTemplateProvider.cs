using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.Contracts.Templates;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Ports;

public interface ILayoutTemplateProvider
{
    TemplateLoadResult Load(TemplateRef reference, CancellationToken cancellationToken);
}

public sealed class TemplateLoadResult
{
    public TemplateLoadResult(LayoutTemplate? template, IReadOnlyList<Diagnostic> diagnostics)
    {
        Template = template;
        Diagnostics = diagnostics;
    }

    public LayoutTemplate? Template { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public bool Success => Template != null && !Diagnostics.Any(diagnostic => diagnostic.Severity == Severity.Error);
}
