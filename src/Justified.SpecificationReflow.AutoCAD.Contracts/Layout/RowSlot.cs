namespace Justified.SpecificationReflow.AutoCAD.Contracts.Layout;

public sealed class RowSlot
{
    public int RowIndex { get; set; }

    public double Baseline { get; set; }

    public Occupancy Occupancy { get; set; }

    public VisualLine? VisualLine { get; set; }
}

public enum Occupancy
{
    Text,
    Spacer
}
