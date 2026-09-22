namespace Justified.SpecificationReflow.AutoCAD.Contracts.Rendering;

// 单次生成的资源上限。防止超大说明把 AutoCAD 会话拖死；超出即拒绝，不悄悄截断。
public sealed class GenerationLimits
{
    public int MaxPages { get; set; } = 200;

    public int MaxTexts { get; set; } = 4000;

    public int MaxBlocks { get; set; } = 500;

    public int MaxCharacters { get; set; } = 200000;

    public static GenerationLimits Default
    {
        get { return new GenerationLimits(); }
    }
}
