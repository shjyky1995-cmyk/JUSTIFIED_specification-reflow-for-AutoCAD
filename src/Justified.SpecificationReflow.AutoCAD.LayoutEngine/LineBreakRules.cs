using System;
using System.Collections.Generic;

namespace Justified.SpecificationReflow.AutoCAD.LayoutEngine;

// PRD 6.3 的基本禁则，对应 LayoutEngineInfo.LineBreakRuleVersion。半角引号同时视为行首、行尾禁则。
internal static class LineBreakRules
{
    private static readonly HashSet<string> LineStartForbidden = new HashSet<string>(StringComparer.Ordinal)
    {
        "，", "。", "；", "：", "！", "？", "、", "）", "】", "》", "〉", "”", "’",
        ",", ".", ";", ":", "!", "?", ")", "]", "}", ">", "\"", "'"
    };

    private static readonly HashSet<string> LineEndForbidden = new HashSet<string>(StringComparer.Ordinal)
    {
        "（", "【", "《", "〈", "“", "‘",
        "(", "[", "{", "<", "\"", "'"
    };

    public static bool IsPending(string? version)
    {
        var text = version == null ? string.Empty : version.Trim();
        return text.Length == 0 || string.Equals(text, "pending", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsImplemented(string? version)
    {
        return string.Equals(version, LayoutEngineInfo.LineBreakRuleVersion, StringComparison.Ordinal);
    }

    public static bool IsLineStartForbidden(string text)
    {
        return LineStartForbidden.Contains(text);
    }

    public static bool IsLineEndForbidden(string text)
    {
        return LineEndForbidden.Contains(text);
    }
}
