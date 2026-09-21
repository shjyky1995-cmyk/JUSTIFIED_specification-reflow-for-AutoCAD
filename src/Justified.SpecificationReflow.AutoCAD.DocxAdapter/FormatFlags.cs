using System;
using System.Collections.Generic;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Word = DocumentFormat.OpenXml.Wordprocessing;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;

namespace Justified.SpecificationReflow.AutoCAD.DocxAdapter;

internal sealed class FormatFlags
{
    public bool Bold { get; private set; }

    public bool Italic { get; private set; }

    public bool Underline { get; private set; }

    public bool Highlight { get; private set; }

    public string? Color { get; private set; }

    public string? FontSize { get; private set; }

    public string? Font { get; private set; }

    public bool Any => Bold || Italic || Underline || Highlight || Color != null || FontSize != null || Font != null;

    public void Absorb(OpenXmlElement? properties)
    {
        if (properties == null) return;
        foreach (var child in properties.ChildElements)
        {
            switch (child)
            {
                case Word.Bold:
                case Word.BoldComplexScript:
                    if (IsOn((OnOffType)child)) Bold = true;
                    break;
                case Word.Italic:
                case Word.ItalicComplexScript:
                    if (IsOn((OnOffType)child)) Italic = true;
                    break;
                case Word.Underline underline:
                    var underlineValue = underline.Val;
                    if (underlineValue == null || !underlineValue.HasValue || underlineValue.Value != Word.UnderlineValues.None)
                        Underline = true;
                    break;
                case Word.Color color:
                    var colorValue = color.Val == null || !color.Val.HasValue ? null : color.Val.Value;
                    if (colorValue != null && colorValue.Length > 0 && !colorValue.Equals("auto", StringComparison.OrdinalIgnoreCase))
                        Color = colorValue;
                    else if (color.ThemeColor != null && color.ThemeColor.HasValue)
                        Color = "theme";
                    break;
                case Word.Highlight:
                    Highlight = true;
                    break;
                case Word.FontSize size:
                    var sizeValue = size.Val == null || !size.Val.HasValue ? null : size.Val.Value;
                    if (!string.IsNullOrEmpty(sizeValue)) FontSize = sizeValue;
                    break;
                case Word.RunFonts fonts:
                    var font = First(fonts.EastAsia, fonts.Ascii, fonts.HighAnsi);
                    if (!string.IsNullOrEmpty(font)) Font = font;
                    break;
            }
        }
    }

    public string Summary()
    {
        var parts = new List<string>();
        if (Bold) parts.Add("bold");
        if (Italic) parts.Add("italic");
        if (Underline) parts.Add("underline");
        if (Highlight) parts.Add("highlight");
        if (Color != null) parts.Add("color=" + Color);
        if (FontSize != null) parts.Add("fontSize=" + FontSize);
        if (Font != null) parts.Add("font=" + Font);
        return string.Join(",", parts);
    }

    public static RunSemantic? Vertical(OpenXmlElement? properties)
    {
        if (properties == null) return null;
        foreach (var child in properties.ChildElements)
        {
            if (child is not Word.VerticalTextAlignment alignment || alignment.Val == null || !alignment.Val.HasValue) continue;
            if (alignment.Val.Value == VerticalPositionValues.Superscript) return RunSemantic.Superscript;
            if (alignment.Val.Value == VerticalPositionValues.Subscript) return RunSemantic.Subscript;
            return RunSemantic.Normal;
        }

        return null;
    }

    private static bool IsOn(OnOffType element)
    {
        if (element.Val == null || !element.Val.HasValue) return true;
        return element.Val;
    }

    private static string? First(params StringValue?[] values)
    {
        foreach (var value in values)
        {
            if (value != null && value.HasValue && !string.IsNullOrEmpty(value.Value)) return value.Value;
        }

        return null;
    }
}
