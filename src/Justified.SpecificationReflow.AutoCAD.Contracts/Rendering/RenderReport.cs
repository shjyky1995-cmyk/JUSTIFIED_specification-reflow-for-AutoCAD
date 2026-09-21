using System.Collections.Generic;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Rendering;

public sealed class RenderReport
{
    public bool Success { get; set; }

    public int PageCount { get; set; }

    public int ObjectCount { get; set; }

    public List<Diagnostic> Diagnostics { get; set; } = new List<Diagnostic>();
}
