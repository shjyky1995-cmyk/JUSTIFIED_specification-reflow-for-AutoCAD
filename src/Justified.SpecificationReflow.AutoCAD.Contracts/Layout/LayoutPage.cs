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
    public int ColumnIndex { get; set; }

    public List<RowSlot> Rows { get; set; } = new List<RowSlot>();
}
