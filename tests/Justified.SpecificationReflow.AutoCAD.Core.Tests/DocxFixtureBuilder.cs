using System.Collections.Generic;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Justified.SpecificationReflow.AutoCAD.Core.Tests;

internal sealed class DocxFixtureBuilder
{
    public List<Style> Styles { get; } = new List<Style>();

    public List<OpenXmlElement> Numbering { get; } = new List<OpenXmlElement>();

    public List<OpenXmlElement> Body { get; } = new List<OpenXmlElement>();

    public bool EmitTerminator { get; set; } = true;

    public string TerminatorStyleId { get; set; } = "Normal";

    public string? HeaderText { get; set; }

    public string? FooterText { get; set; }

    public bool IncludeVba { get; set; }

    public byte[] Build()
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            if (Styles.Count > 0)
            {
                var styles = main.AddNewPart<StyleDefinitionsPart>();
                styles.Styles = new Styles(Styles);
                styles.Styles.Save();
            }

            if (Numbering.Count > 0)
            {
                var numbering = main.AddNewPart<NumberingDefinitionsPart>();
                numbering.Numbering = new Numbering(Numbering);
                numbering.Numbering.Save();
            }

            if (HeaderText != null)
            {
                var header = main.AddNewPart<HeaderPart>();
                header.Header = new Header(Paragraph(null, TextRun(HeaderText)));
                header.Header.Save();
            }

            if (FooterText != null)
            {
                var footer = main.AddNewPart<FooterPart>();
                footer.Footer = new Footer(Paragraph(null, TextRun(FooterText)));
                footer.Footer.Save();
            }

            if (IncludeVba)
            {
                var vba = main.AddNewPart<VbaProjectPart>();
                using var part = vba.GetStream(FileMode.Create, FileAccess.Write);
                part.Write(new byte[] { 0 }, 0, 1);
            }

            var children = new List<OpenXmlElement>(Body);
            if (EmitTerminator)
                children.Add(Paragraph(TerminatorStyleId));
            children.Add(new SectionProperties());
            main.Document = new Document(new Body(children));
            main.Document.Save();
        }

        return stream.ToArray();
    }

    public static Style ParagraphStyle(string styleId, string name, string? basedOn = null, bool isDefault = false, OpenXmlElement? runProperties = null)
    {
        var children = new List<OpenXmlElement> { new StyleName { Val = name } };
        if (basedOn != null) children.Add(new BasedOn { Val = basedOn });
        if (runProperties != null) children.Add(runProperties);
        return new Style(children) { Type = StyleValues.Paragraph, StyleId = styleId, Default = isDefault };
    }

    public static Paragraph Paragraph(string? styleId, params OpenXmlElement[] inline)
    {
        var children = new List<OpenXmlElement>();
        if (styleId != null)
            children.Add(new ParagraphProperties(new ParagraphStyleId { Val = styleId }));
        children.AddRange(inline);
        return new Paragraph(children);
    }

    public static Paragraph Numbered(string styleId, int numId, int level, string text)
    {
        return new Paragraph(
            new ParagraphProperties(
                new ParagraphStyleId { Val = styleId },
                new NumberingProperties(
                    new NumberingLevelReference { Val = level },
                    new NumberingId { Val = numId })),
            TextRun(text));
    }

    public static Run TextRun(string text, RunProperties? properties = null)
    {
        var run = new Run();
        if (properties != null) run.RunProperties = properties;
        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    public static AbstractNum NumberingAbstract(int abstractId, params LevelSpec[] levels)
    {
        var abs = new AbstractNum { AbstractNumberId = abstractId };
        foreach (var level in levels)
        {
            var children = new List<OpenXmlElement>
            {
                new StartNumberingValue { Val = level.Start },
                new NumberingFormat { Val = level.Format },
                new LevelText { Val = level.Text }
            };
            if (level.Restart != null)
                children.Add(new LevelRestart { Val = level.Restart.Value });
            abs.AppendChild(new Level(children) { LevelIndex = level.Level });
        }

        return abs;
    }

    public static NumberingInstance ListInstance(int numId, int abstractId, int? level = null, int? startOverride = null)
    {
        var instance = new NumberingInstance(new AbstractNumId { Val = abstractId }) { NumberID = numId };
        if (level != null && startOverride != null)
        {
            instance.AppendChild(new LevelOverride(
                new StartOverrideNumberingValue { Val = startOverride.Value })
            { LevelIndex = level.Value });
        }

        return instance;
    }

    internal readonly struct LevelSpec
    {
        public LevelSpec(int level, string text, int start, NumberFormatValues format, int? restart)
        {
            Level = level;
            Text = text;
            Start = start;
            Format = format;
            Restart = restart;
        }

        public int Level { get; }

        public string Text { get; }

        public int Start { get; }

        public NumberFormatValues Format { get; }

        public int? Restart { get; }
    }
}
