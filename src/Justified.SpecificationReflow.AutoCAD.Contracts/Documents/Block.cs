using System.Collections.Generic;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Documents;

public sealed class Block
{
    public string Id { get; set; } = string.Empty;

    public BlockType Type { get; set; }

    public SourceRef SourceRef { get; set; } = new SourceRef();

    public List<TextRun> Runs { get; set; } = new List<TextRun>();

    public Numbering? Numbering { get; set; }

    public int? SlotCount { get; set; }
}

public enum BlockType
{
    Heading1,
    Heading2,
    Paragraph,
    Spacer
}
