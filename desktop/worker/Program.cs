using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Ports;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.DocxAdapter;

namespace DocxWorkbench.Worker;

internal static class Program
{
    private const int MaxRequestCharacters = 400_000;
    private const int MaxSectionCharacters = 100_000;
    private const int MaxCorrosionCharacters = 20_000;
    private const int MaxLines = 5_000;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static int Main()
    {
        try
        {
            var input = ReadRequest();
            var request = JsonSerializer.Deserialize<WorkerRequest>(input, JsonOptions)
                ?? throw new ArgumentException("桌面请求为空。");
            var result = request.Operation switch
            {
                "generate" => Generate(request),
                "inspect" => Inspect(request.Path),
                _ => throw new ArgumentException("不支持的桌面操作。")
            };
            Console.Out.Write(JsonSerializer.Serialize(result, JsonOptions));
            return result.Success ? 0 : 1;
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException or JsonException or InvalidDataException or OpenXmlPackageException)
        {
            Console.Out.Write(JsonSerializer.Serialize(new WorkerResult(false, error.Message, null, 0, Array.Empty<string>(), Array.Empty<WorkerDiagnostic>()), JsonOptions));
            return 1;
        }
    }

    private static string ReadRequest()
    {
        using var stdin = Console.OpenStandardInput();
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int count;
        while ((count = stdin.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + count > MaxRequestCharacters * 4)
                throw new ArgumentException("填写内容过长，请缩短后再生成。");
            buffer.Write(chunk, 0, count);
        }
        var input = new UTF8Encoding(false, true).GetString(buffer.ToArray());
        if (input.Length > MaxRequestCharacters) throw new ArgumentException("填写内容过长，请缩短后再生成。");
        return input;
    }

    private static WorkerResult Generate(WorkerRequest request)
    {
        var outputPath = FullDocxPath(request.Path);
        if (File.Exists(outputPath)) throw new IOException("文件已存在，请换一个名称；不会覆盖你修改过的 DOCX。");
        var document = request.Document ?? throw new ArgumentException("说明内容为空。");
        if (document.ExportMode is not null and not "draft" and not "reviewed") throw new ArgumentException("导出方式无效。");
        var draft = document.ExportMode == "draft";
        var title = Required(document.Title, "说明标题", 120);
        var discipline = Required(document.Discipline, "专业", 60);
        var project = document.Project ?? new WorkerProject();
        var projectName = Parameter(project.Name, "工程名称", 120, draft);

        var lines = new List<(string Style, string Text)> { ("Heading1", title) };
        foreach (var notice in document.ReviewNotices ?? new List<string>()) lines.Add(("Normal", Trim(notice, 500, "资料状态")));
        if (document.LayoutMode != "source")
        {
            lines.Add(("Normal", "专业：" + discipline));
            lines.Add(("Normal", "工程名称：" + projectName));
            AddMeta(lines, "工程编号：", project.Number, 60);
            AddMeta(lines, "建设单位：", project.Owner, 120);
            AddMeta(lines, "建设地点：", project.Location, 120);
        }

        if (document.Structural is not null && document.LayoutMode != "source")
        {
            var structural = document.Structural;
            var site = Parameter(structural.SiteCategory, "场地类别", 20, draft);
            var grade = Parameter(structural.SeismicGrade, "抗震设防类别", 20, draft);
            var safety = Parameter(structural.SafetyLevel, "结构安全等级", 20, draft);
            var foundation = Parameter(structural.FoundationGrade, "地基基础设计等级", 20, draft);
            var scheme = Parameter(structural.ProtectionScheme, "材料/防腐方案", 20, draft);
            var intensity = Parameter(structural.SeismicIntensity, "抗震设防烈度", 24, draft);
            if (!draft && intensity == "待核定") throw new ArgumentException("请手工选择抗震设防烈度，或导出 Word 草稿。");
            if (!draft && !new[] { "6度（0.05g）", "7度（0.10g）", "7度（0.15g）", "8度（0.20g）", "8度（0.30g）", "9度（0.40g）" }.Contains(intensity))
                throw new ArgumentException("抗震设防烈度不在可选范围内，请重新选择。");
            var life = structural.DesignLifeYears > 0 ? structural.DesignLifeYears : 50;
            lines.Add(("Heading2", "结构设计参数"));
            lines.Add(("Normal", "场地类别：" + site));
            lines.Add(("Normal", "抗震设防类别：" + grade + "（设防烈度：" + intensity + "）"));
            lines.Add(("Normal", "结构安全等级：" + safety));
            lines.Add(("Normal", "地基基础设计等级：" + foundation));
            lines.Add(("Normal", "设计使用年限：" + life + " 年"));
            if (!string.IsNullOrWhiteSpace(structural.Corrosion))
                lines.Add(("Normal", "水土腐蚀性：" + Trim(structural.Corrosion, MaxCorrosionCharacters, "水土腐蚀性")));
            lines.Add(("Normal", "材料与防腐方案：" + scheme + (string.IsNullOrWhiteSpace(structural.ProtectionExtra) ? string.Empty : "（附加措施：" + Trim(structural.ProtectionExtra, 120, "附加措施") + "）")));
        }

        var sections = document.Sections ?? new List<WorkerSection>();
        if (sections.Count == 0) throw new ArgumentException("请至少选择一个章节。");
        var filled = 0;
        foreach (var section in sections)
        {
            var heading = Required(section.Title, "章节标题", 120);
            var body = section.Body ?? string.Empty;
            if (body.Trim().Length == 0 && (section.Blocks?.Count ?? 0) == 0) continue;
            if (body.Length > MaxSectionCharacters) throw new ArgumentException("章节「" + heading + "」内容过长。");
            filled += 1;
            lines.Add(("Heading2", heading));
            if (section.Blocks is { Count: > 0 })
            {
                if (section.Blocks.Count > MaxLines) throw new ArgumentException("章节「" + heading + "」内容块过多。");
                foreach (var block in section.Blocks)
                {
                    if (block.Kind == "paragraph") lines.Add(("Normal", Trim(block.Text ?? string.Empty, MaxSectionCharacters, "正文段落")));
                    else if (block.Kind == "table" && block.Rows is { Count: > 0 }) lines.Add(("TableJson", JsonSerializer.Serialize(block)));
                    else throw new ArgumentException("章节「" + heading + "」包含无效内容块。");
                }
            }
            else
            {
                var bodyLines = body.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Where(line => line.Trim().Length > 0).ToArray();
                if (bodyLines.Length > MaxLines) throw new ArgumentException("章节「" + heading + "」段落数过多。");
                foreach (var line in bodyLines) lines.Add(("Normal", line));
            }
        }
        if (filled == 0) throw new ArgumentException("所有章节都是空的，请至少填写一个章节正文。");

        var bytes = BuildDocx(lines);
        var hasTables = lines.Any(line => line.Style == "TableJson");
        var parsed = hasTables ? null : Parse(new DocxBytesSource(Path.GetFileName(outputPath), bytes));
        if (parsed is not null && !parsed.Success) return FromParse(parsed, null, "生成的 DOCX 未通过导入检查，文件没有保存。");

        var directory = Path.GetDirectoryName(outputPath)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, "." + Path.GetFileName(outputPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, outputPath);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        if (hasTables) return TableDocumentResult(outputPath, bytes);
        return FromParse(parsed!, outputPath, "DOCX 已生成，可用 Word/WPS 修改。");
    }

