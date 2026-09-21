namespace Justified.SpecificationReflow.AutoCAD.DocxAdapter;

// 样式映射、文档身份和资源上限不在 T03 冻结的 ParseProfile 上。
// 院标包落地前由调用方显式传入，解析器不填静默默认值。
public sealed class DocxParseOptions
{
    public string DocumentId { get; set; } = string.Empty;

    public string DisciplineCode { get; set; } = string.Empty;

    public StyleMap StyleMap { get; set; } = new StyleMap();

    // null 表示尚未标定（CAL-09），不启用数值上限。
    public long? MaxSourceBytes { get; set; }

    public long? MaxUncompressedBytes { get; set; }
}
