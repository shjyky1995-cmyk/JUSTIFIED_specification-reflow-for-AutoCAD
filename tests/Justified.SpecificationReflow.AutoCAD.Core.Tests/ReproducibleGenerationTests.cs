using System;
using System.IO;
using System.Linq;
using System.Threading;
using Justified.SpecificationReflow.AutoCAD.Application;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Geometry;
using Justified.SpecificationReflow.AutoCAD.Contracts.Ports;
using Justified.SpecificationReflow.AutoCAD.Contracts.Rendering;
using Justified.SpecificationReflow.AutoCAD.DocxAdapter;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Justified.SpecificationReflow.AutoCAD.Core.Tests;

public class ReproducibleGenerationTests
{
    [Test]
    public void TenThousandCharactersRepeatExactlyAcrossDisciplines()
    {
        var builder = new DocxFixtureBuilder();
        builder.Styles.Add(DocxFixtureBuilder.ParagraphStyle("Normal", "Normal"));
        for (var i = 0; i < 100; i++)
            builder.Body.Add(DocxFixtureBuilder.Paragraph("Normal", DocxFixtureBuilder.TextRun(new string('甲', 99) + "。")));
        var bytes = builder.Build();
        var baseline = Generate(bytes, "structure");
        Assert.That(baseline.Success, Is.True, string.Join(";", baseline.Diagnostics.Select(x => x.Message)));
        Assert.That(string.Concat(baseline.Texts.Select(x => x.Text)).Length, Is.EqualTo(10000));
        Assert.That(baseline.Document!.Source.ContentHash, Is.Not.Empty);
        Assert.That(baseline.Layout!.Pages.Count, Is.LessThanOrEqualTo(10));
        foreach (var discipline in new[] { "structure", "electrical", "water" })
        {
            var actual = Generate(bytes, discipline);
            Assert.That(actual.Success, Is.True);
            Assert.That(JsonConvert.SerializeObject(actual.Texts), Is.EqualTo(JsonConvert.SerializeObject(baseline.Texts)));
            Assert.That(actual.Document!.Source.ContentHash, Is.EqualTo(baseline.Document.Source.ContentHash));
            Assert.That(actual.ReadMilliseconds, Is.GreaterThanOrEqualTo(0));
            Assert.That(actual.ParseMilliseconds, Is.GreaterThanOrEqualTo(0));
            Assert.That(actual.LayoutMilliseconds, Is.GreaterThan(0));
            TestContext.WriteLine($"模拟测量核心检查 {discipline}: read={actual.ReadMilliseconds:F3}, parse={actual.ParseMilliseconds:F3}, layout={actual.LayoutMilliseconds:F3} ms；非宿主性能签收");
        }
    }

    [Test]
    public void ParserFailureStillDisposesTheMeasuredSource()
    {
        var source = new TrackingSource();
        var result = Service("water").Generate(source, LayoutSamples.Standard(), LayoutSamples.Columns(100),
            new RenderTransform { UnitScale = 1, TargetSpace = TargetSpace.Model }, CancellationToken.None);
        Assert.That(result.Success, Is.False);
        Assert.That(result.Diagnostics.Any(x => x.Code == "E_DOCX_READ"), Is.True);
        Assert.That(source.Stream.CanRead, Is.False);
        Assert.That(result.ParseMilliseconds, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void CancellationDuringReadReturnsNoTextAndDisposesSource()
    {
        using var cancellation = new CancellationTokenSource();
        var source = new CancellingSource(cancellation);
        var result = Service("structure").Generate(source, LayoutSamples.Standard(), LayoutSamples.Columns(100),
            new RenderTransform { UnitScale = 1, TargetSpace = TargetSpace.Model }, cancellation.Token);
        Assert.That(source.Stream.Reads, Is.EqualTo(1));
        Assert.That(result.Success, Is.False);
        Assert.That(result.Diagnostics.Any(x => x.Code == "CANCELLED"), Is.True);
        Assert.That(result.Texts, Is.Empty);
        Assert.That(source.Stream.CanRead, Is.False);
    }

    private sealed class CancellingSource : IDocumentSource
    {
        public CancellingSource(CancellationTokenSource cancellation) { Stream = new CancellingStream(cancellation); }
        public CancellingStream Stream { get; }
        public SourceInfo Info { get; } = new SourceInfo { Name = "cancel.docx" };
        public Stream OpenRead() => Stream;
    }

    private sealed class CancellingStream : MemoryStream
    {
        private readonly CancellationTokenSource _cancellation;
        public int Reads { get; private set; }
        public CancellingStream(CancellationTokenSource cancellation) : base(new byte[100000]) { _cancellation = cancellation; }
        public override int Read(byte[] buffer, int offset, int count)
        {
            Reads++;
            var read = base.Read(buffer, offset, count);
            _cancellation.Cancel();
            return read;
        }
    }

    private static NoteGenerationResult Generate(byte[] bytes, string discipline)
    {
        var template = LayoutSamples.Columns(new[] { 100d, 100d, 100d }, 71, 7.2, 0);
        template.DisciplineCode = discipline;
        return Service(discipline).Generate(new DocxBytesSource("generated.docx", bytes), LayoutSamples.Standard(), template,
            new RenderTransform { UnitScale = 1, AnchorWcs = new Point2 { X = 100, Y = 50 }, TargetSpace = TargetSpace.Model },
            CancellationToken.None, GenerationLimits.Default);
    }

    private static NoteGenerationService Service(string discipline) => new NoteGenerationService(
        new DocxDocumentParser(new DocxParseOptions
        {
            DocumentId = "repeat", DisciplineCode = discipline,
            StyleMap = new StyleMap { Entries = { new StyleMapEntry { Match = StyleMapMatch.StyleId, Key = "Normal", Target = BlockType.Paragraph } } }
        }),
        LayoutSamples.Engine(), new FakeMeasure());

    private sealed class TrackingSource : IDocumentSource
    {
        public MemoryStream Stream { get; } = new MemoryStream(new byte[] { 0, 1, 2 });
        public SourceInfo Info { get; } = new SourceInfo { Name = "broken.docx" };
        public Stream OpenRead() => Stream;
    }
}