    private static void AddMeta(List<(string Style, string Text)> lines, string label, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        lines.Add(("Normal", label + Trim(value, maxLength, label.TrimEnd('：'))));
    }

    private static WorkerResult Inspect(string? path)
    {
        var fullPath = FullDocxPath(path);
        if (!File.Exists(fullPath)) throw new IOException("找不到 DOCX 文件。");
        var parsed = Parse(new DocxFileSource(fullPath));
        return FromParse(parsed, fullPath, parsed.Success ? "DOCX 内容可以交给 CAD 导入。" : "DOCX 存在需要修正的内容。");
    }

    private static string FullDocxPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("请选择 DOCX 文件位置。");
        var fullPath = Path.GetFullPath(path);
        if (!string.Equals(Path.GetExtension(fullPath), ".docx", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("请选择 .docx 文件。");
        return fullPath;
    }

    private static string Required(string? value, string label, int maxLength)
    {
        var result = value?.Trim() ?? string.Empty;
        if (result.Length == 0) throw new ArgumentException("请填写" + label + "。");
        if (result.Length > maxLength) throw new ArgumentException(label + "过长。");
        return result;
    }

    private static string Parameter(string? value, string label, int maxLength, bool draft)
    {
        if (draft && string.IsNullOrWhiteSpace(value)) return "【待填写：" + label + "】";
        return Required(value, label, maxLength);
    }

    private static string Trim(string value, int maxLength, string label)
    {
        var result = value.Trim();
        if (result.Length > maxLength) throw new ArgumentException(label + "过长。");
        return result;
    }

    private static byte[] BuildDocx(List<(string Style, string Text)> lines)
    {
        using var stream = new MemoryStream();
        using (var word = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = word.AddMainDocumentPart();
            var stylesPart = main.AddNewPart<StyleDefinitionsPart>();
            stylesPart.Styles = new Styles(
                MakeStyle("Normal", "正文", true),
                MakeStyle("Heading1", "标题 1", false),
                MakeStyle("Heading2", "标题 2", false));
            stylesPart.Styles.Save();
            var body = new Body();
            foreach (var (style, text) in lines)
                body.Append(style == "TableJson" ? MakeTable(JsonSerializer.Deserialize<WorkerBlock>(text) ?? throw new InvalidDataException("表格数据无效。")) : Paragraph(style, text));
            body.Append(new SectionProperties(new PageSize { Width = 11906, Height = 16838 }, new PageMargin { Top = 1417, Bottom = 1417, Left = 1134, Right = 1134 }));
            main.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(body);
            main.Document.Save();
            var errors = new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(word).Take(3).ToArray();
            if (errors.Length > 0) throw new InvalidDataException("DOCX 结构检查失败：" + string.Join("；", errors.Select(error => error.Description)));
            word.PackageProperties.Title = lines.Count > 0 ? lines[0].Text : null;
        }
        return stream.ToArray();
    }

    private static Style MakeStyle(string id, string name, bool isDefault)
    {
        var style = new Style(new StyleName { Val = name }) { Type = StyleValues.Paragraph, StyleId = id, Default = isDefault };
        var heading = id != "Normal";
        style.Append(new StyleParagraphProperties(new SpacingBetweenLines { Before = heading ? "240" : "0", After = heading ? "120" : "0", Line = "360", LineRule = LineSpacingRuleValues.Auto }));
        style.Append(new StyleRunProperties(new RunFonts { Ascii = "SimSun", HighAnsi = "SimSun", EastAsia = "宋体" }, new FontSize { Val = id == "Heading1" ? "32" : id == "Heading2" ? "26" : "21" }));
        return style;
    }

    private static Paragraph Paragraph(string style, string text)
    {
        var paragraph = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = style }));
        if (style is "Heading1" or "Heading2") paragraph.ParagraphProperties!.Append(new KeepNext());
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            if (lineIndex > 0) paragraph.Append(new Run(new Break()));
            var parts = lines[lineIndex].Split('\t');
            for (var partIndex = 0; partIndex < parts.Length; partIndex++)
            {
                if (partIndex > 0) paragraph.Append(new Run(new TabChar()));
                if (parts[partIndex].Length > 0) paragraph.Append(new Run(new Text(parts[partIndex]) { Space = SpaceProcessingModeValues.Preserve }));
            }
        }
        return paragraph;
    }

    private static Table MakeTable(WorkerBlock block)
    {
        var rows = block.Rows ?? throw new ArgumentException("表格缺少行。");
        if (rows.Count == 0 || rows.Count > 100 || rows.Any(row => row.Count == 0 || row.Count > 12 || row.Count != rows[0].Count)) throw new ArgumentException("表格行列数无效。");
        var ratios = block.ColumnWidths ?? Enumerable.Repeat(1d, rows[0].Count).ToList();
        if (ratios.Count != rows[0].Count || ratios.Any(value => !double.IsFinite(value) || value <= 0) || !double.IsFinite(ratios.Sum())) throw new ArgumentException("表格列宽无效。");
        const int availableWidth = 9638;
        var widths = ratios.Select(value => Math.Max(1, (int)Math.Floor(value / ratios.Sum() * availableWidth))).ToArray();
        widths[^1] = availableWidth - widths.Take(widths.Length - 1).Sum();
        if (widths[^1] <= 0) throw new ArgumentException("表格列宽比例无效。");
        var table = new Table(new TableProperties(
            new TableWidth { Width = availableWidth.ToString(), Type = TableWidthUnitValues.Dxa },
            new TableBorders(new TopBorder { Val = BorderValues.Single, Size = 4 }, new LeftBorder { Val = BorderValues.Single, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4 }, new RightBorder { Val = BorderValues.Single, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 }, new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }),
            new TableLayout { Type = TableLayoutValues.Fixed }));
        table.Append(new TableGrid(widths.Select(width => new GridColumn { Width = width.ToString() })));
        for (var r = 0; r < rows.Count; r++)
        {
            var properties = new TableRowProperties(new CantSplit());
            if (r == 0) properties.Append(new TableHeader());
            var tableRow = new TableRow(properties);
            for (var c = 0; c < rows[r].Count; c++)
            {
                var cell = new TableCell(new TableCellProperties(new TableCellWidth { Width = widths[c].ToString(), Type = TableWidthUnitValues.Dxa }));
                foreach (var line in Trim(rows[r][c], 10000, "表格单元格").Replace("\r\n", "\n").Split('\n')) cell.Append(Paragraph("Normal", line));
                tableRow.Append(cell);
            }
            table.Append(tableRow);
        }
        return table;
    }

    private static WorkerResult TableDocumentResult(string path, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var word = WordprocessingDocument.Open(stream, false);
        var body = word.MainDocumentPart?.Document.Body ?? throw new InvalidDataException("DOCX 缺少正文。");
        var headings = body.Elements<Paragraph>().Where(p => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value is "Heading1" or "Heading2").Select(p => p.InnerText).ToArray();
        var blocks = body.Elements().Count(element => element is DocumentFormat.OpenXml.Wordprocessing.Paragraph or Table);
        if (headings.Length < 2 || !body.Elements<Table>().Any()) throw new InvalidDataException("DOCX 章节或表格结构无效。");
        return new WorkerResult(true, "含可编辑表格的 DOCX 已生成；CAD 表格导入请使用另行验收的 T28 候选版。", path, blocks, headings,
            new[] { new WorkerDiagnostic("W_TABLE_CAD_UNSUPPORTED", "warning", "已发布 CAD v0.1.0 不支持表格；T28 候选版的表格导入仍需单独验收。") });
    }

    private static DocumentParseResult Parse(IDocumentSource source)
    {
        var map = new StyleMap();
        map.Entries.Add(new StyleMapEntry { Match = StyleMapMatch.StyleId, Key = "Normal", Target = BlockType.Paragraph });
        map.Entries.Add(new StyleMapEntry { Match = StyleMapMatch.StyleId, Key = "Heading1", Target = BlockType.Heading1 });
        map.Entries.Add(new StyleMapEntry { Match = StyleMapMatch.StyleId, Key = "Heading2", Target = BlockType.Heading2 });
        map.Entries.Add(new StyleMapEntry { Match = StyleMapMatch.Name, Key = "正文", Target = BlockType.Paragraph });
        map.Entries.Add(new StyleMapEntry { Match = StyleMapMatch.Name, Key = "标题 1", Target = BlockType.Heading1 });
        map.Entries.Add(new StyleMapEntry { Match = StyleMapMatch.Name, Key = "标题 2", Target = BlockType.Heading2 });
        var parser = new DocxDocumentParser(new DocxParseOptions
        {
            DocumentId = "desktop-check",
            DisciplineCode = "general",
            StyleMap = map,
            MaxSourceBytes = 20_000_000,
            MaxUncompressedBytes = 100_000_000
        });
        return parser.Parse(source, new ParseProfile { Standard = new StandardRef { Id = "jsr-note", Version = "1.0.0" } }, CancellationToken.None);
    }

    private static WorkerResult FromParse(DocumentParseResult parsed, string? path, string message)
    {
        var headings = parsed.Document?.Blocks
            .Where(block => block.Type == BlockType.Heading1 || block.Type == BlockType.Heading2)
            .Select(block => string.Concat(block.Runs.Select(run => run.Text)))
            .ToArray() ?? Array.Empty<string>();
        return new WorkerResult(parsed.Success, message, path, parsed.Document?.Blocks.Count ?? 0, headings,
            parsed.Diagnostics.Take(30).Select(item => new WorkerDiagnostic(item.Code, item.Severity.ToString(), item.Message)).ToArray());
    }
}

