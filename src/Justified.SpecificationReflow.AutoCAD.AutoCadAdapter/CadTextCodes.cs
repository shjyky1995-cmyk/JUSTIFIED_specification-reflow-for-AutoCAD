using System;
using System.IO;

namespace Justified.SpecificationReflow.AutoCAD.AutoCadAdapter;

// 当前 tssdeng 字体把钢筋字形放在 132 号位。测量和最终 DBText 必须使用同一串控制码。
internal static class CadTextCodes
{
    public static string Encode(string text, string? fontFile)
    {
        if (string.IsNullOrEmpty(text)
            || !string.Equals(Path.GetFileName(fontFile), "tssdeng.shx", StringComparison.OrdinalIgnoreCase))
            return text;

        return text.Replace("φ", "%%132").Replace("Φ", "%%132");
    }
}
