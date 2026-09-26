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
            Console.Out.Write(JsonSerializer.Serialize(new WorkerResult(false, error.Message, null, 0, Array.Empty<WorkerDiagnostic>()), JsonOptions));
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
        var title = Required(request.Title, "说明标题", 120);
        var project = Required(request.ProjectName, "工程名称", 120);
        var discipline = Required(request.Discipline, "专业", 60);
        var sections = new[]
        {
            ("工程概况", request.Overview),
            ("设计依据", request.Basis),
            ("设计要求", request.Requirements),
            ("其他说明", request.Other)
        };
        if (sections.All(item => string.IsNullOrWhiteSpace(item.Item2)))
            throw new ArgumentException("请至少填写一个说明栏目。");
        foreach (var section in sections)
            if ((section.Item2?.Length ?? 0) > 100_000) throw new ArgumentException(section.Item1 + "内容过长。");

        var bytes = BuildDocx(title, project, discipline, sections);
        var parsed = Parse(new DocxBytesSource(Path.GetFileName(outputPath), bytes));
        if (!parsed.Success) return FromParse(parsed, null, "生成的 DOCX 未通过导入检查，文件没有保存。");

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
        return FromParse(parsed, outputPath, "DOCX 已生成，可用 Word/WPS 修改。");
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

    private static byte[] BuildDocx(string title, string project, string discipline, IEnumerable<(string Heading, string? Body)> sections)
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
            body.Append(Paragraph("Heading1", title));
            body.Append(Paragraph("Normal", "工程名称：" + project));
            body.Append(Paragraph("Normal", "专业：" + discipline));
            foreach (var (heading, content) in sections)
            {
                if (string.IsNullOrWhiteSpace(content)) continue;
                body.Append(Paragraph("Heading2", heading));
                foreach (var line in content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
                    body.Append(Paragraph("Normal", line));
            }
            body.Append(new SectionProperties());
            main.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(body);
            main.Document.Save();
            word.PackageProperties.Title = title;
        }
        return stream.ToArray();
    }

    private static Style MakeStyle(string id, string name, bool isDefault) =>
        new(new StyleName { Val = name }) { Type = StyleValues.Paragraph, StyleId = id, Default = isDefault };

    private static Paragraph Paragraph(string style, string text)
    {
        var paragraph = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = style }));
        if (text.Length > 0) paragraph.Append(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        return paragraph;
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

    private static WorkerResult FromParse(DocumentParseResult parsed, string? path, string message) =>
        new(parsed.Success, message, path, parsed.Document?.Blocks.Count ?? 0,
            parsed.Diagnostics.Take(30).Select(item => new WorkerDiagnostic(item.Code, item.Severity.ToString(), item.Message)).ToArray());
}

internal sealed class WorkerRequest
{
    public string Operation { get; set; } = string.Empty;
    public string? Path { get; set; }
    public string? Title { get; set; }
    public string? ProjectName { get; set; }
    public string? Discipline { get; set; }
    public string? Overview { get; set; }
    public string? Basis { get; set; }
    public string? Requirements { get; set; }
    public string? Other { get; set; }
}

internal sealed record WorkerResult(bool Success, string Message, string? Path, int Blocks, IReadOnlyList<WorkerDiagnostic> Diagnostics);
internal sealed record WorkerDiagnostic(string Code, string Severity, string Message);
