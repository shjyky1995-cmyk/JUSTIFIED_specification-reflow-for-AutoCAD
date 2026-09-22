using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Justified.SpecificationReflow.AutoCAD.Application;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Geometry;
using Justified.SpecificationReflow.AutoCAD.Contracts.Layout;
using Justified.SpecificationReflow.AutoCAD.Contracts.Ports;
using Justified.SpecificationReflow.AutoCAD.Contracts.Rendering;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.Contracts.Templates;
using NUnit.Framework;

namespace Justified.SpecificationReflow.AutoCAD.Core.Tests;

// GenerationLimits：超大说明必须拒绝，不悄悄截断，也不照样落图。
public class NoteGenerationLimitTests
{
    [Test]
    public void OverBlockLimitStopsBeforeLayout()
    {
        var parser = new FixedParser(blocks: 501, charactersPerBlock: 1);
        var engine = new FixedEngine(pages: 1);
        var service = new NoteGenerationService(parser, engine, new NoMeasure());
        var result = service.Generate(new MemorySource(), Standard(), Template(), Transform(), CancellationToken.None, Limits(blocks: 500, texts: 10, pages: 10));
        Assert.That(result.Success, Is.False);
        Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.EResourceLimit));
        Assert.That(result.Diagnostics.Single().Message, Does.Contain("段落块数"));
        Assert.That(engine.LayoutCalls, Is.Zero);
        Assert.That(result.Texts, Is.Empty);
    }

    [Test]
    public void OverCharacterLimitStopsBeforeLayout()
    {
        var parser = new FixedParser(blocks: 2, charactersPerBlock: 1001);
        var engine = new FixedEngine(pages: 1);
        var service = new NoteGenerationService(parser, engine, new NoMeasure());
        var result = service.Generate(new MemorySource(), Standard(), Template(), Transform(), CancellationToken.None, Limits(characters: 2000));
        Assert.That(result.Success, Is.False);
        Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.EResourceLimit));
        Assert.That(result.Diagnostics.Single().Message, Does.Contain("字符数"));
    }

    [Test]
    public void OverPageLimitStopsAfterLayout()
    {
        var parser = new FixedParser(blocks: 1, charactersPerBlock: 10);
        var engine = new FixedEngine(pages: 41);
        var service = new NoteGenerationService(parser, engine, new NoMeasure());
        var result = service.Generate(new MemorySource(), Standard(), Template(), Transform(), CancellationToken.None, Limits(pages: 40));
        Assert.That(result.Success, Is.False);
        Assert.That(result.Diagnostics.Single(item => item.Code == DiagnosticCodes.EResourceLimit).Message, Does.Contain("页数"));
        Assert.That(result.Texts, Is.Empty);
    }

    [Test]
    public void OverTextLimitStopsAfterPlacement()
    {
        var parser = new FixedParser(blocks: 1, charactersPerBlock: 10);
        var engine = new FixedEngine(pages: 6);
        var service = new NoteGenerationService(parser, engine, new NoMeasure());
        var result = service.Generate(new MemorySource(), Standard(), Template(), Transform(), CancellationToken.None, Limits(texts: 5));
        Assert.That(result.Success, Is.False);
        Assert.That(result.Diagnostics.Single(item => item.Code == DiagnosticCodes.EResourceLimit).Message, Does.Contain("单行文字数"));
    }

    [Test]
    public void WithinLimitsGenerates()
    {
        var parser = new FixedParser(blocks: 3, charactersPerBlock: 10);
        var engine = new FixedEngine(pages: 2);
        var service = new NoteGenerationService(parser, engine, new NoMeasure());
        var result = service.Generate(new MemorySource(), Standard(), Template(), Transform(), CancellationToken.None, Limits(pages: 40, texts: 4000));
        Assert.That(result.Success, Is.True, Dump(result.Diagnostics));
        Assert.That(result.Texts.Count, Is.EqualTo(2));
    }

    [Test]
    public void NoLimitsKeepsOldBehaviour()
    {
        var parser = new FixedParser(blocks: 3000, charactersPerBlock: 1000);
        var engine = new FixedEngine(pages: 400);
        var service = new NoteGenerationService(parser, engine, new NoMeasure());
        var result = service.Generate(new MemorySource(), Standard(), Template(), Transform(), CancellationToken.None, null);
        Assert.That(result.Success, Is.True, Dump(result.Diagnostics));
        Assert.That(result.Texts.Count, Is.EqualTo(400));
    }

    private static GenerationLimits Limits(int blocks = int.MaxValue, int characters = int.MaxValue, int pages = int.MaxValue, int texts = int.MaxValue)
    {
        return new GenerationLimits { MaxBlocks = blocks, MaxCharacters = characters, MaxPages = pages, MaxTexts = texts };
    }

    private static InstitutionStandard Standard()
    {
        return new InstitutionStandard
        {
            StandardId = "jsr-note",
            Version = "1.0.0",
            TextHeight = 4.5,
            WidthFactor = 0.75,
            ObliqueAngle = 0,
            RowPitch = 7.2,
            SymbolMapVersion = "passthrough-1.0.0",
            LineBreakRuleVersion = "1.0.0",
            MeasurementTolerance = 0,
            LayerPolicy = new LayerPolicy { LayerName = "JSR_NOTE_TEXT" },
            FontProfile = new FontProfile
            {
                Fonts = { new FontEntry { Family = "TSSD", FileIdentity = "tssdeng.shx", BigFont = "tssdchn.shx" } }
            },
            Styles = new Dictionary<string, StyleDefinition>
            {
                ["heading1"] = new StyleDefinition { FontRef = "TSSD" },
                ["heading2"] = new StyleDefinition { FontRef = "TSSD" },
                ["body"] = new StyleDefinition { FontRef = "TSSD" }
            }
        };
    }

    private static LayoutTemplate Template()
    {
        return new LayoutTemplate
        {
            TemplateId = "jsr-A1-three-column",
            Version = "1.0.0",
            DisciplineCode = "structure",
            PaperCode = PaperCode.A1,
            Status = TemplateStatus.Calibrated,
            Orientation = Orientation.Landscape,
            Unit = LengthUnit.Mm,
            StandardRef = new StandardRef { Id = "jsr-note", Version = "1.0.0" },
            Anchor = new TemplateAnchor { Kind = AnchorKind.NoteTopRight, X = 0, Y = 0 },
            PageBounds = new PageBounds { Left = -783, Right = 58, Top = 40.46, Bottom = -553.54 },
            Columns = { new ColumnGeometry { ColumnId = "c0", Left = -710, Right = -480, Top = 0, Bottom = -511.91, FirstBaselineY = -6.2, RowCount = 71 } },
            PageStep = new Point2 { X = 861, Y = 0 },
            FramePolicy = FramePolicy.None
        };
    }

    private static RenderTransform Transform()
    {
        return new RenderTransform { AnchorWcs = new Point2 { X = 0, Y = 0 }, UnitScale = 1, TargetSpace = TargetSpace.Model };
    }

    private static string Dump(IReadOnlyList<Diagnostic> diagnostics)
    {
        return string.Join(" | ", diagnostics.Select(item => item.Code + " " + item.Message));
    }

    private sealed class MemorySource : IDocumentSource
    {
        public SourceInfo Info
        {
            get { return new SourceInfo { Kind = DocumentSourceKind.Docx, Name = "memory.docx", ContentHash = string.Empty }; }
        }

        public Stream OpenRead() => new MemoryStream(new byte[] { 0x50, 0x4b, 0x03, 0x04 });
    }

    private sealed class FixedParser : IDocumentParser
    {
        private readonly int _blocks;
        private readonly int _charactersPerBlock;

        public FixedParser(int blocks, int charactersPerBlock)
        {
            _blocks = blocks;
            _charactersPerBlock = charactersPerBlock;
        }

        public DocumentParseResult Parse(IDocumentSource source, ParseProfile profile, CancellationToken cancellationToken)
        {
            var document = new Document { SchemaVersion = "1.0.0", DocumentId = "test", DisciplineCode = "structure" };
            for (var i = 0; i < _blocks; i++)
            {
                var run = new TextRun { Text = new string('字', _charactersPerBlock), Semantic = RunSemantic.Normal };
                document.Blocks.Add(new Block { Id = "b" + i, Type = BlockType.Paragraph, Runs = { run } });
            }

            return new DocumentParseResult(document, Array.Empty<Diagnostic>());
        }
    }

    private sealed class FixedEngine : ILayoutEngine
    {
        private readonly int _pages;

        public FixedEngine(int pages)
        {
            _pages = pages;
        }

        public int LayoutCalls { get; private set; }

        public LayoutResult Layout(Document document, InstitutionStandard standard, LayoutTemplate template, ITextMeasureService measure, CancellationToken cancellationToken)
        {
            LayoutCalls++;
            var result = new LayoutResult { SchemaVersion = "1.0.0", DocumentHash = "hash", EngineVersion = "test" };
            result.StandardRef = new StandardRef { Id = standard.StandardId, Version = standard.Version };
            result.TemplateRef = new TemplateRef { Id = template.TemplateId, Version = template.Version };
            for (var page = 0; page < _pages; page++)
            {
                var run = new RenderRun
                {
                    Text = "第 " + page + " 行文字",
                    RelativeOrigin = new Point2 { X = 0, Y = 0 },
                    ResolvedStyle = new ResolvedStyle
                    {
                        StyleId = "body",
                        Semantic = RunSemantic.Normal,
                        Font = standard.FontProfile.Fonts[0],
                        TextHeight = standard.TextHeight,
                        WidthFactor = standard.WidthFactor,
                        ObliqueAngle = standard.ObliqueAngle
                    }
                };
                var visual = new VisualLine { Text = run.Text, RenderRuns = { run } };
                var row = new RowSlot { RowIndex = 0, Baseline = -6.2, Occupancy = Occupancy.Text, VisualLine = visual };
                result.Pages.Add(new LayoutPage
                {
                    PageIndex = page,
                    PageOffset = new Point2 { X = 861 * page, Y = 0 },
                    Columns = { new LayoutColumn { ColumnIndex = 0, Rows = { row } } }
                });
            }

            return result;
        }
    }

    private sealed class NoMeasure : ITextMeasureService
    {
        public TextMeasurement Measure(IReadOnlyList<TextRun> runs, ResolvedStyle style, CancellationToken cancellationToken)
        {
            return new TextMeasurement(Array.Empty<RunMeasurement>(), Array.Empty<Diagnostic>());
        }
    }
}
