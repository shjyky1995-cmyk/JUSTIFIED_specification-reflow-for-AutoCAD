using System.Security.Cryptography;
using System.Text.Json;
using System.IO.Compression;
using System.Xml.Linq;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Ports;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.Contracts.Templates;
using Justified.SpecificationReflow.AutoCAD.DocxAdapter;
using Justified.SpecificationReflow.AutoCAD.Standards;

if (args.Length is < 1 or > 3)
{
    Console.Error.WriteLine("Usage: T12Audit <docx-directory> [local-package-root] [report.json]");
    return 2;
}

var documentDirectory = Path.GetFullPath(args[0]);
if (!Directory.Exists(documentDirectory))
{
    Console.Error.WriteLine("Document directory does not exist.");
    return 2;
}

var map = new StyleMap();
Add(StyleMapMatch.StyleId, "Heading1", BlockType.Heading1);
Add(StyleMapMatch.StyleId, "Heading2", BlockType.Heading2);
Add(StyleMapMatch.StyleId, "Normal", BlockType.Paragraph);
Add(StyleMapMatch.Name, "heading 1", BlockType.Heading1);
Add(StyleMapMatch.Name, "heading 2", BlockType.Heading2);
Add(StyleMapMatch.Name, "标题 1", BlockType.Heading1);
Add(StyleMapMatch.Name, "标题 2", BlockType.Heading2);
Add(StyleMapMatch.Name, "正文", BlockType.Paragraph);
Add(StyleMapMatch.Name, "Normal", BlockType.Paragraph);

void Add(StyleMapMatch match, string key, BlockType target) =>
    map.Entries.Add(new StyleMapEntry { Match = match, Key = key, Target = target });

var parser = new DocxDocumentParser(new DocxParseOptions
{
    DocumentId = "t12-audit",
    DisciplineCode = "structure",
    StyleMap = map
});
var profile = new ParseProfile { Standard = new StandardRef { Id = "jsr-note", Version = "1.0.0" } };
var documents = new List<object>();
foreach (var path in Directory.EnumerateFiles(documentDirectory, "*.docx").OrderBy(Path.GetFileName, StringComparer.Ordinal))
{
    try
    {
        using var archive = ZipFile.OpenRead(path);
        var expandedBytes = archive.Entries.Sum(entry => entry.Length);
        var mainDocument = archive.GetEntry("word/document.xml")
            ?? throw new InvalidDataException("Missing word/document.xml");
        using var xml = mainDocument.Open();
        var root = XDocument.Load(xml);
        XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var sourceTextUnits = root.Descendants(word + "t").Sum(node => node.Value.Length);
        var tableCount = root.Descendants(word + "tbl").Count();
        var result = parser.Parse(new DocxFileSource(path), profile, CancellationToken.None);
        documents.Add(new
        {
            name = Path.GetFileName(path),
            sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
            sourceBytes = new FileInfo(path).Length,
            expandedBytes,
            zipEntries = archive.Entries.Count,
            sourceTextUtf16Units = sourceTextUnits,
            tables = tableCount,
            parseSuccess = result.Success,
            blocks = result.Document?.Blocks.Count,
            parsedRunUtf16Units = result.Document?.Blocks.Sum(block => block.Runs.Sum(run => run.Text.Length)),
            diagnostics = result.Diagnostics.GroupBy(item => item.Code)
                .Select(group => new { code = group.Key, count = group.Count() }).ToArray()
        });
    }
    catch (Exception error)
    {
        documents.Add(new { name = Path.GetFileName(path), failure = error.GetType().Name });
    }
}

object? package = null;
if (args.Length >= 2)
{
    var catalog = new DirectoryPackageCatalog(Path.GetFullPath(args[1]), allowTestFixtures: false);
    var standard = catalog.Load(new StandardRef { Id = "jsr-note", Version = "1.0.0" }, CancellationToken.None);
    var templates = new[] { "jsr-A1-three-column", "jsr-A2-three-column", "jsr-A3-two-column" }
        .Select(id =>
        {
            var loaded = catalog.Load(new TemplateRef { Id = id, Version = "1.0.0" }, CancellationToken.None);
            return new { id, success = loaded.Success, diagnostics = loaded.Diagnostics.Select(item => item.Code).Distinct().ToArray() };
        }).ToArray();
    package = new
    {
        standardSuccess = standard.Success,
        standardDiagnostics = standard.Diagnostics.Select(item => item.Code).Distinct().ToArray(),
        templates
    };
}

var report = new { generatedUtc = DateTimeOffset.UtcNow, documents, package };
var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
if (args.Length >= 3)
{
    var output = Path.GetFullPath(args[2]);
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    File.WriteAllText(output, json);
    Console.WriteLine("T12_AUDIT_WRITTEN " + output);
}
else
{
    Console.WriteLine(json);
}
return 0;