internal sealed class WorkerRequest
{
    public string Operation { get; set; } = string.Empty;
    public string? Path { get; set; }
    public WorkerDocument? Document { get; set; }
}

internal sealed class WorkerDocument
{
    public string? Title { get; set; }
    public string? Discipline { get; set; }
    public WorkerProject? Project { get; set; }
    public WorkerStructural? Structural { get; set; }
    public List<WorkerSection>? Sections { get; set; }
    public string? LayoutMode { get; set; }
    public string? ExportMode { get; set; }
    public List<string>? ReviewNotices { get; set; }
}

internal sealed class WorkerProject
{
    public string? Name { get; set; }
    public string? Number { get; set; }
    public string? Owner { get; set; }
    public string? Location { get; set; }
}

internal sealed class WorkerStructural
{
    public string? SiteCategory { get; set; }
    public string? SeismicGrade { get; set; }
    public string? SafetyLevel { get; set; }
    public string? FoundationGrade { get; set; }
    public int DesignLifeYears { get; set; }
    public string? Corrosion { get; set; }
    public string? ProtectionScheme { get; set; }
    public string? ProtectionExtra { get; set; }
    public string? SeismicIntensity { get; set; }
}

internal sealed class WorkerSection
{
    public string? Title { get; set; }
    public string? Body { get; set; }
    public List<WorkerBlock>? Blocks { get; set; }
}

internal sealed class WorkerBlock
{
    public string? Kind { get; set; }
    public string? Text { get; set; }
    public List<List<string>>? Rows { get; set; }
    public List<double>? ColumnWidths { get; set; }
}

internal sealed record WorkerResult(bool Success, string Message, string? Path, int Blocks, IReadOnlyList<string> Headings, IReadOnlyList<WorkerDiagnostic> Diagnostics);
internal sealed record WorkerDiagnostic(string Code, string Severity, string Message);
