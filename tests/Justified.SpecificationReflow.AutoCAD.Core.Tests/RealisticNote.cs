using System.Collections.Generic;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Justified.SpecificationReflow.AutoCAD.Core.Tests;

internal static class RealisticNote
{
    public static byte[] Build()
    {
        var builder = new DocxFixtureBuilder
        {
            Title = "结构设计总说明（测试样本）",
            Creator = "JUSTIFIED test",
            HeaderText = "结构设计总说明（测试样本）",
            FooterText = "脱敏样本，不用于施工"
        };
        builder.Styles.Add(BodyStyle());
        builder.Styles.Add(HeadingStyle("Heading1", "heading 1", "标题 1", "Normal", 0, "32", "1F4E79", "360", "80"));
        builder.Styles.Add(HeadingStyle("Heading2", "heading 2", "标题 2", "Heading1", 1, "28", "2E75B6", "280", "60"));
        builder.Numbering.Add(DecimalList());
        builder.Numbering.Add(DocxFixtureBuilder.ListInstance(1, 0));
        builder.SectionChildren.Add(new PageSize { Width = 11906, Height = 16838 });
        builder.SectionChildren.Add(new PageMargin
        {
            Top = 1440,
            Bottom = 1440,
            Left = 1800,
            Right = 1440,
            Header = 720,
            Footer = 720,
            Gutter = 0
        });

        builder.Body.Add(DocxFixtureBuilder.Paragraph("Heading1", DocxFixtureBuilder.TextRun("结构设计总说明")));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Heading2", DocxFixtureBuilder.TextRun("工程概况")));
        builder.Body.Add(Numbered(0, DocxFixtureBuilder.TextRun("本工程为地上三层钢筋混凝土框架结构，本文件只用于软件解析测试。")));
        builder.Body.Add(Numbered(1, DocxFixtureBuilder.TextRun("结构设计使用年限为50年，结构安全等级为二级。")));
        builder.Body.Add(Numbered(1, DocxFixtureBuilder.TextRun("抗震设防烈度为7度，设计基本地震加速度为0.10g。")));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Normal"));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Heading2", DocxFixtureBuilder.TextRun("主要材料")));
        builder.Body.Add(Numbered(0, DocxFixtureBuilder.TextRun("混凝土强度等级为C30，钢筋采用HRB400。")));
        builder.Body.Add(Numbered(1,
            DocxFixtureBuilder.TextRun("混凝土轴心抗压强度设计值f"),
            Script("c", VerticalPositionValues.Subscript),
            DocxFixtureBuilder.TextRun("取14.3N/mm"),
            Script("2", VerticalPositionValues.Superscript),
            DocxFixtureBuilder.TextRun("。字面平方符号单独保留为²，不得写成普通数字2。")));
        builder.Body.Add(Numbered(1, DocxFixtureBuilder.TextRun("箍筋采用HPB300，分布筋为Φ8@200。")));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Normal", DocxFixtureBuilder.TextRun("注：1. 本条为手工编号，程序不应再自动加一次编号。")));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Heading2", DocxFixtureBuilder.TextRun("构造要求")));
        builder.Body.Add(Numbered(0, new Run(
            new Text("框架梁纵向钢筋应通长设置。") { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve },
            new Break(),
            new Text("梁柱节点核心区箍筋应加密。") { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve })));
        builder.Body.Add(Numbered(1, DocxFixtureBuilder.TextRun("图中符号Φ20@200、规范编号GB 50010-2010应原样保留。")));
        return builder.Build();
    }

    private static Style BodyStyle()
    {
        return new Style(
            new StyleName { Val = "Normal" },
            new Aliases { Val = "正文" },
            new StyleRunProperties(
                new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri", EastAsia = "宋体" },
                new FontSize { Val = "24" },
                new Languages { Val = "en-US", EastAsia = "zh-CN" }))
        {
            Type = StyleValues.Paragraph,
            StyleId = "Normal",
            Default = true
        };
    }

    private static Style HeadingStyle(string styleId, string name, string alias, string basedOn, int outline, string size, string color, string before, string after)
    {
        return new Style(
            new StyleName { Val = name },
            new Aliases { Val = alias },
            new BasedOn { Val = basedOn },
            new NextParagraphStyle { Val = "Normal" },
            new StyleParagraphProperties(
                new KeepNext(),
                new SpacingBetweenLines { Before = before, After = after, Line = "360", LineRule = LineSpacingRuleValues.Auto },
                new OutlineLevel { Val = outline }),
            new StyleRunProperties(
                new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri", EastAsia = "黑体" },
                new Bold(),
                new Color { Val = color },
                new FontSize { Val = size }))
        {
            Type = StyleValues.Paragraph,
            StyleId = styleId
        };
    }

    private static AbstractNum DecimalList()
    {
        var abs = new AbstractNum(
            new MultiLevelType { Val = MultiLevelValues.HybridMultilevel },
            new Nsid { Val = "12AB34CD" })
        { AbstractNumberId = 0 };
        abs.AppendChild(Level(0, "%1.", 720));
        abs.AppendChild(Level(1, "%1.%2", 1140));
        return abs;
    }

    private static Level Level(int index, string text, int left)
    {
        return new Level(
            new StartNumberingValue { Val = 1 },
            new NumberingFormat { Val = NumberFormatValues.Decimal },
            new LevelText { Val = text },
            new LevelSuffix { Val = LevelSuffixValues.Space },
            new LevelJustification { Val = LevelJustificationValues.Left },
            new PreviousParagraphProperties(new Indentation { Left = left.ToString(), Hanging = "360" }))
        { LevelIndex = index };
    }

    private static Paragraph Numbered(int level, params OpenXmlElement[] inline)
    {
        var children = new List<OpenXmlElement>
        {
            new ParagraphProperties(
                new ParagraphStyleId { Val = "Normal" },
                new NumberingProperties(
                    new NumberingLevelReference { Val = level },
                    new NumberingId { Val = 1 }))
        };
        children.AddRange(inline);
        return new Paragraph(children);
    }

    private static Run Script(string text, VerticalPositionValues position)
    {
        return new Run(
            new RunProperties(new VerticalTextAlignment { Val = position }),
            new Text(text) { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve });
    }
}
