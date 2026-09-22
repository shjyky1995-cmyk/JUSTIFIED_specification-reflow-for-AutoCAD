using System;
using System.Collections.Generic;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Layout;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Standards;

// 上下标标定：缩放与基线偏移都是相对正文字高的比例。项目制定默认值见 TEXT_FORMAT_V1.md；
// 草案可以缺省（由 Standards.DraftSessionLoader 在内存补齐），正式包缺省即拒绝。
public static class ScriptCalibration
{
    public const double SuperscriptScale = 0.7;

    public const double SuperscriptRise = 0.35;

    public const double SubscriptScale = 0.7;

    public const double SubscriptDrop = 0.2;

    public static IReadOnlyList<string> Problems(InstitutionStandard? standard)
    {
        var problems = new List<string>();
        if (standard == null)
        {
            problems.Add("院标为空，无法检查上下标标定。");
            return problems;
        }

        CheckScale(problems, standard.SuperscriptScale, "上标缩放");
        CheckRatio(problems, standard.SuperscriptRise, "上标抬升");
        CheckScale(problems, standard.SubscriptScale, "下标缩放");
        CheckRatio(problems, standard.SubscriptDrop, "下标下沉");
        return problems;
    }

    public static bool IsComplete(InstitutionStandard? standard)
    {
        return Problems(standard).Count == 0;
    }

    // 上下标段落的行内样式与基线偏移（毫米，未乘单位比例）。语义保留，供落图与样式命名使用。
    public static ResolvedStyleForScript Apply(ResolvedStyle style, RunSemantic semantic)
    {
        if (style == null) throw new ArgumentNullException(nameof(style));
        if (semantic == RunSemantic.Normal)
            return new ResolvedStyleForScript(Copy(style, RunSemantic.Normal, style.TextHeight), 0);
        if (semantic == RunSemantic.Superscript)
            return new ResolvedStyleForScript(Copy(style, RunSemantic.Superscript, SuperscriptScale * style.TextHeight), SuperscriptRise * style.TextHeight);
        if (semantic == RunSemantic.Subscript)
            return new ResolvedStyleForScript(Copy(style, RunSemantic.Subscript, SubscriptScale * style.TextHeight), -SubscriptDrop * style.TextHeight);
        throw new ArgumentOutOfRangeException(nameof(semantic), "不支持的语义。");
    }

    private static ResolvedStyle Copy(ResolvedStyle style, RunSemantic semantic, double textHeight)
    {
        return new ResolvedStyle
        {
            StyleId = style.StyleId,
            Semantic = semantic,
            Font = style.Font,
            TextHeight = textHeight,
            WidthFactor = style.WidthFactor,
            ObliqueAngle = style.ObliqueAngle
        };
    }

    private static void CheckScale(List<string> problems, double? value, string label)
    {
        if (value == null)
        {
            problems.Add(label + "比例尚未标定。");
            return;
        }

        var number = value.Value;
        if (double.IsNaN(number) || double.IsInfinity(number) || number <= 0 || number > 1)
            problems.Add(label + "比例必须是 0 到 1 之间的有限数值。");
    }

    private static void CheckRatio(List<string> problems, double? value, string label)
    {
        if (value == null)
        {
            problems.Add(label + "比例尚未标定。");
            return;
        }

        var number = value.Value;
        if (double.IsNaN(number) || double.IsInfinity(number) || number < 0 || number > 1)
            problems.Add(label + "比例必须是 0 到 1 之间的有限数值。");
    }
}

// 上下标 run 的行内样式（字高已缩放）与基线偏移（毫米，未乘单位比例）。
public readonly struct ResolvedStyleForScript
{
    public ResolvedStyleForScript(ResolvedStyle style, double baselineOffset)
    {
        Style = style;
        BaselineOffset = baselineOffset;
    }

    public ResolvedStyle Style { get; }

    public double BaselineOffset { get; }
}
