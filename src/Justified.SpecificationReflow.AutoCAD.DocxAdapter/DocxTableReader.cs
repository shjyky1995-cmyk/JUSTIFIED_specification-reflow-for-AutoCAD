using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;

namespace Justified.SpecificationReflow.AutoCAD.DocxAdapter;

// 只读取结构与边框可见性，不读取 Word 字号、物理行高或页面坐标。
internal static class DocxTableReader
{
    internal static string? Read(Table table, Styles? styles, Func<Paragraph, List<Block>> paragraphReader,
        System.Threading.CancellationToken cancellation, out TableData data)
    {
        data = new TableData();
        foreach (var node in table.Descendants())
        {
            cancellation.ThrowIfCancellationRequested();
            if (node != table && node is Table) return "暂不支持嵌套表格。";
            if (new[] { "textDirection", "tl2br", "tr2bl", "hMerge", "gridBefore", "gridAfter", "bidiVisual", "tblpPr", "ins", "del", "moveFrom", "moveTo", "tblPrChange", "trPrChange", "tcPrChange", "tblGridChange" }.Contains(node.LocalName))
                return "暂不支持表格属性或未接受修订：" + node.LocalName;
        }
        var grid = table.GetFirstChild<TableGrid>();
        if (grid == null || !grid.Elements<GridColumn>().Any())
        {
            var autoRows = table.Elements<TableRow>().ToList();
            var count = autoRows.Count > 0 ? autoRows[0].Elements<TableCell>().Count() : 0;
            if (count == 0 || autoRows.Any(r => r.Elements<TableCell>().Count() != count)
                || autoRows.SelectMany(r => r.Elements<TableCell>()).Any(c => Attr(Child(c.TableCellProperties, "tcW"), "type") != "auto"
                    || Child(c.TableCellProperties, "gridSpan") != null || Child(c.TableCellProperties, "vMerge") != null))
                return "表格缺少有效列宽网格，请在 Word/WPS 中明确设置列宽后保存。";
            data.AutoColumnWidths = true;
            data.ColumnWidths = Enumerable.Repeat(1d, count).ToList();
        }
        else foreach (var col in grid.Elements<GridColumn>())
        {
            if (!double.TryParse(Attr(col, "w"), NumberStyles.Float, CultureInfo.InvariantCulture, out var width)
                || double.IsInfinity(width) || double.IsNaN(width) || width <= 0) return "表格列宽必须为有限正数。";
            data.ColumnWidths.Add(width);
        }
        if (data.ColumnWidths.Count == 0) return "表格没有有效列。";
        if (table.ChildElements.Any(n => !(n is TableProperties) && !(n is TableGrid) && !(n is TableRow)))
            return "表格含暂不支持的行包装内容。";
        var rows = table.Elements<TableRow>().ToList();
        data.RowCount = rows.Count;
        if (rows.Count == 0) return "表格没有行。";
        var borderSources = new List<OpenXmlElement>();
        var id = Attr(Child(table.GetFirstChild<TableProperties>(), "tblStyle"), "val");
        var visited = new HashSet<string>();
        while (!string.IsNullOrEmpty(id))
        {
            if (!visited.Add(id)) return "表格样式存在循环继承。";
            var style = styles?.Elements<Style>().FirstOrDefault(s => s.StyleId?.Value == id);
            if (style == null) return "无法解析表格样式：" + id;
            // 条件边框不能当成无边框静默丢掉；普通 TableGrid 的整体边框可直接继承。
            if (style.ChildElements.Any(n => n.LocalName == "tblStylePr" && n.Descendants().Any(b => b.LocalName == "tblBorders" || b.LocalName == "tcBorders")))
                return "条件表格边框暂不支持，请在 Word/WPS 中将所需边框直接应用到单元格。";
            var props = Child(style, "tblPr");
            if (props != null) borderSources.Insert(0, props);
            id = Attr(Child(style, "basedOn"), "val");
        }
        if (table.GetFirstChild<TableProperties>() != null) borderSources.Add(table.GetFirstChild<TableProperties>()!);
        var mergeOrigins = new HashSet<TableCellData>();
        bool headerEnded = false;
        for (int r = 0; r < rows.Count; r++)
        {
            cancellation.ThrowIfCancellationRequested();
            var header = Child(rows[r].TableRowProperties, "tblHeader");
            bool isHeader = header != null && Attr(header, "val") != "0" && Attr(header, "val") != "false" && Attr(header, "val") != "off";
            if (isHeader && headerEnded) return "重复表头必须是从第一行开始的连续行。";
            if (isHeader) data.HeaderRows++; else headerEnded = true;
            if (rows[r].ChildElements.Any(n => !(n is TableRowProperties) && !(n is TableCell))) return "表格行含暂不支持的单元格包装内容。";
            int c = 0;
            foreach (var tc in rows[r].Elements<TableCell>())
            {
                int span = 1;
                var gridSpan = Child(tc.TableCellProperties, "gridSpan");
                if (gridSpan != null && (!int.TryParse(Attr(gridSpan, "val"), out span) || span < 1)) return "合并单元格列数无效。";
                if (c + span > data.ColumnWidths.Count) return "表格行超出列网格。";
                var paragraphs = new List<Block>();
                foreach (var child in tc.ChildElements)
                {
                    if (child is TableCellProperties) continue;
                    if (!(child is Paragraph p)) return "单元格含暂不支持的内容：" + child.LocalName;
                    paragraphs.AddRange(paragraphReader(p));
                }
                var merge = Child(tc.TableCellProperties, "vMerge");
                var mergeValue = Attr(merge, "val");
                if (merge != null && mergeValue != "restart" && mergeValue != "" && mergeValue != "continue") return "纵向合并标记无效。";
                var cell = new TableCellData { Row = r, Column = c, ColumnSpan = span, Paragraphs = paragraphs };
                cell.Top = Border(borderSources, tc, "top", r == 0 ? "top" : "insideH");
                cell.Bottom = Border(borderSources, tc, "bottom", r == rows.Count - 1 ? "bottom" : "insideH");
                cell.Left = Border(borderSources, tc, "left", c == 0 ? "left" : "insideV");
                cell.Right = Border(borderSources, tc, "right", c + span == data.ColumnWidths.Count ? "right" : "insideV");
                if (merge != null && mergeValue != "restart")
                {
                    var above = data.Cells.LastOrDefault(x => mergeOrigins.Contains(x) && x.Column == c && x.ColumnSpan == span && x.Row + x.RowSpan == r);
                    if (above == null) return "纵向合并没有匹配的起始单元格。";
                    if (paragraphs.Any(p => p.Runs.Any(run => !string.IsNullOrWhiteSpace(run.Text)))) return "纵向合并延续单元格含独立文字，不能静默丢弃。";
                    above.RowSpan++;
                    above.Bottom = cell.Bottom;
                    if (above.Left != cell.Left || above.Right != cell.Right) return "纵向合并单元格侧边框不连续，请统一边框。";
                }
                else
                {
                    data.Cells.Add(cell);
                    if (merge != null && mergeValue == "restart") mergeOrigins.Add(cell);
                }
                c += span;
            }
            if (c != data.ColumnWidths.Count) return "表格行与列宽网格不一致。";
        }
        var headerRows = data.HeaderRows;
        if (data.Cells.Any(c => c.Row < headerRows && c.Row + c.RowSpan > headerRows)) return "纵向合并不能跨过重复表头与正文的边界。";
        return null;
    }

    private static bool Border(List<OpenXmlElement> sources, TableCell cell, string edge, string tableEdge)
    {
        bool visible = false;
        foreach (var source in sources)
        {
            var border = Child(Child(source, "tblBorders"), tableEdge);
            if (border != null) visible = Visible(border);
        }
        var own = Child(Child(cell.TableCellProperties, "tcBorders"), edge);
        if (own != null) visible = Visible(own);
        return visible;
    }
    private static bool Visible(OpenXmlElement edge)
    {
        var value = Attr(edge, "val");
        return value != "" && value != "nil" && value != "none";
    }
    private static OpenXmlElement? Child(OpenXmlElement? node, string name) => node?.ChildElements.FirstOrDefault(c => c.LocalName == name);
    private static string Attr(OpenXmlElement? node, string name) => node?.GetAttributes().FirstOrDefault(a => a.LocalName == name).Value ?? string.Empty;
}
