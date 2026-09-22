using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Justified.SpecificationReflow.AutoCAD.Application;
using Justified.SpecificationReflow.AutoCAD.AutoCadAdapter;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Geometry;
using Justified.SpecificationReflow.AutoCAD.Contracts.Layout;
using Justified.SpecificationReflow.AutoCAD.Contracts.Rendering;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.Contracts.Templates;
using Justified.SpecificationReflow.AutoCAD.DocxAdapter;
using Justified.SpecificationReflow.AutoCAD.LayoutEngine;
using Justified.SpecificationReflow.AutoCAD.Standards;
using CadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Newtonsoft.Json;

[assembly: CommandClass(typeof(Justified.SpecificationReflow.AutoCAD.PluginHost.NoteCommands))]

namespace Justified.SpecificationReflow.AutoCAD.PluginHost;

// 正式两步：DN_NOTE_SET 设一次标准、图幅、单位比例和说明文档并记住在当前图；
// DN_NOTE 日常只点一次位置。只接受 classification=production 的已发布标准包，草案包只能走 DN_NOTE_DEV。
public class NoteCommands
{
    [CommandMethod("DN_NOTE_SET", CommandFlags.Modal)]
    public void SetSettings()
    {
        var document = CadApplication.DocumentManager.MdiActiveDocument;
        if (document == null) return;
        var editor = document.Editor;
        try
        {
            var defaultRoot = DefaultStandardRoot();
            editor.WriteMessage("\nDN_NOTE_SET_HINT 默认标准包根目录：" + defaultRoot + "\n");
            editor.WriteMessage("\nDN_NOTE_SET_HINT 直接回车使用默认目录，或粘贴其他包根目录（含 standards 与 templates 两个子目录）。\n");

            var rootInput = editor.GetString(new PromptStringOptions("\n标准包根目录") { AllowSpaces = true });
            if (rootInput.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\nDN_NOTE_SET_CANCELLED\n");
                return;
            }

            var root = string.IsNullOrWhiteSpace(rootInput.StringResult)
                ? defaultRoot
                : rootInput.StringResult.Trim().Trim('"');
            if (!Directory.Exists(root))
            {
                editor.WriteMessage("\nDN_NOTE_SET_FAILED 找不到标准包根目录 " + root + "。\n");
                return;
            }

            var list = new DirectoryPackageCatalog(root, allowTestFixtures: false).ListTemplates(System.Threading.CancellationToken.None);
            foreach (var diagnostic in list.Diagnostics.Where(item => item.Severity == Severity.Error))
                editor.WriteMessage("\nDN_NOTE_SET_WARN " + diagnostic.Code + " " + diagnostic.Message);
            var usable = list.Templates
                .Where(item => string.Equals(item.Classification, "production", StringComparison.OrdinalIgnoreCase)
                    && item.Calibrated
                    && !string.IsNullOrWhiteSpace(item.TemplateId)
                    && !string.IsNullOrWhiteSpace(item.Version))
                .ToList();
            if (usable.Count == 0)
            {
                editor.WriteMessage("\nDN_NOTE_SET_FAILED 该目录没有 classification=production 且已标定的模板。正式命令不接受草案包；开发核对请用 DN_NOTE_DEV。\n");
                return;
            }

            var paperPrompt = new PromptKeywordOptions("\n图幅");
            paperPrompt.Keywords.Add("A1");
            paperPrompt.Keywords.Add("A2");
            paperPrompt.Keywords.Add("A3");
            paperPrompt.Keywords.Default = "A1";
            paperPrompt.AllowNone = true;
            var paper = editor.GetKeywords(paperPrompt);
            if (paper.Status != PromptStatus.OK && paper.Status != PromptStatus.None)
            {
                editor.WriteMessage("\nDN_NOTE_SET_CANCELLED\n");
                return;
            }

            var paperCode = paper.Status == PromptStatus.None || string.IsNullOrWhiteSpace(paper.StringResult) ? "A1" : paper.StringResult;
            var candidates = usable
                .Where(item => string.Equals(item.PaperCode, paperCode, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (candidates.Count == 0)
            {
                editor.WriteMessage("\nDN_NOTE_SET_FAILED 标准包里没有 " + paperCode + " 模板。可用图幅："
                    + string.Join("、", usable.Select(item => string.IsNullOrWhiteSpace(item.PaperCode) ? "未知" : item.PaperCode).Distinct()) + "\n");
                return;
            }

            for (var i = 0; i < candidates.Count; i++)
                editor.WriteMessage("\nDN_NOTE_SET_TEMPLATE " + (i + 1) + " " + candidates[i].TemplateId + " " + candidates[i].Version
                    + " paper=" + candidates[i].PaperCode + " discipline=" + (string.IsNullOrWhiteSpace(candidates[i].DisciplineCode) ? "未填" : candidates[i].DisciplineCode));

            var indexInput = editor.GetString(new PromptStringOptions("\n模板序号，回车 1") { AllowSpaces = false });
            if (indexInput.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\nDN_NOTE_SET_CANCELLED\n");
                return;
            }

            var index = 0;
            var indexText = indexInput.StringResult?.Trim() ?? string.Empty;
            if (indexText.Length > 0 && (!int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out index) || index < 1 || index > candidates.Count))
            {
                editor.WriteMessage("\nDN_NOTE_SET_FAILED 模板序号必须是 1 到 " + candidates.Count + " 的整数。\n");
                return;
            }

            var chosen = candidates[index <= 0 ? 0 : index - 1];

            var scaleInput = editor.GetDouble(new PromptDoubleOptions("\n单位比例 unitScale，1 个标准毫米对应多少图形单位")
            {
                AllowNegative = false,
                AllowZero = false,
                AllowNone = false
            });
            if (scaleInput.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\nDN_NOTE_SET_CANCELLED\n");
                return;
            }

            var file = editor.GetFileNameForOpen(new PromptOpenFileOptions("选择说明 DOCX")
            {
                Filter = "Word 文档 (*.docx)|*.docx",
                DialogCaption = "选择说明"
            });
            if (file.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\nDN_NOTE_SET_CANCELLED\n");
                return;
            }

            var reportInput = editor.GetString(new PromptStringOptions("\n运行报告目录，可空；直接回车表示不写运行报告") { AllowSpaces = true });
            if (reportInput.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\nDN_NOTE_SET_CANCELLED\n");
                return;
            }

            var reportDirectory = reportInput.StringResult?.Trim().Trim('"') ?? string.Empty;

            var loaded = LoadProductionPackage(root, chosen.TemplateId, chosen.Version);
            if (loaded.Standard == null || loaded.Template == null)
            {
                foreach (var diagnostic in loaded.Diagnostics.Where(item => item.Severity == Severity.Error))
                    editor.WriteMessage("\nDN_NOTE_SET_FAILED " + diagnostic.Code + " " + diagnostic.Message);
                return;
            }

            var settings = new NoteSettings
            {
                StandardRoot = Path.GetFullPath(root),
                StandardId = loaded.Template.StandardRef.Id ?? string.Empty,
                StandardVersion = loaded.Template.StandardRef.Version ?? string.Empty,
                TemplateId = loaded.Template.TemplateId,
                TemplateVersion = loaded.Template.Version,
                PaperCode = paperCode,
                UnitScale = scaleInput.Value,
                DocumentPath = file.StringResult,
                ReportDirectory = string.IsNullOrEmpty(reportDirectory) ? null : reportDirectory,
                SavedUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };
            var problems = settings.Problems();
            if (problems.Count > 0)
            {
                foreach (var problem in problems)
                    editor.WriteMessage("\nDN_NOTE_SET_FAILED " + problem);
                return;
            }

            var saveError = new DrawingSettingsStore().Save(document.Database, NoteSettingsCodec.Encode(settings));
            if (saveError != null)
            {
                editor.WriteMessage("\nDN_NOTE_SET_FAILED " + saveError.Code + " " + saveError.Message + "\n");
                return;
            }

            editor.WriteMessage("\nDN_NOTE_SET_OK 设置已记住在当前图。日常生成请执行 DN_NOTE，点一次说明区右上角。\n");
            editor.WriteMessage("\nDN_NOTE_SET_DETAIL standard=" + settings.StandardId + " " + settings.StandardVersion
                + " template=" + settings.TemplateId + " " + settings.TemplateVersion
                + " paper=" + settings.PaperCode
                + " scale=" + Format(settings.UnitScale)
                + " docx=" + settings.DocumentPath
                + " report=" + (settings.ReportDirectory ?? "不写报告")
                + " root=" + settings.StandardRoot + "\n");
        }
        catch (System.Exception error)
        {
            editor.WriteMessage("\nDN_NOTE_SET_FAILED " + error.GetType().Name + " " + error.Message + "\n");
        }
    }

    [CommandMethod("DN_NOTE", CommandFlags.Modal)]
    public void Note()
    {
        var document = CadApplication.DocumentManager.MdiActiveDocument;
        if (document == null) return;
        var editor = document.Editor;
        var database = document.Database;
        var before = Count(database);
        var started = DateTimeOffset.UtcNow;
        try
        {
            var chunks = new DrawingSettingsStore().Load(database);
            var settings = chunks == null ? null : NoteSettingsCodec.Decode(chunks);
            if (settings == null)
            {
                editor.WriteMessage("\nDN_NOTE_SET_REQUIRED 本图还没有工程设置。请先执行 DN_NOTE_SET 记住标准、图幅、单位比例和说明文档。\n");
                editor.WriteMessage("\nDN_NOTE_ENTITIES before=" + before + " after=" + Count(database) + "\n");
                return;
            }

            if (!File.Exists(settings.DocumentPath))
            {
                editor.WriteMessage("\nDN_NOTE_FAILED " + DiagnosticCodes.EDocxRead + " 说明文档不存在：" + settings.DocumentPath + "。请重新 DN_NOTE_SET。\n");
                editor.WriteMessage("\nDN_NOTE_ENTITIES before=" + before + " after=" + Count(database) + "\n");
                return;
            }

            // 先做完全部会在点选前失败的检查，再请设计人员点位置。
            var loaded = LoadProductionPackage(settings.StandardRoot, settings.TemplateId, settings.TemplateVersion);
            if (loaded.Standard == null || loaded.Template == null)
            {
                foreach (var diagnostic in loaded.Diagnostics.Where(item => item.Severity == Severity.Error))
                    editor.WriteMessage("\nDN_NOTE_FAILED " + diagnostic.Code + " " + diagnostic.Message);
                editor.WriteMessage("\nDN_NOTE_FAILED 设置引用的标准包已变化。请重新执行 DN_NOTE_SET。\n");
                editor.WriteMessage("\nDN_NOTE_ENTITIES before=" + before + " after=" + Count(database) + "\n");
                return;
            }

            var standard = loaded.Standard;
            var template = loaded.Template;
            if (!string.Equals(standard.StandardId, settings.StandardId, StringComparison.Ordinal)
                || !string.Equals(standard.Version, settings.StandardVersion, StringComparison.Ordinal)
                || !string.Equals(template.TemplateId, settings.TemplateId, StringComparison.Ordinal)
                || !string.Equals(template.Version, settings.TemplateVersion, StringComparison.Ordinal))
            {
                editor.WriteMessage("\nDN_NOTE_FAILED 标准包与记住的设置不是同一次发布。请重新执行 DN_NOTE_SET。\n");
                editor.WriteMessage("\nDN_NOTE_ENTITIES before=" + before + " after=" + Count(database) + "\n");
                return;
            }

            var fontProblems = new PackageValidator().ValidateFonts(standard, identity => FindFont(database, identity) != null);
            if (fontProblems.Any(item => item.Severity == Severity.Error))
            {
                foreach (var diagnostic in fontProblems.Where(item => item.Severity == Severity.Error))
                    editor.WriteMessage("\nDN_NOTE_FAILED " + diagnostic.Code + " " + diagnostic.Message);
                editor.WriteMessage("\nDN_NOTE_ENTITIES before=" + before + " after=" + Count(database) + "\n");
                return;
            }

            var point = editor.GetPoint(new PromptPointOptions("\n点选说明区右上角"));
            if (point.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\nDN_NOTE_CANCELLED\n");
                return;
            }

            var wcs = point.Value.TransformBy(editor.CurrentUserCoordinateSystem);
            editor.WriteMessage("\nDN_NOTE_ANCHOR_WCS " + Format(wcs.X) + "," + Format(wcs.Y) + "," + Format(wcs.Z));
            editor.WriteMessage("\nDN_NOTE_STANDARD " + standard.StandardId + " " + standard.Version);
            editor.WriteMessage("\nDN_NOTE_TEMPLATE " + template.TemplateId + " " + template.Version);
            editor.WriteMessage("\nDN_NOTE_SCALE " + Format(settings.UnitScale));

            NoteGenerationResult generated;
            using (document.LockDocument())
            {
                var service = new NoteGenerationService(
                    new DocxDocumentParser(new DocxParseOptions
                    {
                        DocumentId = "dn-note",
                        DisciplineCode = string.IsNullOrWhiteSpace(template.DisciplineCode) ? "structure" : template.DisciplineCode,
                        StyleMap = Map()
                    }),
                    new SpecificationLayoutEngine(),
                    new HostTextMeasureService(database));
                generated = service.Generate(
                    new DocxFileSource(settings.DocumentPath),
                    standard,
                    template,
                    new RenderTransform
                    {
                        AnchorWcs = new Point2 { X = wcs.X, Y = wcs.Y },
                        UnitScale = settings.UnitScale,
                        TargetSpace = TargetSpace.Model
                    },
                    System.Threading.CancellationToken.None,
                    GenerationLimits.Default);
            }

            foreach (var diagnostic in generated.Diagnostics.Where(item => item.Severity == Severity.Warning))
                editor.WriteMessage("\nDN_NOTE_WARN " + diagnostic.Code + " " + diagnostic.Message);
            if (!generated.Success)
            {
                foreach (var diagnostic in generated.Diagnostics.Where(item => item.Severity == Severity.Error))
                    editor.WriteMessage("\nDN_NOTE_FAILED " + diagnostic.Code + " " + diagnostic.Message);
                editor.WriteMessage("\nDN_NOTE_ENTITIES before=" + before + " after=" + Count(database) + "\n");
                return;
            }

            if (generated.Diagnostics.Any(item => item.Severity == Severity.Warning))
            {
                var confirm = new PromptKeywordOptions("\n有警告。仍要写入吗");
                confirm.Keywords.Add("是");
                confirm.Keywords.Add("否");
                confirm.Keywords.Default = "否";
                confirm.AllowNone = true;
                var answer = editor.GetKeywords(confirm);
                if (answer.Status != PromptStatus.OK || answer.StringResult != "是")
                {
                    editor.WriteMessage("\nDN_NOTE_CANCELLED\n");
                    return;
                }
            }

            editor.WriteMessage("\nDN_NOTE_SUMMARY pages=" + (generated.Layout == null ? 0 : generated.Layout.Pages.Count)
                + " objects=" + generated.Texts.Count
                + " warnings=" + generated.Diagnostics.Count(item => item.Severity == Severity.Warning));
            if (generated.Texts.Count > 0)
            {
                var first = generated.Texts[0];
                var preview = first.Text.Length <= 40 ? first.Text : first.Text.Substring(0, 40);
                editor.WriteMessage("\nDN_NOTE_FIRST x=" + Format(first.Position.X) + " y=" + Format(first.Position.Y)
                    + " h=" + Format(first.Height) + " widthFactor=" + Format(first.WidthFactor) + " text=" + preview);
            }

            RenderReport report;
            using (document.LockDocument())
                report = new DbTextWriter().Write(database, generated.Texts, wcs.Z, System.Threading.CancellationToken.None);
            var after = Count(database);
            if (!report.Success)
            {
                foreach (var diagnostic in report.Diagnostics)
                    editor.WriteMessage("\nDN_NOTE_FAILED " + diagnostic.Code + " " + diagnostic.Message);
                editor.WriteMessage("\nDN_NOTE_ENTITIES before=" + before + " after=" + after + "\n");
                return;
            }

            var finished = DateTimeOffset.UtcNow;
            var runReport = NoteRunReportBuilder.Create(
                document.Name,
                settings,
                standard,
                template,
                generated,
                new RenderReportResult
                {
                    Success = report.Success,
                    Committed = report.Success && report.ObjectCount > 0,
                    Pages = report.PageCount,
                    Objects = report.ObjectCount,
                    Diagnostics = report.Diagnostics
                },
                started,
                finished);
            WriteRunReport(editor, settings, runReport);

            editor.WriteMessage("\nDN_NOTE_ENTITIES before=" + before + " after=" + after);
            editor.WriteMessage("\nDN_NOTE_OK objects=" + report.ObjectCount);
            editor.WriteMessage("\nDN_NOTE_UNDO 一次 U 撤销本次提交。已有同名样式不会被修改，旧文字也不会被删除。\n");
        }
        catch (System.Exception error)
        {
            editor.WriteMessage("\nDN_NOTE_FAILED " + error.GetType().Name + " " + error.Message);
            editor.WriteMessage("\nDN_NOTE_ENTITIES before=" + before + " after=" + Count(database) + "\n");
        }
    }

    private static void WriteRunReport(Editor editor, NoteSettings settings, NoteRunReport report)
    {
        if (string.IsNullOrWhiteSpace(settings.ReportDirectory)) return;
        try
        {
            Directory.CreateDirectory(settings.ReportDirectory);
            var name = "dn-note-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + ".json";
            var path = Path.Combine(settings.ReportDirectory, name);
            File.WriteAllText(path, JsonConvert.SerializeObject(report, Formatting.Indented));
            editor.WriteMessage("\nDN_NOTE_REPORT " + path + "\n");
        }
        catch (System.Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
        {
            editor.WriteMessage("\nDN_NOTE_REPORT_WARN 运行报告没写成：" + error.Message + "。生成结果本身已完成。\n");
        }
    }

    private static ProductionPackage LoadProductionPackage(string root, string templateId, string templateVersion)
    {
        var diagnostics = new List<Diagnostic>();
        if (!Directory.Exists(root))
        {
            diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "找不到标准包根目录 " + root + "。"));
            return new ProductionPackage(null, null, diagnostics);
        }

        var catalog = new DirectoryPackageCatalog(root, allowTestFixtures: false);
        var templateLoad = catalog.Load(new TemplateRef { Id = templateId, Version = templateVersion }, System.Threading.CancellationToken.None);
        diagnostics.AddRange(templateLoad.Diagnostics);
        if (!templateLoad.Success || templateLoad.Template == null) return new ProductionPackage(null, null, diagnostics);
        var standardLoad = catalog.Load(new StandardRef
        {
            Id = templateLoad.Template.StandardRef.Id ?? string.Empty,
            Version = templateLoad.Template.StandardRef.Version ?? string.Empty
        }, System.Threading.CancellationToken.None);
        diagnostics.AddRange(standardLoad.Diagnostics);
        if (!standardLoad.Success || standardLoad.Standard == null) return new ProductionPackage(null, null, diagnostics);
        return new ProductionPackage(standardLoad.Standard, templateLoad.Template, diagnostics);
    }

