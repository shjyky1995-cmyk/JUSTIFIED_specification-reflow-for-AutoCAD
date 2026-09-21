using System.Collections.Generic;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;

namespace Justified.SpecificationReflow.AutoCAD.DocxAdapter;

public enum StyleMapMatch
{
    StyleId,
    Name
}

public sealed class StyleMapEntry
{
    public StyleMapMatch Match { get; set; }

    public string Key { get; set; } = string.Empty;

    public BlockType Target { get; set; }
}

// 调用方提供的样式映射。正文必须逐条列入；一级/二级标题还可沿 basedOn 继承。
public sealed class StyleMap
{
    public List<StyleMapEntry> Entries { get; set; } = new List<StyleMapEntry>();

    public BlockType? Find(string styleId, IReadOnlyList<string> names)
    {
        foreach (var entry in Entries)
        {
            if (entry.Match != StyleMapMatch.StyleId || string.IsNullOrWhiteSpace(entry.Key)) continue;
            if (string.Equals(entry.Key.Trim(), styleId.Trim(), System.StringComparison.OrdinalIgnoreCase))
                return entry.Target;
        }

        foreach (var name in names)
        {
            foreach (var entry in Entries)
            {
                if (entry.Match != StyleMapMatch.Name || string.IsNullOrWhiteSpace(entry.Key)) continue;
                if (string.Equals(entry.Key.Trim(), name.Trim(), System.StringComparison.OrdinalIgnoreCase))
                    return entry.Target;
            }
        }

        return null;
    }
}
