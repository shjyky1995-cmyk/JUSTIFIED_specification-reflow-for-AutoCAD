using System.Collections.Generic;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Documents;

public sealed class Document
{
    public string SchemaVersion { get; set; } = string.Empty;

    public string DocumentId { get; set; } = string.Empty;

    public string DisciplineCode { get; set; } = string.Empty;

    public SourceInfo Source { get; set; } = new SourceInfo();

    public List<Block> Blocks { get; set; } = new List<Block>();

    public Dictionary<string, object> Extensions { get; set; } = new Dictionary<string, object>();
}
