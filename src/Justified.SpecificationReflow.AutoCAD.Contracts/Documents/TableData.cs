using System.Collections.Generic;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Documents;

public sealed class TableData
{
    public List<double> ColumnWidths { get; set; } = new List<double>();
    public bool AutoColumnWidths { get; set; }
    public bool ShouldSerializeAutoColumnWidths() => AutoColumnWidths;
    public int RowCount { get; set; }
    public int HeaderRows { get; set; }
    public List<TableCellData> Cells { get; set; } = new List<TableCellData>();
}

public sealed class TableCellData
{
    public int Row { get; set; }
    public int Column { get; set; }
    public int RowSpan { get; set; } = 1;
    public int ColumnSpan { get; set; } = 1;
    public bool Top { get; set; }
    public bool Bottom { get; set; }
    public bool Left { get; set; }
    public bool Right { get; set; }
    public List<Block> Paragraphs { get; set; } = new List<Block>();
}

public static class DocumentBlocks
{
    public static IEnumerable<Block> All(IEnumerable<Block> blocks)
    {
        foreach (var block in blocks)
        {
            yield return block;
            if (block?.Table == null) continue;
            foreach (var cell in block.Table.Cells)
                foreach (var paragraph in cell.Paragraphs) yield return paragraph;
        }
    }
}
