using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Justified.SpecificationReflow.AutoCAD.LayoutEngine;

// 按源顺序切成中文单字、英文单词、数字和最长工程 token。不拆代理对或组合字符。
internal static class TextTokenizer
{
    internal sealed class Token
    {
        public string Text { get; set; } = string.Empty;

        public int Start { get; set; }

        public bool CollapsibleSpace { get; set; }

        public bool Splittable { get; set; }
    }

    // 较长的写法靠比较匹配长度取胜，顺序只决定等长时的优先。
    private static readonly Regex[] Engineering =
    {
        new Regex(@"\GGB/T +[0-9]+(?:[.\-][0-9]+)*", RegexOptions.CultureInvariant | RegexOptions.Compiled),
        new Regex(@"\GGB +[0-9]+(?:[.\-][0-9]+)*", RegexOptions.CultureInvariant | RegexOptions.Compiled),
        new Regex(@"\GHRB[0-9]+", RegexOptions.CultureInvariant | RegexOptions.Compiled),
        new Regex(@"\G[Φφϕ∅⌀][0-9]+(?:\.[0-9]+)?(?:@[0-9]+)?", RegexOptions.CultureInvariant | RegexOptions.Compiled),
        new Regex(@"\G[0-9]+(?:\.[0-9]+)?kN/m(?:²|2)", RegexOptions.CultureInvariant | RegexOptions.Compiled),
        new Regex(@"\G[0-9]+(?:\.[0-9]+)?(?:mm|cm|MPa|kPa|kN|kg)", RegexOptions.CultureInvariant | RegexOptions.Compiled),
        new Regex(@"\G[0-9]+(?:\.[0-9]+)?g(?![A-Za-z])", RegexOptions.CultureInvariant | RegexOptions.Compiled),
        new Regex(@"\G[0-9]+/[0-9]+", RegexOptions.CultureInvariant | RegexOptions.Compiled),
        new Regex(@"\GC[0-9]{2,3}(?![A-Za-z0-9])", RegexOptions.CultureInvariant | RegexOptions.Compiled)
    };

    public static bool TryTokenize(string text, out List<Token> tokens, out string? error)
    {
        tokens = new List<Token>();
        error = null;
        if (text.Length == 0) return true;

        var boundaries = ElementBoundaries(text);
        var index = 0;
        while (index < text.Length)
        {
            var element = StringInfo.GetNextTextElement(text, index);
            if (ContainsControl(element))
            {
                error = element == "\t"
                    ? "含有制表符。V1 不猜测缩进。"
                    : "含有无法排版的控制字符。";
                tokens.Clear();
                return false;
            }

            var engineering = LongestEngineering(text, index, boundaries);
            if (engineering > 0)
            {
                tokens.Add(Make(text, index, engineering, false));
                index += engineering;
                continue;
            }

            if (IsBreakSpace(element))
            {
                var start = index;
                var builder = new StringBuilder();
                while (index < text.Length)
                {
                    var next = StringInfo.GetNextTextElement(text, index);
                    if (!IsBreakSpace(next)) break;
                    builder.Append(next);
                    index += next.Length;
                }

                tokens.Add(new Token
                {
                    Text = builder.ToString(),
                    Start = start,
                    CollapsibleSpace = true,
                    Splittable = ElementCount(builder.ToString()) > 1
                });
                continue;
            }

            if (IsWordLetter(element))
            {
                var start = index;
                var builder = new StringBuilder();
                while (index < text.Length)
                {
                    var next = StringInfo.GetNextTextElement(text, index);
                    if (!IsWordLetter(next)) break;
                    builder.Append(next);
                    index += next.Length;
                }

                tokens.Add(Make(text, start, index - start, false));
                continue;
            }

            if (IsAsciiDigit(element))
            {
                var start = index;
                var dotted = false;
                while (index < text.Length)
                {
                    var next = StringInfo.GetNextTextElement(text, index);
                    if (IsAsciiDigit(next))
                    {
                        index += next.Length;
                        continue;
                    }

                    if (!dotted && next == "." && index + next.Length < text.Length && IsAsciiDigit(StringInfo.GetNextTextElement(text, index + next.Length)))
                    {
                        dotted = true;
                        index += next.Length;
                        continue;
                    }

                    break;
                }

                tokens.Add(Make(text, start, index - start, false));
                continue;
            }

            tokens.Add(Make(text, index, element.Length, false));
            index += element.Length;
        }

        return true;
    }

    public static int ElementCount(string text)
    {
        if (text.Length == 0) return 0;
        return StringInfo.ParseCombiningCharacters(text).Length;
    }

    private static Token Make(string text, int start, int length, bool space)
    {
        var value = text.Substring(start, length);
        return new Token
        {
            Text = value,
            Start = start,
            CollapsibleSpace = space,
            Splittable = ElementCount(value) > 1
        };
    }

    private static int LongestEngineering(string text, int index, HashSet<int> boundaries)
    {
        var best = 0;
        foreach (var pattern in Engineering)
        {
            var match = pattern.Match(text, index);
            if (!match.Success || match.Index != index || match.Length <= best) continue;
            var end = index + match.Length;
            if (end != text.Length && !boundaries.Contains(end)) continue;
            best = match.Length;
        }

        return best;
    }

    private static HashSet<int> ElementBoundaries(string text)
    {
        var boundaries = new HashSet<int> { text.Length };
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
            boundaries.Add(enumerator.ElementIndex);
        return boundaries;
    }

    private static bool ContainsControl(string element)
    {
        foreach (var character in element)
        {
            if (char.IsControl(character)) return true;
        }

        return false;
    }

    private static bool IsBreakSpace(string element)
    {
        return element == " " || element == "\u3000";
    }

    private static bool IsAsciiDigit(string element)
    {
        return element.Length == 1 && element[0] >= '0' && element[0] <= '9';
    }

    // 汉字属于 OtherLetter，不能用 IsLetter，否则整段中文会粘成一个不可断词。
    private static bool IsWordLetter(string element)
    {
        if (element.Length == 0 || char.IsHighSurrogate(element[0])) return false;
        var category = char.GetUnicodeCategory(element[0]);
        return category == UnicodeCategory.UppercaseLetter
            || category == UnicodeCategory.LowercaseLetter
            || category == UnicodeCategory.TitlecaseLetter
            || category == UnicodeCategory.ModifierLetter;
    }
}
