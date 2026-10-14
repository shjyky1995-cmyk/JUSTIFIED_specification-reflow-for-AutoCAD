using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Geometry;
using Justified.SpecificationReflow.AutoCAD.Contracts.Layout;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.Contracts.Templates;

namespace Justified.SpecificationReflow.AutoCAD.LayoutEngine;

public sealed partial class SpecificationLayoutEngine
{
    private static void AppendTableHash(System.Text.StringBuilder b, TableData t)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        b.Append("|table:").Append(t.AutoColumnWidths).Append(t.RowCount).Append(':').Append(t.HeaderRows);
        foreach (var w in t.ColumnWidths) b.Append('|').Append(w.ToString("R", culture));
        foreach (var c in t.Cells)
        {
            b.Append('|').Append(c.Row).Append(',').Append(c.Column).Append(',').Append(c.RowSpan).Append(',').Append(c.ColumnSpan)
                .Append(c.Top).Append(c.Bottom).Append(c.Left).Append(c.Right);
            foreach (var p in c.Paragraphs)
            {
                b.Append('|').Append(p.Type).Append(':').Append(p.SlotCount).Append(':').Append(p.Numbering?.Label);
                foreach (var run in p.Runs) b.Append(run.Semantic).Append(':').Append(run.Text.Length).Append(':').Append(run.Text);
            }
        }
    }

    private sealed class TableMetrics
    {
        public double[] X = Array.Empty<double>();
        public int[] Heights = Array.Empty<int>();
        public double Ascent;
        public double Descent;
        public readonly Dictionary<TableCellData, List<VisualLine?>> Text = new Dictionary<TableCellData, List<VisualLine?>>();
    }

    private bool PlaceTable(Block block, Flow flow, LayoutTemplate template, InstitutionStandard standard,
        LineComposer composer, List<Diagnostic> diagnostics, CancellationToken cancellation)
    {
        bool Fail(string message)
        {
            diagnostics.Add(Problem(DiagnosticCodes.ENoLegalBreak, "表格 " + block.Id + "：" + message, DiagnosticStage.Layout, block.SourceRef));
            return false;
        }
        var table = block.Table;
        var format = standard.TableStyle;
        if (table == null || !ValidTable(table)) return Fail("行列或合并结构无效、重叠或有缺格。");
        if (format == null || !TableStyleRules.IsValid(format)) return Fail("当前院标未配置有效表格样式，请安装支持表格的标准包。");
        if (table.AutoColumnWidths)
        {
            if (format.AutoWidthPolicy != "equal-columns-with-warning") return Fail("表格采用自动列宽，当前院标未允许等宽排布；请在 Word/WPS 设置列宽。");
            diagnostics.Add(new Diagnostic { Code = "W_TABLE_AUTO_WIDTH", Severity = Severity.Warning, Stage = DiagnosticStage.Layout,
                SourceRef = block.SourceRef, Message = "表格 " + block.Id + " 未指定列宽；按院标在当前栏内等宽排布。需要不等宽时请在 Word/WPS 中设置列宽。" });
        }
        for (var i = 0; i < format.BeforeSlots; i++)
            if (!Place(flow, template, standard, null, 0, diagnostics, block)) return false;

        int next = 0;
        int failedColumns = 0;
        var cache = new Dictionary<double, TableMetrics>();
        while (next < table.RowCount)
        {
            if (cancellation.IsCancellationRequested) { diagnostics.Add(Cancelled("表格排版已取消。")); return false; }
            if (!TryPeek(flow, template, out var dest, out var error)) { diagnostics.Add(error!); return false; }
            var width = dest.Geometry.Right - dest.Geometry.Left;
            if (!cache.TryGetValue(width, out var metrics))
            {
                metrics = MeasureTable(table, width, standard, composer, diagnostics, cancellation);
                if (metrics == null) return false;
                cache.Add(width, metrics);
            }
            int end = table.HeaderRows == table.RowCount ? table.RowCount : GroupEnd(table, next);
            // 首个表头与首个正文行组一起迁移，避免孤立表头。
            if (next == 0 && table.HeaderRows > 0 && table.HeaderRows < table.RowCount)
                end = GroupEnd(table, table.HeaderRows);
            var ranges = new List<Tuple<int, int>>();
            if (next > 0 && dest.Row == 0 && table.HeaderRows > 0) ranges.Add(Tuple.Create(0, table.HeaderRows));
            ranges.Add(Tuple.Create(next, end));
            int slots = ranges.Sum(range => metrics.Heights.Skip(range.Item1).Take(range.Item2 - range.Item1).Sum());
            var pad = format.VerticalPaddingEm * standard.TextHeight;
            var top = dest.Geometry.FirstBaselineY - dest.Row * standard.RowPitch + metrics.Ascent + pad;
            int leading = Math.Max(0, (int)Math.Ceiling((top - dest.Geometry.Top - 1e-8) / standard.RowPitch));
            top -= leading * standard.RowPitch;
            if (dest.Row + leading + slots > dest.Geometry.RowCount || top - slots * standard.RowPitch < dest.Geometry.Bottom - 1e-8)
            {
                // 每种栏宽只尝试一次空栏；不会产生无限空页。
                if (dest.Row == 0 && ++failedColumns >= template.Columns.Count) return Fail("第 " + (next + 1) + " 行组连同重复表头超过完整栏高，请拆分该行组。");
                flow.Page = dest.Page; flow.Column = dest.Column; flow.Row = dest.Geometry.RowCount;
                continue;
            }
            failedColumns = 0;
            var page = EnsurePage(flow, template, dest.Page);
            var column = EnsureColumn(page, dest.Column);
            int rowSlot = dest.Row + leading;
            foreach (var range in ranges)
            {
                EmitTableRange(table, metrics, range.Item1, range.Item2, top, rowSlot, column, standard);
                int count = metrics.Heights.Skip(range.Item1).Take(range.Item2 - range.Item1).Sum();
                top -= count * standard.RowPitch;
                rowSlot += count;
            }
            for (int i = dest.Row; i < rowSlot; i++)
                column.Rows.Add(new RowSlot { RowIndex = i, Baseline = dest.Geometry.FirstBaselineY - i * standard.RowPitch, Occupancy = Occupancy.Spacer });
            flow.Page = dest.Page; flow.Column = dest.Column; flow.Row = rowSlot;
            next = end;
        }
        for (var i = 0; i < format.AfterSlots; i++)
            if (!Place(flow, template, standard, null, 0, diagnostics, block)) return false;
        return true;
    }

    private static bool ValidTable(TableData t)
    {
        if (t.RowCount < 1 || t.RowCount > 10000 || t.ColumnWidths == null || t.ColumnWidths.Count < 1 || t.ColumnWidths.Count > 256
            || t.Cells == null || t.Cells.Count > 100000 || t.HeaderRows < 0 || t.HeaderRows > t.RowCount || t.ColumnWidths.Any(w => !Positive(w))
            || !Positive(t.ColumnWidths.Sum())) return false;
        var occupied = new HashSet<long>();
        foreach (var c in t.Cells)
        {
            if (c == null || c.Row < 0 || c.Column < 0 || c.RowSpan < 1 || c.ColumnSpan < 1 || c.RowSpan > t.RowCount - c.Row
                || c.ColumnSpan > t.ColumnWidths.Count - c.Column || c.Paragraphs == null
                || c.Paragraphs.Any(p => p == null || p.Type == BlockType.Table)
                || (c.Row < t.HeaderRows && c.Row + c.RowSpan > t.HeaderRows)) return false;
            for (int r = c.Row; r < c.Row + c.RowSpan; r++)
                for (int col = c.Column; col < c.Column + c.ColumnSpan; col++)
                    if (!occupied.Add((long)r * t.ColumnWidths.Count + col)) return false;
        }
        return occupied.Count == t.RowCount * t.ColumnWidths.Count;
    }

    private static int GroupEnd(TableData table, int start)
    {
        int end = start + 1;
        for (int r = start; r < end; r++)
            foreach (var c in table.Cells.Where(c => c.Row == r)) end = Math.Max(end, c.Row + c.RowSpan);
        return end;
    }

    private static TableMetrics? MeasureTable(TableData table, double width, InstitutionStandard standard,
        LineComposer composer, List<Diagnostic> diagnostics, CancellationToken cancellation)
    {
        var m = new TableMetrics { X = new double[table.ColumnWidths.Count + 1], Heights = Enumerable.Repeat(1, table.RowCount).ToArray(), Ascent = standard.TextHeight };
        var sum = table.ColumnWidths.Sum();
        for (int i = 0; i < table.ColumnWidths.Count; i++) m.X[i + 1] = m.X[i] + width * table.ColumnWidths[i] / sum;
        var pad = standard.TableStyle!.HorizontalPaddingEm * standard.TextHeight;
        foreach (var cell in table.Cells)
        {
            var lines = new List<VisualLine?>();
            m.Text.Add(cell, lines);
            var available = m.X[cell.Column + cell.ColumnSpan] - m.X[cell.Column] - 2 * pad;
            if (available <= 0) { diagnostics.Add(Problem(DiagnosticCodes.ENoLegalBreak, "表格第 " + (cell.Row + 1) + " 行第 " + (cell.Column + 1) + " 列窄于单元格留白。", DiagnosticStage.Layout, null)); return null; }
            foreach (var p in cell.Paragraphs)
            {
                if (p.Type == BlockType.Spacer) { for (int i = 0; i < (p.SlotCount ?? 1); i++) lines.Add(null); continue; }
                // 表内所有文字使用正文样式，编号与上下标语义仍保留。
                var body = new Block { Type = BlockType.Paragraph, Runs = p.Runs, Numbering = p.Numbering, SourceRef = p.SourceRef };
                if (!TryResolve(standard, body, out var style, out _, out var styleError)) { diagnostics.Add(styleError!); return null; }
                if (HasScriptRun(body) && !ScriptCalibration.IsComplete(standard)) { diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "表格上下标尚未标定。", DiagnosticStage.Standard, p.SourceRef)); return null; }
                if (FirstMissingGlyph(body, style) != null) { diagnostics.Add(Problem(DiagnosticCodes.EFontMissing, "表格文字在所选字体中缺字。", DiagnosticStage.Standard, p.SourceRef)); return null; }
                if (!LineComposer.TryCreate(body, out var cursor, out var error)) { diagnostics.Add(error!); return null; }
                while (cursor.Index < cursor.Atoms.Count)
                {
                    if (cancellation.IsCancellationRequested) { diagnostics.Add(Cancelled("表格测量已取消。")); return null; }
                    int before = cursor.Index;
                    var decision = composer.Next(cursor, available, 0, style, standard.MeasurementTolerance, cancellation);
                    if (decision.Error != null) { diagnostics.Add(decision.Error); return null; }
                    diagnostics.AddRange(decision.Warnings);
                    if (decision.Done) break;
                    if (decision.Spacer) lines.Add(null);
                    else if (decision.Line != null)
                    {
                        var line = decision.Line;
                        if (line.InkBounds.MinX < -1e-8 || line.InkBounds.MaxX > available + standard.MeasurementTolerance + 1e-8)
                        { diagnostics.Add(Problem(DiagnosticCodes.ENoLegalBreak, "表格单元格字形超出列宽。", DiagnosticStage.Layout, p.SourceRef)); return null; }
                        m.Ascent = Math.Max(m.Ascent, line.InkBounds.MaxY);
                        m.Descent = Math.Max(m.Descent, -line.InkBounds.MinY);
                        lines.Add(line);
                    }
                    if (cursor.Index <= before) { diagnostics.Add(Problem(DiagnosticCodes.ENoLegalBreak, "表格换行未消费内容。", DiagnosticStage.Layout, p.SourceRef)); return null; }
                }
            }
        }
        if (m.Ascent + m.Descent > standard.RowPitch + 1e-8)
        { diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "表格字形高度超过正文行距。", DiagnosticStage.Layout, null)); return null; }
        double vertical = standard.TableStyle.VerticalPaddingEm * standard.TextHeight;
        foreach (var cell in table.Cells.OrderBy(c => c.RowSpan))
        {
            int count = Math.Max(1, m.Text[cell].Count);
            double required = m.Ascent + m.Descent + 2 * vertical + (count - 1) * standard.RowPitch;
            int slots = Math.Max(1, (int)Math.Ceiling((required - 1e-8) / standard.RowPitch));
            int current = m.Heights.Skip(cell.Row).Take(cell.RowSpan).Sum();
            if (current < slots) m.Heights[cell.Row + cell.RowSpan - 1] += slots - current;
        }
        return m;
    }

    private static void EmitTableRange(TableData table, TableMetrics m, int start, int end, double top, int slot,
        LayoutColumn column, InstitutionStandard standard)
    {
        var y = new double[end - start + 1]; y[0] = top;
        for (int r = start; r < end; r++) y[r - start + 1] = y[r - start] - m.Heights[r] * standard.RowPitch;
        foreach (var cell in table.Cells.Where(c => c.Row >= start && c.Row < end))
        {
            double left = m.X[cell.Column], right = m.X[cell.Column + cell.ColumnSpan];
            double cellTop = y[cell.Row - start], bottom = y[cell.Row + cell.RowSpan - start];
            var baseline = cellTop - m.Ascent - standard.TableStyle!.VerticalPaddingEm * standard.TextHeight;
            int index = 0;
            foreach (var line in m.Text[cell])
            {
                if (line != null)
                {
                    var offset = left + standard.TableStyle.HorizontalPaddingEm * standard.TextHeight;
                    // 测量缓存可重复用于续栏表头，不修改缓存中的坐标。
                    var copy = new VisualLine { Text = line.Text, MeasuredWidth = line.MeasuredWidth, InkBounds = ShiftInk(line.InkBounds, offset), SourceSlices = line.SourceSlices };
                    foreach (var run in line.RenderRuns)
                        copy.RenderRuns.Add(new RenderRun { Text = run.Text, RelativeOrigin = new Point2 { X = run.RelativeOrigin.X + offset, Y = run.RelativeOrigin.Y }, BaselineOffset = run.BaselineOffset, ResolvedStyle = run.ResolvedStyle, MeasuredAdvance = run.MeasuredAdvance, InkBounds = ShiftInk(run.InkBounds, offset) });
                    column.TableTexts.Add(new RowSlot { RowIndex = slot + m.Heights.Skip(start).Take(cell.Row - start).Sum() + index, Baseline = baseline - index * standard.RowPitch, Occupancy = Occupancy.Text, VisualLine = copy });
                }
                index++;
            }
            if (cell.Top) AddEdge(column.Lines, left, cellTop, right, cellTop);
            if (cell.Bottom) AddEdge(column.Lines, left, bottom, right, bottom);
            if (cell.Left) AddEdge(column.Lines, left, bottom, left, cellTop);
            if (cell.Right) AddEdge(column.Lines, right, bottom, right, cellTop);
        }
    }

    private static Bounds2 ShiftInk(Bounds2 ink, double x) => new Bounds2 { MinX = ink.MinX + x, MaxX = ink.MaxX + x, MinY = ink.MinY, MaxY = ink.MaxY };

    // 合并共线且相接或重叠的边，包含合并单元格对接多个小格的情况。
    private static void AddEdge(List<LayoutLine> edges, double x1, double y1, double x2, double y2)
    {
        bool horizontal = Math.Abs(y1 - y2) < 1e-8;
        for (int i = edges.Count - 1; i >= 0; i--)
        {
            var e = edges[i];
            bool same = horizontal ? Math.Abs(e.Start.Y - y1) < 1e-8 && Math.Abs(e.End.Y - y1) < 1e-8
                : Math.Abs(e.Start.X - x1) < 1e-8 && Math.Abs(e.End.X - x1) < 1e-8;
            if (!same) continue;
            if (horizontal && e.End.X >= x1 - 1e-8 && e.Start.X <= x2 + 1e-8)
            { x1 = Math.Min(x1, e.Start.X); x2 = Math.Max(x2, e.End.X); edges.RemoveAt(i); }
            else if (!horizontal && e.End.Y >= y1 - 1e-8 && e.Start.Y <= y2 + 1e-8)
            { y1 = Math.Min(y1, e.Start.Y); y2 = Math.Max(y2, e.End.Y); edges.RemoveAt(i); }
        }
        edges.Add(new LayoutLine { Start = new Point2 { X = x1, Y = y1 }, End = new Point2 { X = x2, Y = y2 } });
    }
}
