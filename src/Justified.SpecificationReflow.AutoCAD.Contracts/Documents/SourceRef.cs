namespace Justified.SpecificationReflow.AutoCAD.Contracts.Documents;

public sealed class SourceRef
{
    public int ParagraphIndex { get; set; }

    public int? RunIndex { get; set; }

    public TextRange? TextRange { get; set; }
}

public sealed class TextRange
{
    public int Start { get; set; }

    public int Length { get; set; }
}
