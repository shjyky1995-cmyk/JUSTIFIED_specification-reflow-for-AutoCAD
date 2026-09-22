using System;
using System.Linq;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.Standards;
using NUnit.Framework;

namespace Justified.SpecificationReflow.AutoCAD.Core.Tests;

public class NoteSettingsTests
{
    [Test]
    public void RoundTripKeepsEverySetting()
    {
        var settings = new NoteSettings
        {
            StandardRoot = @"G:\packs\jsr",
            StandardId = "jsr-note",
            StandardVersion = "1.0.0",
            TemplateId = "jsr-A1-three-column",
            TemplateVersion = "1.0.0",
            PaperCode = "A1",
            UnitScale = 1,
            DocumentPath = @"G:\notes\说明.docx",
            ReportDirectory = @"G:\notes\_reports",
            SavedUtc = DateTimeOffset.Parse("2026-09-22T06:30:00.0000000+00:00").ToString("O")
        };
        Assert.That(settings.Problems(), Is.Empty);

        var chunks = NoteSettingsCodec.Encode(settings);
        Assert.That(chunks, Is.Not.Empty);
        Assert.That(chunks.All(chunk => chunk.Length <= NoteSettingsCodec.ChunkSize), Is.True);

        var decoded = NoteSettingsCodec.Decode(chunks);
        Assert.That(decoded, Is.Not.Null);
        Assert.That(decoded!.StandardRoot, Is.EqualTo(settings.StandardRoot));
        Assert.That(decoded.StandardId, Is.EqualTo(settings.StandardId));
        Assert.That(decoded.StandardVersion, Is.EqualTo(settings.StandardVersion));
        Assert.That(decoded.TemplateId, Is.EqualTo(settings.TemplateId));
        Assert.That(decoded.TemplateVersion, Is.EqualTo(settings.TemplateVersion));
        Assert.That(decoded.PaperCode, Is.EqualTo(settings.PaperCode));
        Assert.That(decoded.UnitScale, Is.EqualTo(settings.UnitScale));
        Assert.That(decoded.DocumentPath, Is.EqualTo(settings.DocumentPath));
        Assert.That(decoded.ReportDirectory, Is.EqualTo(settings.ReportDirectory));
        Assert.That(decoded.SavedUtc, Is.EqualTo(settings.SavedUtc));
        Assert.That(decoded.Problems(), Is.Empty);
    }

    [Test]
    public void LongRootSplitsIntoManyChunks()
    {
        var settings = Complete();
        settings.StandardRoot = @"G:\" + string.Join("", Enumerable.Repeat("很长的目录名", 40));
        var chunks = NoteSettingsCodec.Encode(settings);
        Assert.That(chunks.Count, Is.GreaterThan(1));
        Assert.That(NoteSettingsCodec.Decode(chunks)!.StandardRoot, Is.EqualTo(settings.StandardRoot));
    }

    [Test]
    public void DamagedChunksDecodeToNull()
    {
        var chunks = NoteSettingsCodec.Encode(Complete());
        Assert.That(NoteSettingsCodec.Decode(null), Is.Null);
        Assert.That(NoteSettingsCodec.Decode(new string[0]), Is.Null);
        Assert.That(NoteSettingsCodec.Decode(chunks.Skip(1).ToList()), Is.Null);
        Assert.That(NoteSettingsCodec.Decode(chunks.Take(chunks.Count - 1).ToList()), Is.Null);
        Assert.That(NoteSettingsCodec.Decode(new[] { "!!不是Base64!!" }), Is.Null);
    }

    [Test]
    public void IncompleteSettingsReportProblems()
    {
        Assert.That(new NoteSettings().Problems(), Is.Not.Empty);
        var badScale = Complete();
        badScale.UnitScale = 0;
        Assert.That(badScale.Problems().Single(), Does.Contain("单位比例"));
        var badPaper = Complete();
        badPaper.PaperCode = "A4";
        Assert.That(badPaper.Problems().Single(), Does.Contain("图幅"));
        var badTime = Complete();
        badTime.SavedUtc = "2026-09-22 06:30";
        Assert.That(badTime.Problems().Single(), Does.Contain("保存时间"));
        var badReport = Complete();
        badReport.ReportDirectory = "G" + new string(':', 1) + "\0bad";
        Assert.That(badReport.Problems().Single(), Does.Contain("运行报告目录"));
    }

    [Test]
    public void SettingsMissingRequiredValuesNeverRoundTrip()
    {
        var broken = Complete();
        broken.StandardRoot = " ";
        Assert.That(NoteSettingsCodec.Decode(NoteSettingsCodec.Encode(broken)), Is.Null);
    }

    private static NoteSettings Complete()
    {
        return new NoteSettings
        {
            StandardRoot = @"G:\packs\jsr",
            StandardId = "jsr-note",
            StandardVersion = "1.0.0",
            TemplateId = "jsr-A1-three-column",
            TemplateVersion = "1.0.0",
            PaperCode = "A1",
            UnitScale = 1,
            DocumentPath = @"G:\notes\说明.docx",
            ReportDirectory = null,
            SavedUtc = DateTimeOffset.Parse("2026-09-22T06:30:00.0000000+00:00").ToString("O")
        };
    }
}
