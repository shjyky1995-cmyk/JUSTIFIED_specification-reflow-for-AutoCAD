namespace Justified.SpecificationReflow.AutoCAD.Contracts.Documents;

public sealed class TextRun
{
    public string Text { get; set; } = string.Empty;

    public RunSemantic Semantic { get; set; }
}