    private static string DefaultStandardRoot()
    {
        var codeBase = typeof(NoteCommands).Assembly.Location;
        var directory = string.IsNullOrEmpty(codeBase) ? AppDomain.CurrentDomain.BaseDirectory : Path.GetDirectoryName(codeBase);
        return Path.Combine(directory ?? AppDomain.CurrentDomain.BaseDirectory, "standards", "published");
    }

    private static string? FindFont(Database database, string identity)
    {
        try
        {
            var found = Autodesk.AutoCAD.DatabaseServices.HostApplicationServices.Current.FindFile(identity, database, FindFileHint.FontFile);
            return string.IsNullOrWhiteSpace(found) ? null : found;
        }
        catch (System.Exception)
        {
            return null;
        }
    }

    internal static StyleMap Map()
    {
        var map = new StyleMap();
        Add(map, StyleMapMatch.StyleId, "Heading1", BlockType.Heading1);
        Add(map, StyleMapMatch.StyleId, "Heading2", BlockType.Heading2);
        Add(map, StyleMapMatch.StyleId, "Normal", BlockType.Paragraph);
        Add(map, StyleMapMatch.Name, "heading 1", BlockType.Heading1);
        Add(map, StyleMapMatch.Name, "heading 2", BlockType.Heading2);
        Add(map, StyleMapMatch.Name, "标题 1", BlockType.Heading1);
        Add(map, StyleMapMatch.Name, "标题 2", BlockType.Heading2);
        Add(map, StyleMapMatch.Name, "正文", BlockType.Paragraph);
        Add(map, StyleMapMatch.Name, "Normal", BlockType.Paragraph);
        return map;
    }

    private static void Add(StyleMap map, StyleMapMatch match, string key, BlockType target)
    {
        map.Entries.Add(new StyleMapEntry { Match = match, Key = key, Target = target });
    }

    internal static int Count(Database database)
    {
        using (var transaction = database.TransactionManager.StartTransaction())
        {
            var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForRead);
            var count = 0;
            foreach (ObjectId _ in space) count++;
            transaction.Commit();
            return count;
        }
    }

    private static string Format(double value)
    {
        return value.ToString("G17", CultureInfo.InvariantCulture);
    }

    private static Diagnostic Problem(string code, string message)
    {
        return new Diagnostic
        {
            Code = code,
            Severity = Severity.Error,
            Stage = DiagnosticStage.Standard,
            Message = message
        };
    }

    private readonly struct ProductionPackage
    {
        public ProductionPackage(InstitutionStandard? standard, Contracts.Templates.LayoutTemplate? template, List<Diagnostic> diagnostics)
        {
            Standard = standard;
            Template = template;
            Diagnostics = diagnostics;
        }

        public InstitutionStandard? Standard { get; }

        public Contracts.Templates.LayoutTemplate? Template { get; }

        public List<Diagnostic> Diagnostics { get; }
    }
}
