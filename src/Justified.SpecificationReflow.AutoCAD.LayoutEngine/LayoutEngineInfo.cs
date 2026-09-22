namespace Justified.SpecificationReflow.AutoCAD.LayoutEngine;

// 引擎实现的换行规则身份。生产草案的 lineBreakRuleVersion 仍是 pending，不能把本常量写回草案来冒充已发布。
public static class LayoutEngineInfo
{
    public const string EngineVersion = "0.1.0";

    public const string SchemaVersion = "1.0";

    public const string LineBreakRuleVersion = "jsr-linebreak-1.0.0";

    // 失控保护，不是 CAL-09 的页数上限。正常满页不会触达。
    public const int DefaultSafetyPageLimit = 10000;
}
