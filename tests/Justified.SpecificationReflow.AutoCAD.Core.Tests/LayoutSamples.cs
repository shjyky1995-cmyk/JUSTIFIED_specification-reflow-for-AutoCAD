using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Geometry;
using Justified.SpecificationReflow.AutoCAD.Contracts.Layout;
using Justified.SpecificationReflow.AutoCAD.Contracts.Ports;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.Contracts.Templates;
using Justified.SpecificationReflow.AutoCAD.LayoutEngine;
using NUnit.Framework;

namespace Justified.SpecificationReflow.AutoCAD.Core.Tests;

internal static class LayoutSamples
{
    public static SpecificationLayoutEngine Engine(int safetyPageLimit = LayoutEngineInfo.DefaultSafetyPageLimit)
    {
        return new SpecificationLayoutEngine(safetyPageLimit);
    }

    public static InstitutionStandard Standard(
        double height = 4.5,
        double widthFactor = 0.75,
        double pitch = 7.2,
        double indent = 0,
        double hanging = 0,
        double tolerance = 0,
        string? lineBreakVersion = null,
        string symbolMapVersion = "identity-1")
    {
        var style = new StyleDefinition
        {
            FontRef = "TSSD",
            Indent = indent,
            HangingIndent = hanging,
            BeforeSlots = 0,
            AfterSlots = 0
        };
        return new InstitutionStandard
        {
            StandardId = "test-note",
            Version = "1.0.0",
            FontProfile = new FontProfile
            {
                Fonts = new List<FontEntry>
                {
                    new FontEntry { Family = "TSSD", FileIdentity = "tssdeng.shx", BigFont = "tssdchn.shx" }
                }
            },
            TextHeight = height,
            WidthFactor = widthFactor,
            ObliqueAngle = 0,
            RowPitch = pitch,
            Styles = new Dictionary<string, StyleDefinition>
            {
                ["heading1"] = Copy(style),
                ["heading2"] = Copy(style),
                ["body"] = Copy(style)
            },
            LayerPolicy = new LayerPolicy { LayerName = "JSR_NOTE_TEXT" },
            SymbolMapVersion = symbolMapVersion,
            LineBreakRuleVersion = lineBreakVersion ?? LayoutEngineInfo.LineBreakRuleVersion,
            MeasurementTolerance = tolerance
        };
    }

    public static LayoutTemplate Columns(params double[] widths)
    {
        return Columns(widths, 8, 7.2, 90);
    }

    public static LayoutTemplate Columns(double[] widths, int rows, double pitch, double firstBaseline)
    {
        var columns = new List<ColumnGeometry>();
        var x = 0d;
        var bottom = firstBaseline - Math.Max(0, rows - 1) * pitch - 30;
        for (var index = 0; index < widths.Length; index++)
        {
            columns.Add(new ColumnGeometry
            {
                ColumnId = "c" + index.ToString(CultureInfo.InvariantCulture),
                Left = x,
                Right = x + widths[index],
                Top = firstBaseline + 30,
                Bottom = bottom,
                FirstBaselineY = firstBaseline,
                RowCount = rows
            });
            x += widths[index] + 10;
        }

        return new LayoutTemplate
        {
            TemplateId = "test-layout",
            Version = "1.0.0",
            DisciplineCode = "structure",
            PaperCode = PaperCode.A1,
            Status = TemplateStatus.Calibrated,
            Orientation = Orientation.Landscape,
            Unit = LengthUnit.Mm,
            StandardRef = new StandardRef { Id = "test-note", Version = "1.0.0" },
            Anchor = new TemplateAnchor { Kind = AnchorKind.NoteTopRight },
            PageBounds = new PageBounds { Left = -20, Right = x + 20, Top = firstBaseline + 40, Bottom = bottom - 20 },
            Columns = columns,
            PageStep = new Point2 { X = Math.Max(100, x + 40), Y = 0 },
            FramePolicy = FramePolicy.None
        };
    }

    public static Document DocumentOf(params Block[] blocks)
    {
        return new Document
        {
            SchemaVersion = "1.0",
            DocumentId = "doc-1",
            DisciplineCode = "structure",
            Source = new SourceInfo { Kind = DocumentSourceKind.Docx, Name = "sample.docx", ContentHash = "abc123" },
            Blocks = new List<Block>(blocks)
        };
    }

    public static Block Text(int paragraph, string text, string? label = null, BlockType type = BlockType.Paragraph)
    {
        return Runs(paragraph, type, label, new TextRun { Text = text, Semantic = RunSemantic.Normal });
    }

    public static Block Runs(int paragraph, BlockType type, string? label, params TextRun[] runs)
    {
        return new Block
        {
            Id = "p" + paragraph.ToString(CultureInfo.InvariantCulture),
            Type = type,
            SourceRef = new SourceRef { ParagraphIndex = paragraph },
            Numbering = label == null ? null : new Numbering { Label = label, SourceKind = NumberingSourceKind.Automatic },
            Runs = new List<TextRun>(runs)
        };
    }

