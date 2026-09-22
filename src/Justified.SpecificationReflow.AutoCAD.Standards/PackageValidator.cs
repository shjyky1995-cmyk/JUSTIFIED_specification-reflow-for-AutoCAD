using System;
using System.Collections.Generic;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.Contracts.Templates;

namespace Justified.SpecificationReflow.AutoCAD.Standards;

public sealed class InstalledStyleSnapshot
{
    public string Name { get; set; } = string.Empty;

    public string FontFamily { get; set; } = string.Empty;

    public double TextHeight { get; set; }

    public double WidthFactor { get; set; }

    public double ObliqueAngle { get; set; }
}

// 发布校验只拒绝缺值和非法几何。它不补默认字高、栏宽或行数。
public sealed class PackageValidator
{
    public IReadOnlyList<Diagnostic> ValidateStandard(InstitutionStandard standard)
    {
        var diagnostics = new List<Diagnostic>();
        if (standard == null)
        {
            diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "院标为空。", "standard"));
            return diagnostics;
        }

        RequireText(diagnostics, standard.StandardId, "standardId", "院标 standardId 不能为空。");
        RequireVersion(diagnostics, standard.Version, "version");
        if (!Positive(standard.TextHeight))
            diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "字高必须是正数，不能用 0 或空值代替标定。", "textHeight"));
        if (!Positive(standard.WidthFactor))
            diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "宽度系数必须是正数。", "widthFactor"));
        if (!Finite(standard.ObliqueAngle))
            diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "倾斜角必须是有限数值。", "obliqueAngle"));
        if (!Positive(standard.RowPitch))
            diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "行距必须是正数。", "rowPitch"));
        if (!Finite(standard.MeasurementTolerance) || standard.MeasurementTolerance < 0)
            diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "测量公差必须是不小于 0 的有限数值。", "measurementTolerance"));
        RequireText(diagnostics, standard.SymbolMapVersion, "symbolMapVersion", "符号映射版本不能为空。");
        RequireText(diagnostics, standard.LineBreakRuleVersion, "lineBreakRuleVersion", "换行规则版本不能为空。");
        foreach (var problem in ScriptCalibration.Problems(standard))
            diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "上下标标定不完整：" + problem, "superscript/subscript"));
        if (standard.LayerPolicy == null || string.IsNullOrWhiteSpace(standard.LayerPolicy.LayerName))
            diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "图层策略缺少图层名。", "layerPolicy.layerName"));

        var families = new HashSet<string>(StringComparer.Ordinal);
        if (standard.FontProfile?.Fonts == null || standard.FontProfile.Fonts.Count == 0)
        {
            diagnostics.Add(Error(DiagnosticCodes.EFontMissing, "字体档案为空。", "fontProfile"));
        }
        else
        {
            for (var i = 0; i < standard.FontProfile.Fonts.Count; i++)
            {
                var font = standard.FontProfile.Fonts[i];
                var path = "fontProfile.fonts[" + i + "]";
                if (font == null || string.IsNullOrWhiteSpace(font.Family))
                    diagnostics.Add(Error(DiagnosticCodes.EFontMissing, "字体缺少族名。", path + ".family"));
                else if (!families.Add(font.Family))
                    diagnostics.Add(Error(DiagnosticCodes.EFontMissing, "字体族名重复：" + font.Family + "。", path + ".family"));
                if (font == null || string.IsNullOrWhiteSpace(font.FileIdentity))
                    diagnostics.Add(Error(DiagnosticCodes.EFontMissing, "字体缺少文件身份，不能发布。", path + ".fileIdentity"));
            }
        }

        RequireStyle(diagnostics, standard, "heading1", true);
        RequireStyle(diagnostics, standard, "heading2", true);
        RequireStyle(diagnostics, standard, "body", true);
        if (standard.Styles != null)
        {
            foreach (var pair in standard.Styles)
            {
                if (pair.Key != "heading1" && pair.Key != "heading2" && pair.Key != "body")
                    diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "V1 院标只接收 heading1、heading2、body。", "styles." + pair.Key));
            }
        }

        return diagnostics;
    }

    public IReadOnlyList<Diagnostic> ValidateTemplate(LayoutTemplate template, InstitutionStandard? standard)
    {
        var diagnostics = new List<Diagnostic>();
        if (template == null)
        {
            diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "模板为空。", "template"));
            return diagnostics;
        }

        RequireText(diagnostics, template.TemplateId, "templateId", "模板 templateId 不能为空。");
        RequireVersion(diagnostics, template.Version, "version");
        RequireText(diagnostics, template.DisciplineCode, "disciplineCode", "模板 disciplineCode 不能为空。");
        if (template.Status != TemplateStatus.Calibrated)
            diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "模板尚未标定，不能发布。", "status"));
        if (template.FramePolicy == FramePolicy.StandardBlock)
            diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "standardBlock 需要已标定的图框块。当前没有这块资产，不能发布。", "framePolicy"));
        if (!Finite(template.Anchor?.X) || !Finite(template.Anchor?.Y))
            diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "锚点坐标必须是有限数值。", "anchor"));
        if (template.StandardRef == null || string.IsNullOrWhiteSpace(template.StandardRef.Id) || string.IsNullOrWhiteSpace(template.StandardRef.Version))
            diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "模板必须引用院标 id 和 version。", "standardRef"));
        else if (standard != null && (template.StandardRef.Id != standard.StandardId || template.StandardRef.Version != standard.Version))
            diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "模板引用的院标与正在校验的院标不一致。", "standardRef"));

        var bounds = template.PageBounds;
        var width = bounds == null ? double.NaN : bounds.Right - bounds.Left;
        var height = bounds == null ? double.NaN : bounds.Top - bounds.Bottom;
        if (!Positive(width) || !Positive(height))
            diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "页面边界必须是正的宽度和高度。", "pageBounds"));
        if (template.PageStep == null)
            diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "pageStep 不能为空，也不能填 0 后当作已标定。", "pageStep"));
        else if (!Finite(template.PageStep.Value.X) || !Finite(template.PageStep.Value.Y) || (Positive(width) && Positive(height) && Math.Abs(template.PageStep.Value.X) + 1e-9 < width && Math.Abs(template.PageStep.Value.Y) + 1e-9 < height))
            diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "pageStep 必须让相邻页不重叠。", "pageStep"));

        if (template.Columns == null || template.Columns.Count == 0)
        {
            diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "模板没有栏。不能把空栏当成可排版模板。", "columns"));
            return diagnostics;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < template.Columns.Count; i++)
        {
            var column = template.Columns[i];
            var path = "columns[" + i + "]";
            if (column == null)
            {
                diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "栏为空。", path));
                continue;
            }

            if (string.IsNullOrWhiteSpace(column.ColumnId) || !ids.Add(column.ColumnId))
                diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "栏 id 必须唯一且非空。", path + ".columnId"));
            if (!(column.Right > column.Left) || !(column.Top > column.Bottom))
                diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "栏的左右、上下边界必须是正跨度。", path));
            if (column.RowCount <= 0)
                diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "行数必须是正整数，不能由字高临时推算。", path + ".rowCount"));
            if (bounds != null && Positive(width) && Positive(height) && (column.Left < bounds.Left || column.Right > bounds.Right || column.Bottom < bounds.Bottom || column.Top > bounds.Top))
                diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "栏超出页面边界。", path));
            if (standard != null && Positive(standard.RowPitch) && column.RowCount > 0 && Finite(column.FirstBaselineY))
            {
                var last = column.FirstBaselineY - (column.RowCount - 1) * standard.RowPitch;
                if (column.FirstBaselineY > column.Top + 1e-9 || last < column.Bottom - 1e-9)
                    diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "首行或末行基线超出栏高。行数不能靠挤压缩进栏内。", path + ".firstBaselineY"));
            }
            else if (!Finite(column.FirstBaselineY))
            {
                diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "首行基线必须是有限数值。", path + ".firstBaselineY"));
            }
        }

        return diagnostics;
    }

    public IReadOnlyList<Diagnostic> ValidateFonts(InstitutionStandard standard, Func<string, bool> fileExists)
    {
        var diagnostics = new List<Diagnostic>(ValidateStandard(standard));
        if (fileExists == null) throw new ArgumentNullException(nameof(fileExists));
        if (standard?.FontProfile?.Fonts == null) return diagnostics;
        for (var i = 0; i < standard.FontProfile.Fonts.Count; i++)
        {
            var font = standard.FontProfile.Fonts[i];
            if (font == null) continue;
            Probe(diagnostics, font.FileIdentity, "fontProfile.fonts[" + i + "].fileIdentity", fileExists);
            if (!string.IsNullOrWhiteSpace(font.BigFont))
                Probe(diagnostics, font.BigFont!, "fontProfile.fonts[" + i + "].bigFont", fileExists);
        }

        return diagnostics;
    }

    public IReadOnlyList<Diagnostic> ValidateStyleReuse(InstitutionStandard standard, IReadOnlyList<InstalledStyleSnapshot> installed)
    {
        var diagnostics = new List<Diagnostic>();
        if (installed == null) throw new ArgumentNullException(nameof(installed));
        if (standard == null) return diagnostics;
        foreach (var role in new[] { "heading1", "heading2", "body" })
        {
            if (standard.Styles == null || !standard.Styles.TryGetValue(role, out var style) || style == null) continue;
            var name = VersionedStyleName(standard, role);
            foreach (var existing in installed)
            {
                if (existing == null || !string.Equals(existing.Name, name, StringComparison.Ordinal)) continue;
                if (existing.FontFamily != style.FontRef
                    || !Same(existing.TextHeight, standard.TextHeight)
                    || !Same(existing.WidthFactor, standard.WidthFactor)
                    || !Same(existing.ObliqueAngle, standard.ObliqueAngle))
                {
                    diagnostics.Add(Error(DiagnosticCodes.EStyleConflict, "图中已有样式 " + name + "，且与院标不同。不会修改旧样式。", "styles." + role));
                }
            }
        }

        return diagnostics;
    }

    public static string VersionedStyleName(InstitutionStandard standard, string role)
    {
        return (standard?.StandardId ?? string.Empty) + "-" + (standard?.Version ?? string.Empty) + "-" + role;
    }

    private static void RequireStyle(List<Diagnostic> diagnostics, InstitutionStandard standard, string role, bool slotsMustBeZero)
    {
        if (standard.Styles == null || !standard.Styles.TryGetValue(role, out var style) || style == null)
        {
            diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "缺少样式 " + role + "。", "styles." + role));
            return;
        }

        if (string.IsNullOrWhiteSpace(style.FontRef))
            diagnostics.Add(Error(DiagnosticCodes.EFontMissing, role + " 没有字体引用。", "styles." + role + ".fontRef"));
        else if (standard.FontProfile?.Fonts == null || !standard.FontProfile.Fonts.Exists(font => font != null && font.Family == style.FontRef))
            diagnostics.Add(Error(DiagnosticCodes.EFontMissing, role + " 引用了不存在的字体 " + style.FontRef + "。", "styles." + role + ".fontRef"));
        if (!Finite(style.Indent) || style.Indent < 0 || !Finite(style.HangingIndent) || style.HangingIndent < 0)
            diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, role + " 的缩进必须是不小于 0 的有限数值。", "styles." + role + ".indent"));
        if (slotsMustBeZero && (style.BeforeSlots != 0 || style.AfterSlots != 0))
            diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, role + " 的前后槽必须为 0。字高只有院标这一处，标题不另插空槽。", "styles." + role + ".beforeSlots"));
        if (style.BeforeSlots < 0 || style.AfterSlots < 0)
            diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, role + " 的槽数不能为负。", "styles." + role + ".beforeSlots"));
    }

    private static void Probe(List<Diagnostic> diagnostics, string? identity, string path, Func<string, bool> fileExists)
    {
        if (string.IsNullOrWhiteSpace(identity)) return;
        bool found;
        try
        {
            found = fileExists(identity!);
        }
        catch (Exception error)
        {
            diagnostics.Add(Error(DiagnosticCodes.EFontMissing, "检查字体失败：" + error.Message, path));
            return;
        }

        if (!found)
            diagnostics.Add(Error(DiagnosticCodes.EFontMissing, "找不到字体文件 " + identity + "。不会用其他字体代替。", path));
    }

    private static void RequireText(List<Diagnostic> diagnostics, string? value, string path, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, message, path));
    }

    private static void RequireVersion(List<Diagnostic> diagnostics, string? version, string path)
    {
        if (string.IsNullOrWhiteSpace(version))
            diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "版本不能为空。", path));
        else if (string.Equals(version, "pending", StringComparison.OrdinalIgnoreCase))
            diagnostics.Add(Error(DiagnosticCodes.ETemplateInvalid, "版本 pending 表示尚未标定，不能发布。", path));
    }

    private static bool Positive(double? value) => value != null && Finite(value) && value.Value > 0;

    private static bool Finite(double? value) => value != null && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value);

    private static bool Same(double left, double right) => Finite(left) && Finite(right) && Math.Abs(left - right) <= 1e-9;

    private static Diagnostic Error(string code, string message, string path)
    {
        return new Diagnostic
        {
            Code = code,
            Severity = Severity.Error,
            Stage = DiagnosticStage.Standard,
            Message = message,
            Details = new Dictionary<string, string> { ["path"] = path }
        };
    }
}
