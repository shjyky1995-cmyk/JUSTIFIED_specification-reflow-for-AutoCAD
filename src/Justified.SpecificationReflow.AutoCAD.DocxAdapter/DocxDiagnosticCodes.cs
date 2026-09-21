namespace Justified.SpecificationReflow.AutoCAD.DocxAdapter;

// 解析阶段的 info 码。不写入 Contracts.DiagnosticCodes，避免扩大已冻结的错误码表面。
public static class DocxDiagnosticCodes
{
    public const string IgnoredFormat = "I_IGNORED_FORMAT";

    public const string IgnoredPagination = "I_IGNORED_PAGINATION";

    public const string IgnoredHeaderFooter = "I_IGNORED_HEADER_FOOTER";

    public const string MacroNotExecuted = "I_MACRO_NOT_EXECUTED";
}