    public static Block Spacer(int paragraph, int slots = 1)
    {
        return new Block
        {
            Id = "p" + paragraph.ToString(CultureInfo.InvariantCulture),
            Type = BlockType.Spacer,
            SourceRef = new SourceRef { ParagraphIndex = paragraph },
            SlotCount = slots
        };
    }

    public static TextRun Run(string text, RunSemantic semantic = RunSemantic.Normal)
    {
        return new TextRun { Text = text, Semantic = semantic };
    }

    public static void Ok(LayoutResult result)
    {
        var errors = result.Diagnostics.Where(item => item.Severity == Severity.Error).Select(item => item.Code + " " + item.Message).ToArray();
        Assert.That(errors, Is.Empty, string.Join(" | ", errors));
    }

    public static string[] Lines(LayoutResult result)
    {
        return Rows(result).Where(row => row.Occupancy == Occupancy.Text && row.VisualLine != null).Select(row => row.VisualLine!.Text).ToArray();
    }

    public static RowSlot[] Rows(LayoutResult result)
    {
        return result.Pages.SelectMany(page => page.Columns).SelectMany(column => column.Rows).ToArray();
    }

    public static string Rebuild(Document document, LayoutResult result)
    {
        var blocks = new Dictionary<int, Block>();
        foreach (var block in document.Blocks)
            blocks[block.SourceRef.ParagraphIndex] = block;
        var builder = new System.Text.StringBuilder();
        foreach (var row in Rows(result))
        {
            if (row.VisualLine == null) continue;
            foreach (var slice in row.VisualLine.SourceSlices)
            {
                if (slice.RunIndex == null || slice.TextRange == null) continue;
                var run = blocks[slice.ParagraphIndex].Runs[slice.RunIndex.Value];
                builder.Append(run.Text.Substring(slice.TextRange.Start, slice.TextRange.Length));
            }
        }

        return builder.ToString();
    }

    private static StyleDefinition Copy(StyleDefinition style)
    {
        return new StyleDefinition
        {
            FontRef = style.FontRef,
            Indent = style.Indent,
            HangingIndent = style.HangingIndent,
            BeforeSlots = style.BeforeSlots,
            AfterSlots = style.AfterSlots
        };
    }
}

internal sealed class FakeMeasure : ITextMeasureService
{
    public double Unit { get; set; } = 1;

    public double Cjk { get; set; } = 1;

    public double InkHeight { get; set; }

    public double InkDescent { get; set; }

    public int ExtraInkAfterLength { get; set; } = int.MaxValue;

    public double ExtraInk { get; set; }

    public Dictionary<string, double> Exact { get; } = new Dictionary<string, double>(StringComparer.Ordinal);

    public Diagnostic? Failure { get; set; }

    public int FailOnCall { get; set; } = int.MaxValue;

    public int Calls { get; private set; }

    public TextMeasurement Measure(IReadOnlyList<TextRun> runs, ResolvedStyle style, CancellationToken cancellationToken)
    {
        Calls++;
        if (cancellationToken.IsCancellationRequested)
            return new TextMeasurement(new RunMeasurement[0], new[] { Problem(DiagnosticCodes.Cancelled, "测量已取消。") });
        if (Failure != null && Calls >= FailOnCall)
            return new TextMeasurement(new RunMeasurement[0], new[] { Failure });

        var text = runs == null || runs.Count == 0 || runs[0] == null ? string.Empty : runs[0].Text;
        var width = Exact.TryGetValue(text, out var exact) ? exact : WidthOf(text);
        var extra = text.Length > ExtraInkAfterLength ? ExtraInk : 0;
        var height = InkHeight > 0 ? InkHeight : style.TextHeight;
        return new TextMeasurement(
            new[]
            {
                new RunMeasurement(width, new Bounds2
                {
                    MinX = 0,
                    MinY = -InkDescent,
                    MaxX = width + extra,
                    MaxY = height
                })
            },
            new Diagnostic[0]);
    }

    private double WidthOf(string text)
    {
        var sum = 0d;
        var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            sum += IsHan(element) ? Cjk : Unit;
        }

        return sum;
    }

    private static bool IsHan(string element)
    {
        if (element.Length != 1) return false;
        var character = element[0];
        return (character >= 0x4E00 && character <= 0x9FFF)
            || (character >= 0x3400 && character <= 0x4DBF)
            || (character >= 0xF900 && character <= 0xFAFF);
    }

    private static Diagnostic Problem(string code, string message)
    {
        return new Diagnostic
        {
            Code = code,
            Severity = Severity.Error,
            Stage = DiagnosticStage.Measure,
            Message = message
        };
    }
}
