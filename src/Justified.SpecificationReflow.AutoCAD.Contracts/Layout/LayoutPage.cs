using System.Collections.Generic;
using Justified.SpecificationReflow.AutoCAD.Contracts.Geometry;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Layout;

public sealed class LayoutPage
{
    public int PageIndex { get; set; }

    public Point2 PageOffset { get; set; }

    public List<LayoutColumn> Columns { get; set; } = new List<LayoutColumn>();
}

public sealed class LayoutColumn
{
    public List<RowSlot> TableTexts { get; set; } = new List<RowSlot>();
    public List<LayoutLine> Lines { get; set; } = new List<LayoutLine>();
    public bool ShouldSerializeTableTexts() => TableTexts.Count > 0;
    public bool ShouldSerializeLines() => Lines.Count > 0;
    public int ColumnIndex { get; set; }

    public List<RowSlot> Rows { get; set; } = new List<RowSlot>();
}
