using System;
using System.Collections.Generic;
using System.Threading;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Layout;
using Justified.SpecificationReflow.AutoCAD.Contracts.Rendering;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.Contracts.Templates;

namespace Justified.SpecificationReflow.AutoCAD.Application;

public sealed class NotePlacementResult
{
    public bool Success { get; set; }

    public List<PlacedText> Texts { get; set; } = new List<PlacedText>();

    public List<Diagnostic> Diagnostics { get; set; } = new List<Diagnostic>();
}

// 把排版结果换成 DBText 的世界坐标。有错误时不返回任何文字，避免半份结果。
public static class NotePlacement
{
    public static NotePlacementResult Create(LayoutResult layout, LayoutTemplate template, InstitutionStandard standard, RenderTransform transform, CancellationToken cancellationToken)
    {
        if (layout == null) throw new ArgumentNullException(nameof(layout));
        if (template == null) throw new ArgumentNullException(nameof(template));
        if (standard == null) throw new ArgumentNullException(nameof(standard));
        if (transform == null) throw new ArgumentNullException(nameof(transform));

        var result = new NotePlacementResult();
        if (cancellationToken.IsCancellationRequested)
        {
            result.Diagnostics.Add(Problem(DiagnosticCodes.Cancelled, "落图已取消。"));
            return result;
        }

        if (layout.Diagnostics != null)
        {
            foreach (var diagnostic in layout.Diagnostics)
            {
                if (diagnostic != null) result.Diagnostics.Add(diagnostic);
            }
        }

        if (HasError(result.Diagnostics))
            return result;

        if (!Finite(transform.UnitScale) || transform.UnitScale <= 0)
        {
            result.Diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "单位比例必须是正数。未知单位时不能猜测。"));
            return result;
        }

        if (transform.TargetSpace != TargetSpace.Model)
        {
            result.Diagnostics.Add(Problem(DiagnosticCodes.ECadEnv, "当前只在模型空间生成，文字保持世界坐标水平。"));
            return result;
        }

        if (!Finite(transform.AnchorWcs.X) || !Finite(transform.AnchorWcs.Y))
        {
            result.Diagnostics.Add(Problem(DiagnosticCodes.ECadEnv, "插入点必须是有限坐标。"));
            return result;
        }

        var layerName = standard.LayerPolicy == null ? null : standard.LayerPolicy.LayerName;
        if (string.IsNullOrWhiteSpace(layerName))
        {
            result.Diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "院标没有图层名。"));
            return result;
        }

        if (template.Columns == null || template.Columns.Count == 0)
        {
            result.Diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "模板没有栏，不能落图。"));
            return result;
        }

        if (layout.Pages == null)
        {
            result.Success = true;
            return result;
        }

        foreach (var page in layout.Pages)
        {
            if (page?.Columns == null) continue;
            foreach (var column in page.Columns)
            {
                if (column == null || column.ColumnIndex < 0 || column.ColumnIndex >= template.Columns.Count)
                {
                    result.Texts.Clear();
                    result.Diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "排版栏与模板栏对不上。"));
                    return result;
                }

                var geometry = template.Columns[column.ColumnIndex];
                if (column.Rows == null) continue;
                foreach (var row in column.Rows)
                {
                    if (row == null || row.Occupancy == Occupancy.Spacer || row.VisualLine == null)
                        continue;
                    if (row.VisualLine.RenderRuns == null || row.VisualLine.RenderRuns.Count == 0)
                        continue;
                    foreach (var run in row.VisualLine.RenderRuns)
                    {
                        if (run == null || run.Text == null || run.Text.Length == 0) continue;
                        if (run.ResolvedStyle == null)
                        {
                            result.Texts.Clear();
                            result.Diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "文字 run 没有解析出的样式。"));
                            return result;
                        }

                        var font = run.ResolvedStyle.Font;
                        if (font == null || string.IsNullOrWhiteSpace(font.FileIdentity))
                        {
                            result.Texts.Clear();
                            result.Diagnostics.Add(Problem(DiagnosticCodes.EFontMissing, "文字样式没有字体文件。"));
                            return result;
                        }

                        result.Texts.Add(new PlacedText
                        {
                            Text = run.Text,
                            Position = NoteCoordinates.World(transform.AnchorWcs, transform.UnitScale, page.PageOffset, geometry.Left, row.Baseline, run.RelativeOrigin, run.BaselineOffset),
                            Height = run.ResolvedStyle.TextHeight * transform.UnitScale,
                            WidthFactor = run.ResolvedStyle.WidthFactor,
                            ObliqueDegrees = run.ResolvedStyle.ObliqueAngle,
                            StyleName = StyleName(standard, run.ResolvedStyle.StyleId),
                            FontFile = font.FileIdentity ?? string.Empty,
                            BigFont = font.BigFont ?? string.Empty,
                            Layer = layerName ?? string.Empty,
                            PageIndex = page.PageIndex,
                            ColumnIndex = column.ColumnIndex,
                            RowIndex = row.RowIndex
                        });
                    }
                }
            }
        }

        result.Success = true;
        return result;
    }

    public static string StyleName(InstitutionStandard standard, string role)
    {
        return (standard.StandardId ?? string.Empty) + "-" + (standard.Version ?? string.Empty) + "-" + (role ?? string.Empty);
    }

    private static bool HasError(List<Diagnostic> diagnostics)
    {
        if (diagnostics == null) return false;
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic != null && diagnostic.Severity == Severity.Error) return true;
        }

        return false;
    }

    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static Diagnostic Problem(string code, string message)
    {
        return new Diagnostic
        {
            Code = code,
            Severity = Severity.Error,
            Stage = DiagnosticStage.Render,
            Message = message
        };
    }
}
