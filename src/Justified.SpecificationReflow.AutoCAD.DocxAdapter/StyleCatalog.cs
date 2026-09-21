using System;
using System.Collections.Generic;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;

namespace Justified.SpecificationReflow.AutoCAD.DocxAdapter;

internal sealed class StyleCatalog
{
    private readonly Dictionary<string, ParagraphStyle> _paragraphs;
    private readonly Dictionary<string, CharacterStyle> _characters;
    private readonly StyleMap _map;

    private StyleCatalog(
        Dictionary<string, ParagraphStyle> paragraphs,
        Dictionary<string, CharacterStyle> characters,
        StyleMap map,
        string? defaultParagraphStyleId)
    {
        _paragraphs = paragraphs;
        _characters = characters;
        _map = map;
        DefaultParagraphStyleId = defaultParagraphStyleId;
    }

    public string? DefaultParagraphStyleId { get; }

    public static StyleCatalog Load(MainDocumentPart? main, StyleMap map)
    {
        var paragraphs = new Dictionary<string, ParagraphStyle>(StringComparer.OrdinalIgnoreCase);
        var characters = new Dictionary<string, CharacterStyle>(StringComparer.OrdinalIgnoreCase);
        string? defaultId = null;
        var styles = main?.StyleDefinitionsPart?.Styles;
        if (styles == null) return new StyleCatalog(paragraphs, characters, map, null);

        foreach (var style in styles.Elements<Style>())
        {
            var id = StringOf(style.StyleId);
            if (id == null || id.Length == 0 || style.Type == null || !style.Type.HasValue) continue;
            if (style.Type.Value == StyleValues.Paragraph)
            {
                var info = new ParagraphStyle(
                    id,
                    NamesOf(style),
                    StringOf(style.BasedOn?.Val),
                    style.StyleRunProperties);
                paragraphs[id] = info;
                if (defaultId == null && IsTrue(style.Default)) defaultId = id;
            }
            else if (style.Type.Value == StyleValues.Character)
            {
                characters[id] = new CharacterStyle(id, StringOf(style.BasedOn?.Val), SemanticOf(style.StyleRunProperties));
            }
        }

        return new StyleCatalog(paragraphs, characters, map, defaultId);
    }

    // 直接样式命中正文才算正文。标题允许沿 basedOn 向上识别。
    // 因此“标题 3”即使基于正文，也不会被降成正文。
    public bool TryResolve(string? paragraphStyleId, out BlockType blockType, out ParagraphStyle? direct)
    {
        blockType = default;
        direct = null;
        var startId = paragraphStyleId ?? DefaultParagraphStyleId;
        if (startId == null || startId.Length == 0 || !_paragraphs.TryGetValue(startId, out var start)) return false;
        direct = start;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = start;
        var isDirect = true;
        while (current != null && seen.Add(current.StyleId))
        {
            var mapped = _map.Find(current.StyleId, current.Names);
            if (mapped == BlockType.Heading1 || mapped == BlockType.Heading2)
            {
                blockType = mapped.Value;
                return true;
            }

            if (mapped == BlockType.Paragraph && isDirect)
            {
                blockType = BlockType.Paragraph;
                return true;
            }

            isDirect = false;
            current = current.BasedOn != null && _paragraphs.TryGetValue(current.BasedOn, out var next) ? next : null;
        }

        return false;
    }

    public RunSemantic SemanticOfRunStyle(string? styleId)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = styleId;
        while (current != null && current.Length > 0 && seen.Add(current))
        {
            if (!_characters.TryGetValue(current, out var style)) return RunSemantic.Normal;
            if (style.Semantic != RunSemantic.Normal) return style.Semantic;
            current = style.BasedOn;
        }

        return RunSemantic.Normal;
    }

    public void AccumulateFormat(string? paragraphStyleId, FormatFlags flags)
    {
        var startId = paragraphStyleId ?? DefaultParagraphStyleId;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentId = startId;
        while (currentId != null && currentId.Length > 0 && seen.Add(currentId))
        {
            if (!_paragraphs.TryGetValue(currentId, out var style)) return;
            flags.Absorb(style.RunProperties);
            currentId = style.BasedOn;
        }
    }

    private static List<string> NamesOf(Style style)
    {
        var names = new List<string>();
        var name = StringOf(style.StyleName?.Val);
        if (name != null && name.Trim().Length > 0) names.Add(name.Trim());
        var aliases = StringOf(style.Aliases?.Val);
        if (aliases == null || aliases.Trim().Length == 0) return names;
        foreach (var part in aliases.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0) names.Add(trimmed);
        }

        return names;
    }

    private static RunSemantic SemanticOf(StyleRunProperties? properties)
    {
        var alignment = properties?.VerticalTextAlignment?.Val;
        if (alignment == null || !alignment.HasValue) return RunSemantic.Normal;
        if (alignment.Value == VerticalPositionValues.Superscript) return RunSemantic.Superscript;
        if (alignment.Value == VerticalPositionValues.Subscript) return RunSemantic.Subscript;
        return RunSemantic.Normal;
    }

    private static bool IsTrue(DocumentFormat.OpenXml.OnOffValue? value)
    {
        if (value == null || !value.HasValue) return false;
        return value;
    }

    private static string? StringOf(DocumentFormat.OpenXml.StringValue? value)
    {
        if (value == null || !value.HasValue) return null;
        return value.Value;
    }

    internal sealed class ParagraphStyle
    {
        public ParagraphStyle(string styleId, IReadOnlyList<string> names, string? basedOn, StyleRunProperties? runProperties)
        {
            StyleId = styleId;
            Names = names;
            BasedOn = basedOn;
            RunProperties = runProperties;
        }

        public string StyleId { get; }

        public IReadOnlyList<string> Names { get; }

        public string? BasedOn { get; }

        public StyleRunProperties? RunProperties { get; }
    }

    private sealed class CharacterStyle
    {
        public CharacterStyle(string styleId, string? basedOn, RunSemantic semantic)
        {
            StyleId = styleId;
            BasedOn = basedOn;
            Semantic = semantic;
        }

        public string StyleId { get; }

        public string? BasedOn { get; }

        public RunSemantic Semantic { get; }
    }
}
