namespace Justified.SpecificationReflow.AutoCAD.Contracts.Templates;

public sealed class ColumnGeometry
{
    public string ColumnId { get; set; } = string.Empty;

    public double Left { get; set; }

    public double Right { get; set; }

    public double Top { get; set; }

    public double Bottom { get; set; }

    public double FirstBaselineY { get; set; }

    public int RowCount { get; set; }
}
