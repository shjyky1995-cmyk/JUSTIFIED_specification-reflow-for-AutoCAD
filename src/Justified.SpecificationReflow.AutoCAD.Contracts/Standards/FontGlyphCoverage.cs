using System;
using System.Collections.Generic;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Standards;

// 实测字形缺失清单：AutoCAD 2021 + tssdeng.shx/tssdchn.shx 组合下确认缺失的 Unicode 字符。
// 命中即阻断（E_FONT_MISSING），不允许以问号或方框落图（PRD 10）。清单按字体文件身份维护，
// 新字体资产须重新实测后扩充；未列出的字符不预设缺失。
public static class FontGlyphCoverage
{
    private static readonly Dictionary<string, HashSet<char>> Missing = Build();

    // 返回第一个命中的缺失字符；没有命中返回 null。大字体优先，其次西文字体。
    public static char? FirstMissingGlyph(string? fileIdentity, string? bigFont, string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        foreach (var identity in new[] { bigFont, fileIdentity })
        {
            if (string.IsNullOrWhiteSpace(identity)) continue;
            var key = identity!;
            if (!Missing.TryGetValue(key, out var set)) continue;
            foreach (var character in text)
            {
                if (set.Contains(character)) return character;
            }
        }

        return null;
    }

    private static Dictionary<string, HashSet<char>> Build()
    {
        var circled = new HashSet<char>();
        foreach (var character in "\u2460\u2461\u2462\u2463\u2464\u2465\u2466\u2467\u2468\u2469\u246A\u246B\u246C\u246D\u246E\u246F\u2470\u2471\u2472\u2473")
            circled.Add(character);
        foreach (var character in "\u3251\u3252\u3253\u3254\u3255\u3256\u3257\u3258\u3259\u325A\u325B\u325C\u325D\u325E\u325F\u32B1\u32B2\u32B3\u32B4\u32B5\u32B6\u32B7\u32B8\u32B9\u32BA\u32BB\u32BC\u32BD\u32BE\u32BF")
            circled.Add(character);

        var map = new Dictionary<string, HashSet<char>>(StringComparer.OrdinalIgnoreCase);
        map["tssdchn.shx"] = circled;
        return map;
    }
}
