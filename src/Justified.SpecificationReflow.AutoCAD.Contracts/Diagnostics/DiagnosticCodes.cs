namespace Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;

// 错误码取值与 PRD 第 10 节一致；E_SCHEMA_INVALID 为 T03 新增，用于一般协议违例
// （缺字段、类型错误、未知属性、非法数值等），不改变既有错误码语义。
public static class DiagnosticCodes
{
    public const string EDocxRead = "E_DOCX_READ";
    public const string EUnsupportedContent = "E_UNSUPPORTED_CONTENT";
    public const string ENumbering = "E_NUMBERING";
    public const string ESchemaVersion = "E_SCHEMA_VERSION";
    public const string ESchemaInvalid = "E_SCHEMA_INVALID";
    public const string ETemplateInvalid = "E_TEMPLATE_INVALID";
    public const string EFontMissing = "E_FONT_MISSING";
    public const string ENoLegalBreak = "E_NO_LEGAL_BREAK";
    public const string WTokenSplit = "W_TOKEN_SPLIT";
    public const string ECadEnv = "E_CAD_ENV";
    public const string EStyleConflict = "E_STYLE_CONFLICT";
    public const string ERenderFailed = "E_RENDER_FAILED";
    public const string EResourceLimit = "E_RESOURCE_LIMIT";
    public const string Cancelled = "CANCELLED";
}
