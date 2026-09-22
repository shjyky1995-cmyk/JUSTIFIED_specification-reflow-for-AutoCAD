using System;
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
using Justified.SpecificationReflow.AutoCAD.Contracts.Rendering;
using Justified.SpecificationReflow.AutoCAD.DocxAdapter;
using Justified.SpecificationReflow.AutoCAD.LayoutEngine;
using Justified.SpecificationReflow.AutoCAD.Standards;
using CadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Justified.SpecificationReflow.AutoCAD.PluginHost.NoteCommands))]

namespace Justified.SpecificationReflow.AutoCAD.PluginHost;

// DN_NOTE：选 Word、草案图幅和单位比例，点一次说明区右上角，写入全部 DBText。
public class NoteCommands
{
    [CommandMethod("DN_NOTE", CommandFlags.Modal)]
    public void Note()
    {
        var document = CadApplication.DocumentManager.MdiActiveDocument;
        if (document == null) return;
        var editor = document.Editor;
        var before = Count(document.Database);
        try
        {
            var file = editor.GetFileNameForOpen(new PromptOpenFileOptions("选择说明 DOCX")
            {
                Filter = "Word 文档 (*.docx)|*.docx",
                DialogCaption = "选择说明"
            });
            if (file.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\nDN_NOTE_CANCELLED\n");
                return;
            }

            var folderPrompt = new PromptStringOptions("\n标准目录，选含 jsr-note-1.0.0.json 的 drafts 文件夹")
            {
                AllowSpaces = true
            };
            var folder = editor.GetString(folderPrompt);
            if (folder.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\nDN_NOTE_CANCELLED\n");
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
                editor.WriteMessage("\nDN_NOTE_CANCELLED\n");
                return;
            }

            var scalePrompt = new PromptDoubleOptions("\n单位比例 unitScale，1 个标准毫米对应多少图形单位")
            {
                AllowNegative = false,
                AllowZero = false,
                AllowNone = false
            };
            var scale = editor.GetDouble(scalePrompt);
            if (scale.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\nDN_NOTE_CANCELLED\n");
                return;
            }

            var point = editor.GetPoint(new PromptPointOptions("\n点选说明区右上角"));
            if (point.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\nDN_NOTE_CANCELLED\n");
                return;
            }

            var wcs = point.Value.TransformBy(editor.CurrentUserCoordinateSystem);
            editor.WriteMessage("\nDN_NOTE_DOCX " + file.StringResult);
            editor.WriteMessage("\nDN_NOTE_ANCHOR_UCS " + Format(point.Value.X) + "," + Format(point.Value.Y) + "," + Format(point.Value.Z));
            editor.WriteMessage("\nDN_NOTE_ANCHOR_WCS " + Format(wcs.X) + "," + Format(wcs.Y) + "," + Format(wcs.Z));

            var paperCode = paper.Status == PromptStatus.OK && !string.IsNullOrWhiteSpace(paper.StringResult) ? paper.StringResult : "A1";
            var session = new DraftSessionLoader().Load(ResolveDrafts(folder.StringResult), paperCode, LayoutEngineInfo.LineBreakRuleVersion, System.Threading.CancellationToken.None);
            foreach (var diagnostic in session.Diagnostics.Where(item => item.Severity == Severity.Info))
                editor.WriteMessage("\nDN_NOTE_DRAFT " + diagnostic.Message);
            if (!session.Success || session.Standard == null || session.Template == null)
            {
                foreach (var diagnostic in session.Diagnostics.Where(item => item.Severity == Severity.Error))
                    editor.WriteMessage("\nDN_NOTE_FAILED " + diagnostic.Code + " " + diagnostic.Message);
                editor.WriteMessage("\nDN_NOTE_ENTITIES before=" + before + " after=" + Count(document.Database) + "\n");
                return;
            }

            editor.WriteMessage("\nDN_NOTE_STANDARD " + session.Standard.StandardId + " " + session.Standard.Version);
            editor.WriteMessage("\nDN_NOTE_TEMPLATE " + session.Template.TemplateId + " " + session.Template.Version);
            editor.WriteMessage("\nDN_NOTE_SCALE " + Format(scale.Value));

            NoteGenerationResult generated;
            using (document.LockDocument())
            {
                var service = new NoteGenerationService(
                    new DocxDocumentParser(new DocxParseOptions
                    {
                        DocumentId = "dn-note",
                        DisciplineCode = string.IsNullOrWhiteSpace(session.Template.DisciplineCode) ? "structure" : session.Template.DisciplineCode,
                        StyleMap = Map()
                    }),
                    new SpecificationLayoutEngine(),
                    new HostTextMeasureService(document.Database));
                generated = service.Generate(
                    new DocxFileSource(file.StringResult),
                    session.Standard,
                    session.Template,
                    new RenderTransform
                    {
                        AnchorWcs = new Point2 { X = wcs.X, Y = wcs.Y },
                        UnitScale = scale.Value,
                        TargetSpace = TargetSpace.Model
                    },
                    System.Threading.CancellationToken.None);
            }

            foreach (var diagnostic in generated.Diagnostics.Where(item => item.Severity == Severity.Warning))
                editor.WriteMessage("\nDN_NOTE_WARN " + diagnostic.Code + " " + diagnostic.Message);
            if (!generated.Success)
            {
                foreach (var diagnostic in generated.Diagnostics.Where(item => item.Severity == Severity.Error))
                    editor.WriteMessage("\nDN_NOTE_FAILED " + diagnostic.Code + " " + diagnostic.Message);
                editor.WriteMessage("\nDN_NOTE_ENTITIES before=" + before + " after=" + Count(document.Database) + "\n");
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

            var step = session.Template.PageStep ?? new Point2();
            editor.WriteMessage("\nDN_NOTE_SUMMARY pages=" + (generated.Layout == null ? 0 : generated.Layout.Pages.Count)
                + " objects=" + generated.Texts.Count
                + " warnings=" + generated.Diagnostics.Count(item => item.Severity == Severity.Warning));
            editor.WriteMessage("\nDN_NOTE_PAGE_STEP x=" + Format(step.X) + " y=" + Format(step.Y) + " scale=" + Format(scale.Value));
            if (generated.Texts.Count > 0)
            {
                var first = generated.Texts[0];
                var preview = first.Text.Length <= 40 ? first.Text : first.Text.Substring(0, 40);
                editor.WriteMessage("\nDN_NOTE_FIRST x=" + Format(first.Position.X) + " y=" + Format(first.Position.Y)
                    + " h=" + Format(first.Height) + " widthFactor=" + Format(first.WidthFactor) + " text=" + preview);
            }

            RenderReport report;
            using (document.LockDocument())
                report = new DbTextWriter().Write(document.Database, generated.Texts, wcs.Z, System.Threading.CancellationToken.None);
            var after = Count(document.Database);
            if (!report.Success)
            {
                foreach (var diagnostic in report.Diagnostics)
                    editor.WriteMessage("\nDN_NOTE_FAILED " + diagnostic.Code + " " + diagnostic.Message);
                editor.WriteMessage("\nDN_NOTE_ENTITIES before=" + before + " after=" + after + "\n");
                return;
            }

            editor.WriteMessage("\nDN_NOTE_ENTITIES before=" + before + " after=" + after);
            editor.WriteMessage("\nDN_NOTE_OK objects=" + report.ObjectCount);
            editor.WriteMessage("\nDN_NOTE_UNDO 一次 U 撤销本次提交。已有同名样式不会被修改，旧文字也不会被删除。\n");
        }
        catch (System.Exception error)
        {
            editor.WriteMessage("\nDN_NOTE_FAILED " + error.GetType().Name + " " + error.Message);
            editor.WriteMessage("\nDN_NOTE_ENTITIES before=" + before + " after=" + Count(document.Database) + "\n");
        }
    }

    private static string ResolveDrafts(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        var trimmed = input.Trim().Trim('"');
        if (File.Exists(Path.Combine(trimmed, "jsr-note-1.0.0.json"))) return trimmed;
        var drafts = Path.Combine(trimmed, "standards", "drafts");
        if (File.Exists(Path.Combine(drafts, "jsr-note-1.0.0.json"))) return drafts;
        var nested = Path.Combine(trimmed, "drafts");
        if (File.Exists(Path.Combine(nested, "jsr-note-1.0.0.json"))) return nested;
        return trimmed;
    }

    private static StyleMap Map()
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

    private static int Count(Database database)
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
}
