using System;
using System.Globalization;
using System.Reflection;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Justified.SpecificationReflow.AutoCAD.AutoCadAdapter;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Layout;
using Justified.SpecificationReflow.AutoCAD.Contracts.Ports;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using CadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Justified.SpecificationReflow.AutoCAD.PluginHost.Commands))]

namespace Justified.SpecificationReflow.AutoCAD.PluginHost;

// DN_DIAG checks dependencies. DN_MEASURE reads host glyph extents and does not commit its transaction.
public class Commands
{
    [CommandMethod("DN_DIAG", CommandFlags.Modal)]
    public void Diagnose()
    {
        var editor = CadApplication.DocumentManager.MdiActiveDocument.Editor;
        try
        {
            foreach (var name in new[] {
                "Justified.SpecificationReflow.AutoCAD.Contracts", "Justified.SpecificationReflow.AutoCAD.DocumentCore", "Justified.SpecificationReflow.AutoCAD.DocxAdapter",
                "Justified.SpecificationReflow.AutoCAD.Standards", "Justified.SpecificationReflow.AutoCAD.LayoutEngine", "Justified.SpecificationReflow.AutoCAD.Application",
                "Justified.SpecificationReflow.AutoCAD.AutoCadAdapter",
                "Newtonsoft.Json, Version=13.0.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed",
                "DocumentFormat.OpenXml, Version=3.0.2.0, Culture=neutral, PublicKeyToken=8fb06cb64d019a17",
                "DocumentFormat.OpenXml.Framework, Version=3.0.2.0, Culture=neutral, PublicKeyToken=8fb06cb64d019a17"
            })
            {
                var assembly = Assembly.Load(name);
                var expected = new AssemblyName(name).Version ?? new Version(0, 1, 0, 0);
                if (assembly.GetName().Version != expected)
                    throw new InvalidOperationException("Dependency version mismatch: " + assembly.FullName);
                editor.WriteMessage("\nDN_DEP " + assembly.GetName().Name + " " + assembly.GetName().Version);
            }
            editor.WriteMessage("\nDN_DIAG_OK: 依赖已加载。生成命令是 DN_NOTE。草案不是已发布院标。\n");
        }
        catch (System.Exception error)
        {
            editor.WriteMessage("\nDN_DIAG_FAILED: " + error.GetType().Name + ": " + error.Message + "\n");
        }
    }

    // 控制台测量探针。字高和宽度系数只属于这次命令，不是已发布院标。
    [CommandMethod("DN_MEASURE", CommandFlags.Modal)]
    public void MeasureSample()
    {
        var document = CadApplication.DocumentManager.MdiActiveDocument;
        var editor = document.Editor;
        var database = document.Database;
        var before = CountEntities(database);
        try
        {
            var service = new HostTextMeasureService(database);
            var style = TestStyle("txt.shx", null);
            var sample = service.Measure(new[] { new TextRun { Text = "W20", Semantic = RunSemantic.Normal } }, style, System.Threading.CancellationToken.None);
            if (!sample.Success || sample.Runs.Count != 1)
            {
                editor.WriteMessage("\nDN_MEASURE_FAILED " + First(sample) + "\n");
                return;
            }

            var run = sample.Runs[0];
            editor.WriteMessage("\nDN_MEASURE_STYLE test-command textHeight=2.5 widthFactor=0.8 obliqueAngle=0");
            editor.WriteMessage("\nDN_MEASURE_FONT txt.shx");
            editor.WriteMessage("\nDN_MEASURE_TEXT W20");
            editor.WriteMessage("\nDN_MEASURE_DBTEXT DBText");
            editor.WriteMessage("\nDN_MEASURE_ADVANCE " + run.Advance.ToString("G17", CultureInfo.InvariantCulture));
            editor.WriteMessage("\nDN_MEASURE_BOUNDS "
                + run.InkBounds.MinX.ToString("G17", CultureInfo.InvariantCulture) + ","
                + run.InkBounds.MinY.ToString("G17", CultureInfo.InvariantCulture) + ","
                + run.InkBounds.MaxX.ToString("G17", CultureInfo.InvariantCulture) + ","
                + run.InkBounds.MaxY.ToString("G17", CultureInfo.InvariantCulture));

            var missing = service.Measure(
                new[] { new TextRun { Text = "W20", Semantic = RunSemantic.Normal } },
                TestStyle("missing-test-font.shx", null),
                System.Threading.CancellationToken.None);
            editor.WriteMessage("\nDN_MEASURE_MISSING_FONT " + First(missing));

            var superscript = service.Measure(
                new[] { new TextRun { Text = "2", Semantic = RunSemantic.Superscript } },
                style,
                System.Threading.CancellationToken.None);
            editor.WriteMessage("\nDN_MEASURE_SUPERSCRIPT " + First(superscript));

            var after = CountEntities(database);
            editor.WriteMessage("\nDN_MEASURE_ENTITIES before=" + before + " after=" + after);
            if (missing.Diagnostics.Count == 1 && missing.Diagnostics[0].Code == DiagnosticCodes.EFontMissing
                && superscript.Diagnostics.Count == 1 && superscript.Diagnostics[0].Code == DiagnosticCodes.ETemplateInvalid
                && before == after && run.Advance > 0 && run.InkBounds.MaxX > run.InkBounds.MinX)
                editor.WriteMessage("\nDN_MEASURE_OK\n");
            else
                editor.WriteMessage("\nDN_MEASURE_FAILED result was not a positive DBText extent or entities remained\n");
        }
        catch (System.Exception error)
        {
            editor.WriteMessage("\nDN_MEASURE_FAILED " + error.GetType().Name + ": " + error.Message + "\n");
        }
    }

    private static ResolvedStyle TestStyle(string fileIdentity, string? bigFont)
    {
        return new ResolvedStyle
        {
            StyleId = "test-measure",
            Semantic = RunSemantic.Normal,
            Font = new FontEntry { Family = "test", FileIdentity = fileIdentity, BigFont = bigFont },
            TextHeight = 2.5,
            WidthFactor = 0.8,
            ObliqueAngle = 0
        };
    }

    private static string First(TextMeasurement measurement)
    {
        if (measurement.Diagnostics.Count == 0) return "no-diagnostic";
        return measurement.Diagnostics[0].Code + " " + measurement.Diagnostics[0].Message;
    }

    private static int CountEntities(Database database)
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
}
