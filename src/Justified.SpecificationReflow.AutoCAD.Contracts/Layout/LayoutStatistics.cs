using System;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Layout;

public sealed class LayoutStatistics
{
    public int PageCount { get; set; }

    public int RowCount { get; set; }

    public int ObjectCount { get; set; }

    public int WarningCount { get; set; }

    public TimeSpan Elapsed { get; set; }
}
