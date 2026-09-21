namespace Justified.SpecificationReflow.AutoCAD.Contracts.Documents;

public sealed class Numbering
{
    public string Label { get; set; } = string.Empty;

    public NumberingSourceKind SourceKind { get; set; }

    public int Level { get; set; }
}

public enum NumberingSourceKind
{
    Manual,
    Automatic
}
