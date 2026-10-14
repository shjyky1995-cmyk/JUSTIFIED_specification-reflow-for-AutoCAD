using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocxWorkbench.Worker;

// 投标专用操作，不经过 CAD 排版/解析协议，不改变设计说明导出。
internal static class BidDocx
{
    private static string Required(string? text, int max = 100_000)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > max) throw new ArgumentException("投标正文为空或超过长度限制。");
        return text;
    }

    internal static WorkerResult Extract(string? source)
    {
        var path = Path.GetFullPath(Required(source, 1000));
        if (Path.GetExtension(path).ToLowerInvariant() != ".docx" || new FileInfo(path).Length > 20_000_000) throw new ArgumentException("请选择20MB以内的DOCX。");
        using (var archive = ZipFile.OpenRead(path))
        {
            if (archive.Entries.Count > 5000 || archive.Entries.Sum(e => e.Length) > 100_000_000) throw new ArgumentException("DOCX解压内容超过限额。");
        }
        using var word = WordprocessingDocument.Open(path, false);
        var body = word.MainDocumentPart?.Document.Body ?? throw new InvalidDataException("DOCX缺少正文。");
        var blocks = new List<BidSourceBlock>();
        var warnings = new List<string>();
        void Add(string location, string text)
        {
            if (text.Length > 50_000 || blocks.Count >= 3000) throw new ArgumentException("资料段落过长或过多，请拆分文件。");
            if (!string.IsNullOrWhiteSpace(text)) blocks.Add(new(location, text));
        }
        int p = 0, t = 0;
        foreach (var item in body.Elements())
        {
            if (item is Paragraph paragraph) Add("正文段落 " + (++p), Plain(paragraph));
            else if (item is Table table)
            {
                t++; int r = 0;
                foreach (var row in table.Elements<TableRow>())
                {
                    r++; int c = 0;
                    foreach (var cell in row.Elements<TableCell>()) Add($"表 {t} / 行 {r} / 单元格 {++c}", string.Join("\n", cell.Elements<Paragraph>().Select(Plain)));
                }
                if (table.Descendants<Table>().Any()) warnings.Add($"表{t}含嵌套表格，未完整提取，请在原文件核对。");
                if (table.Descendants<GridSpan>().Any() || table.Descendants<VerticalMerge>().Any()) warnings.Add($"表{t}含合并单元格，位置按实际单元格顺序计数，需对照原表。");
            }
            else if (item is not SectionProperties) warnings.Add("存在内容控件等未完整提取的正文对象，请打开原文件核对。");
        }
        var unsupported = body.Descendants().Any(e => e.LocalName is "drawing" or "pict" or "object" or "altChunk" or "ins" or "del" or "footnoteReference" or "oMath" or "fldChar");
        if (unsupported) warnings.Add("图片、公式、修订、域或脚注未可靠提取；文字预览不是完整原文件。");
        if (body.Descendants<NumberingProperties>().Any()) warnings.Add("自动编号未展开，请按原文段落和表格位置核对。");
        if (word.MainDocumentPart!.HeaderParts.Any() || word.MainDocumentPart.FooterParts.Any()) warnings.Add("页眉页脚未列入正文摘录，请人工核对。");
        if (!blocks.Any()) warnings.Add("未提取到正文，请在原文件中人工摘录要求。");
        if (blocks.Sum(b => b.Text.Length) > 600_000) throw new ArgumentException("资料全文过长，请拆分后导入。");
        return new WorkerResult(true, "已提取文字和简单表格；仍需人工核对。", path, blocks.Count, Array.Empty<string>(), warnings.Distinct().Select(w => new WorkerDiagnostic("BID_SOURCE_REVIEW", "warning", w)).ToArray()) { SourceBlocks = blocks };
    }

    private static string Plain(Paragraph paragraph) => string.Concat(paragraph.Descendants().Select(e => e switch { Text text => text.Text, TabChar => "\t", Break => "\n", _ => "" }));

    internal static WorkerResult Export(WorkerRequest request)
    {
        var data = request.BidDocument ?? throw new ArgumentException("缺少投标正文。");
        if (data.Mode is not "draft" and not "reviewed") throw new ArgumentException("导出方式无效。");
        var path = Path.GetFullPath(Required(request.Path, 1000));
        if (Path.GetExtension(path).ToLowerInvariant() != ".docx") throw new ArgumentException("请选择DOCX路径。");
        if (File.Exists(path)) throw new IOException("文件已存在，请使用新名称；不会覆盖已有Word。");
        if (data.Sections.Count is < 1 or > 100) throw new ArgumentException("章节数量无效。");
        using var stream = new MemoryStream();
        using (var word = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = word.AddMainDocumentPart();
            var styles = main.AddNewPart<StyleDefinitionsPart>();
            styles.Styles = new Styles(Style("Normal", "正文", 22), Style("Heading1", "标题 1", 32), Style("Heading2", "标题 2", 28));
            var body = new Body();
            body.Append(Paragraph(Required(data.Title, 160), "Heading1"));
            body.Append(Paragraph(data.Mode == "draft" ? "编制草稿 · 待核对" : "已核对稿 · 待最终签章提交"));
            foreach (var text in data.Meta) body.Append(Paragraph(Required(text, 5000)));
            body.Append(Paragraph("编制状态与待完善事项", "Heading2"));
            foreach (var text in data.Notices) body.Append(Paragraph(Required(text, 5000)));
            body.Append(Paragraph("目录（章节清单）", "Heading2"));
            foreach (var (s,i) in data.Sections.Select((s,i) => (s,i))) body.Append(Paragraph($"{i+1}. {Required(s.Title, 160)}"));
            foreach (var (section,i) in data.Sections.Select((s,i) => (s,i)))
            {
                body.Append(Paragraph($"{i+1}. {Required(section.Title, 160)}", "Heading2"));
                if (section.Blocks.Count > 5000) throw new ArgumentException("章节块数量过多。");
                foreach (var block in section.Blocks)
                {
                    if (block.Kind == "paragraph") body.Append(Paragraph(block.Text ?? ""));
                    else if (block.Kind == "table") body.Append(Table(block.Rows ?? throw new ArgumentException("表格为空。")));
                    else throw new ArgumentException("投标包含不支持的内容块。");
                }
            }
            foreach (var appendix in data.Appendix)
            {
                body.Append(Paragraph(Required(appendix.Title, 160), "Heading2"));
                // 长对照表分组；每组重复列头，不静默丢行。
                foreach (var rows in appendix.Rows.Skip(1).Chunk(90)) body.Append(Table(new[] { appendix.Rows[0] }.Concat(rows).ToList()));
                if (appendix.Rows.Count <= 1) body.Append(Paragraph("暂无条目，待核对。"));
            }
            body.Append(new SectionProperties(new PageSize { Width = 11906, Height = 16838 }, new PageMargin { Top = 1440, Bottom = 1440, Left = 1440, Right = 1440 }));
            main.Document = new Document(body); main.Document.Save(); styles.Styles.Save();
            word.PackageProperties.Title = data.Title;
            var errors = new OpenXmlValidator().Validate(word).Take(3).ToArray();
            if (errors.Length > 0) throw new InvalidDataException("投标DOCX结构不符合规范：" + string.Join("；", errors.Select(e => e.Description)));
        }
        var bytes = stream.ToArray();
        using (var verify = WordprocessingDocument.Open(new MemoryStream(bytes), false))
        {
            var headings = verify.MainDocumentPart!.Document.Body!.Elements<Paragraph>().Count(p => p.ParagraphProperties?.ParagraphStyleId?.Val == "Heading2");
            if (headings != data.Sections.Count + data.Appendix.Count + 2) throw new InvalidDataException("投标章节读回不一致。");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllBytes(temporary, bytes);
        File.Move(temporary, path); // 不覆盖现有文件
        return new WorkerResult(true, "可编辑投标DOCX已导出；证明附件另行整理。", path, data.Sections.Count, data.Sections.Select(s => s.Title ?? "").ToArray(), Array.Empty<WorkerDiagnostic>());
    }
    private static Style Style(string id, string name, int size) => new(new StyleName { Val = name }, new StyleParagraphProperties(new SpacingBetweenLines { After = "120", Line = "360", LineRule = LineSpacingRuleValues.Auto }), new StyleRunProperties(new RunFonts { Ascii = "SimSun", EastAsia = "宋体" }, new FontSize { Val = size.ToString() })) { Type = StyleValues.Paragraph, StyleId = id, Default = id == "Normal" };
    private static Paragraph Paragraph(string text, string style = "Normal")
    {
        if (text.Length > 100_000) throw new ArgumentException("正文段落过长。");
        var p = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = style }));
        if (style != "Normal") p.ParagraphProperties!.Append(new KeepNext());
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (int i=0; i<lines.Length; i++) { if (i>0) p.Append(new Run(new Break())); p.Append(new Run(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve })); }
        return p;
    }
    private static Table Table(List<List<string>> rows)
    {
        if (rows.Count is < 1 or > 100 || rows[0].Count is < 1 or > 12 || rows.Any(r => r.Count != rows[0].Count)) throw new ArgumentException("表格行列不一致或超过100行/12列。");
        int width = 9026 / rows[0].Count;
        var t = new Table(new TableProperties(new TableWidth { Width = "9026", Type = TableWidthUnitValues.Dxa }, new TableBorders(new TopBorder { Val = BorderValues.Single }, new LeftBorder { Val = BorderValues.Single }, new BottomBorder { Val = BorderValues.Single }, new RightBorder { Val = BorderValues.Single }, new InsideHorizontalBorder { Val = BorderValues.Single }, new InsideVerticalBorder { Val = BorderValues.Single }), new TableLayout { Type = TableLayoutValues.Fixed }));
        t.Append(new TableGrid(rows[0].Select(_ => new GridColumn { Width = width.ToString() })));
        for (int i=0; i<rows.Count; i++) { var properties = new TableRowProperties(); if(i==0) properties.Append(new TableHeader()); var row = new TableRow(properties); foreach (var cell in rows[i]) row.Append(new TableCell(new TableCellProperties(new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa }), Paragraph(cell))); t.Append(row); }
        return t;
    }
}
internal sealed record BidSourceBlock(string Location, string Text);
internal sealed class BidExportDocument
{
    public string? Title { get; set; }
    public string? Mode { get; set; }
    public List<string> Meta { get; set; } = new();
    public List<string> Notices { get; set; } = new();
    public List<BidExportSection> Sections { get; set; } = new();
    public List<BidAppendix> Appendix { get; set; } = new();
}
internal sealed class BidExportSection { public string? Title { get; set; } public List<WorkerBlock> Blocks { get; set; } = new(); }
internal sealed class BidAppendix { public string? Title { get; set; } public List<List<string>> Rows { get; set; } = new(); }
