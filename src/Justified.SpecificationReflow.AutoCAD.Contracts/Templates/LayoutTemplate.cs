using System.Collections.Generic;
using Justified.SpecificationReflow.AutoCAD.Contracts.Geometry;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Templates;

public sealed class LayoutTemplate
{
    public string TemplateId { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string DisciplineCode { get; set; } = string.Empty;

    public PaperCode PaperCode { get; set; }

    public TemplateStatus Status { get; set; }

    public Orientation Orientation { get; set; }

    public LengthUnit Unit { get; set; }

    public StandardRef StandardRef { get; set; } = new StandardRef();

    public TemplateAnchor Anchor { get; set; } = new TemplateAnchor();

    public PageBounds PageBounds { get; set; } = new PageBounds();

    public List<ColumnGeometry> Columns { get; set; } = new List<ColumnGeometry>();

    public Point2? PageStep { get; set; }

    public FramePolicy FramePolicy { get; set; }
}

public enum PaperCode
{
    A1,
    A2,
    A3
}

public enum TemplateStatus
{
    Uncalibrated,
    Calibrated
}

public enum Orientation
{
    Portrait,
    Landscape
}

public enum LengthUnit
{
    Mm
}

public enum FramePolicy
{
    None,
    StandardBlock
}
