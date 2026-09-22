using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Geometry;
using Justified.SpecificationReflow.AutoCAD.Contracts.Layout;
using Justified.SpecificationReflow.AutoCAD.Contracts.Ports;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.Contracts.Templates;

namespace Justified.SpecificationReflow.AutoCAD.LayoutEngine;

// 按当前栏的可用宽度逐行换行，再写入固定行槽。栏宽、行距和缩进都来自当次院标与模板。
public sealed class SpecificationLayoutEngine : ILayoutEngine
{
    private readonly int _safetyPageLimit;

    public SpecificationLayoutEngine()
        : this(LayoutEngineInfo.DefaultSafetyPageLimit)
    {
    }

    public SpecificationLayoutEngine(int safetyPageLimit)
    {
        if (safetyPageLimit <= 0) throw new ArgumentOutOfRangeException(nameof(safetyPageLimit), "页数保护上限必须是正数。");
        _safetyPageLimit = safetyPageLimit;
    }

    public LayoutResult Layout(Document document, InstitutionStandard standard, LayoutTemplate template, ITextMeasureService measure, CancellationToken cancellationToken)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        if (standard == null) throw new ArgumentNullException(nameof(standard));
        if (template == null) throw new ArgumentNullException(nameof(template));
        if (measure == null) throw new ArgumentNullException(nameof(measure));

        var watch = Stopwatch.StartNew();
        var documentHash = DocumentHash(document);
        var profileHash = ProfileHash(standard);
        var diagnostics = new List<Diagnostic>();
        if (cancellationToken.IsCancellationRequested)
        {
            diagnostics.Add(Cancelled("排版已取消。"));
            return Finish(documentHash, standard, template, profileHash, new List<LayoutPage>(), diagnostics, watch);
        }

        diagnostics.AddRange(Validate(document, standard, template));
        if (HasError(diagnostics))
            return Finish(documentHash, standard, template, profileHash, new List<LayoutPage>(), diagnostics, watch);

        var pages = new List<LayoutPage>();
        var flow = new Flow(pages);
        var composer = new LineComposer(new CachedMeasure(measure));
        var budget = StepBudget(document, standard);
        var steps = 0;
        foreach (var block in document.Blocks)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                diagnostics.Add(Cancelled("排版已取消。"));
                return Finish(documentHash, standard, template, profileHash, new List<LayoutPage>(), diagnostics, watch);
            }

            if (block == null)
            {
                diagnostics.Add(Problem(DiagnosticCodes.ESchemaInvalid, "文档含有空的块。", DiagnosticStage.Protocol, null));
                return Finish(documentHash, standard, template, profileHash, new List<LayoutPage>(), diagnostics, watch);
            }

            if (block.Type == BlockType.Spacer)
            {
                var slots = block.SlotCount ?? 1;
                if (slots <= 0)
                {
                    diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "空行槽数必须是正整数。", DiagnosticStage.Layout, block.SourceRef));
                    return Finish(documentHash, standard, template, profileHash, new List<LayoutPage>(), diagnostics, watch);
                }

                for (var slot = 0; slot < slots; slot++)
                {
                    if (!Advance(budget, ref steps, diagnostics, block))
                        return Finish(documentHash, standard, template, profileHash, new List<LayoutPage>(), diagnostics, watch);
                    if (!Place(flow, template, standard, null, 0, diagnostics, block))
                        return Finish(documentHash, standard, template, profileHash, new List<LayoutPage>(), diagnostics, watch);
                }

                continue;
            }

            if (!TryResolve(standard, block, out var style, out var definition, out var styleError))
            {
                diagnostics.Add(styleError!);
                return Finish(documentHash, standard, template, profileHash, new List<LayoutPage>(), diagnostics, watch);
            }

            if (!ScriptCalibration.IsComplete(standard) && HasScriptRun(block))
            {
                diagnostics.Add(Problem(
                    DiagnosticCodes.ETemplateInvalid,
                    "上下标缩放尚未标定，不能排版，也不会把上下标改成普通文字。请补齐院标的上下标标定或改用不含上下标的说明。",
                    DiagnosticStage.Standard,
                    block.SourceRef));
                return Finish(documentHash, standard, template, profileHash, new List<LayoutPage>(), diagnostics, watch);
            }

            var missingGlyph = FirstMissingGlyph(block, style);
            if (missingGlyph != null)
            {
                diagnostics.Add(Problem(
                    DiagnosticCodes.EFontMissing,
                    "字符 " + Describe(missingGlyph.Value) + " 在当前字体中没有字形。请改写该字符；不会用问号或方框代替。",
                    DiagnosticStage.Standard,
                    block.SourceRef));
                return Finish(documentHash, standard, template, profileHash, new List<LayoutPage>(), diagnostics, watch);
            }

            if (!LineComposer.TryCreate(block, out var cursor, out var createError))
            {
                diagnostics.Add(createError!);
                return Finish(documentHash, standard, template, profileHash, new List<LayoutPage>(), diagnostics, watch);
            }

            for (var before = 0; before < definition.BeforeSlots; before++)
            {
                if (!Advance(budget, ref steps, diagnostics, block))
                    return Finish(documentHash, standard, template, profileHash, new List<LayoutPage>(), diagnostics, watch);
                if (!Place(flow, template, standard, null, 0, diagnostics, block))
                    return Finish(documentHash, standard, template, profileHash, new List<LayoutPage>(), diagnostics, watch);
            }

            while (cursor.Index < cursor.Atoms.Count)
            {
                if (!Advance(budget, ref steps, diagnostics, block))
                    return Finish(documentHash, standard, template, profileHash, new List<LayoutPage>(), diagnostics, watch);
                if (!TryPeek(flow, template, out var destination, out var peekError))
                {
                    diagnostics.Add(peekError!);
                    return Finish(documentHash, standard, template, profileHash, new List<LayoutPage>(), diagnostics, watch);
                }

                var columnWidth = destination.Geometry.Right - destination.Geometry.Left;
                var indent = cursor.TextLineEmitted ? definition.HangingIndent : definition.Indent;
                var available = columnWidth - indent;
                var nextIsText = !cursor.Atoms[cursor.Index].HardBreak;
                if (nextIsText && !(available > 1e-9))
                {
                    diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "缩进不小于栏宽，没有可排的宽度。", DiagnosticStage.Layout, block.SourceRef));
                    return Finish(documentHash, standard, template, profileHash, new List<LayoutPage>(), diagnostics, watch);
                }

                var decision = composer.Next(cursor, available, indent, style, standard.MeasurementTolerance, cancellationToken);
                if (decision.Error != null)
                {
                    diagnostics.Add(decision.Error);
                    return Finish(documentHash, standard, template, profileHash, new List<LayoutPage>(), diagnostics, watch);
                }

                diagnostics.AddRange(decision.Warnings);
                if (decision.Done) break;
                if (decision.Spacer)
                {
                    if (!Place(flow, template, standard, null, 0, diagnostics, block))
                        return Finish(documentHash, standard, template, profileHash, new List<LayoutPage>(), diagnostics, watch);
                    continue;
                }

                if (decision.Line == null)
                {
                    diagnostics.Add(Problem(DiagnosticCodes.ENoLegalBreak, "排版没有产出视觉行。", DiagnosticStage.Layout, block.SourceRef));
                    return Finish(documentHash, standard, template, profileHash, new List<LayoutPage>(), diagnostics, watch);
                }

                if (!Place(flow, template, standard, decision.Line, columnWidth, diagnostics, block))
                    return Finish(documentHash, standard, template, profileHash, new List<LayoutPage>(), diagnostics, watch);
            }

            composer.FinishParagraph(cursor);
            for (var after = 0; after < definition.AfterSlots; after++)
            {
                if (!Advance(budget, ref steps, diagnostics, block))
                    return Finish(documentHash, standard, template, profileHash, new List<LayoutPage>(), diagnostics, watch);
                if (!Place(flow, template, standard, null, 0, diagnostics, block))
                    return Finish(documentHash, standard, template, profileHash, new List<LayoutPage>(), diagnostics, watch);
            }
        }

        watch.Stop();
        return Finish(documentHash, standard, template, profileHash, pages, diagnostics, watch);
    }

    private bool Place(Flow flow, LayoutTemplate template, InstitutionStandard standard, VisualLine? line, double columnWidth, List<Diagnostic> diagnostics, Block block)
    {
        if (!TryPeek(flow, template, out var destination, out var error))
        {
            diagnostics.Add(error!);
            return false;
        }

        var baseline = destination.Geometry.FirstBaselineY - destination.Row * standard.RowPitch;
        if (line != null && !LineInsideColumn(destination, baseline, line, columnWidth, standard, block, out error))
        {
            diagnostics.Add(error!);
            return false;
        }

        var page = EnsurePage(flow, template, destination.Page);
        var column = EnsureColumn(page, destination.Column);
        column.Rows.Add(new RowSlot
        {
            RowIndex = destination.Row,
            Baseline = baseline,
            Occupancy = line == null ? Occupancy.Spacer : Occupancy.Text,
            VisualLine = line
        });
        flow.Page = destination.Page;
        flow.Column = destination.Column;
        flow.Row = destination.Row + 1;
        return true;
    }

    private bool TryPeek(Flow flow, LayoutTemplate template, out Destination destination, out Diagnostic? error)
    {
        var page = flow.Page;
        var column = flow.Column;
        var row = flow.Row;
        var columns = template.Columns;
        if (row == columns[column].RowCount)
        {
            column++;
            row = 0;
        }

        if (column == columns.Count)
        {
            page++;
            column = 0;
            row = 0;
        }

        if (page >= _safetyPageLimit)
        {
            destination = default;
            error = new Diagnostic
            {
                Code = DiagnosticCodes.EResourceLimit,
                Severity = Severity.Error,
                Stage = DiagnosticStage.Layout,
                Message = "页数达到排版保护上限，已停止。正常写满一页不会触发这个上限。",
                Details = new Dictionary<string, string>
                {
                    ["safetyPageLimit"] = _safetyPageLimit.ToString(CultureInfo.InvariantCulture)
                }
            };
            return false;
        }

        destination = new Destination(page, column, row, columns[column]);
        error = null;
        return true;
    }

    private static LayoutPage EnsurePage(Flow flow, LayoutTemplate template, int pageIndex)
    {
        var step = template.PageStep!.Value;
        while (flow.Pages.Count <= pageIndex)
        {
            var index = flow.Pages.Count;
            flow.Pages.Add(new LayoutPage
            {
                PageIndex = index,
                PageOffset = new Point2 { X = step.X * index, Y = step.Y * index },
                Columns = new List<LayoutColumn>()
            });
        }

        return flow.Pages[pageIndex];
    }

    private static LayoutColumn EnsureColumn(LayoutPage page, int columnIndex)
    {
        while (page.Columns.Count <= columnIndex)
        {
            page.Columns.Add(new LayoutColumn
            {
                ColumnIndex = page.Columns.Count,
                Rows = new List<RowSlot>()
            });
        }

        return page.Columns[columnIndex];
    }

    private static bool LineInsideColumn(Destination destination, double baseline, VisualLine line, double columnWidth, InstitutionStandard standard, Block block, out Diagnostic? error)
    {
        var tolerance = standard.MeasurementTolerance;
        var ink = line.InkBounds;
        var geometry = destination.Geometry;
        if (line.MeasuredWidth > columnWidth - line.RenderRuns[0].RelativeOrigin.X + tolerance + 1e-9
            || ink.MaxX > columnWidth + tolerance + 1e-9
            || ink.MinX < -tolerance - 1e-9)
        {
            error = Problem(DiagnosticCodes.ENoLegalBreak, "整行测量后仍然超出栏宽。", DiagnosticStage.Layout, block.SourceRef);
            return false;
        }

        if (baseline + ink.MaxY > geometry.Top + tolerance + 1e-9
            || baseline + ink.MinY < geometry.Bottom - tolerance - 1e-9
            || ink.MaxY > standard.RowPitch + tolerance + 1e-9
            || -ink.MinY > standard.RowPitch + tolerance + 1e-9)
        {
            error = Problem(DiagnosticCodes.ETemplateInvalid, "字形超出栏高，或行距不足以避开相邻行。", DiagnosticStage.Layout, block.SourceRef);
            return false;
        }

        error = null;
        return true;
    }

    private static int StepBudget(Document document, InstitutionStandard standard)
    {
        var budget = 16;
        var styleSlots = 0;
        if (standard.Styles != null)
        {
            foreach (var style in standard.Styles.Values)
            {
                if (style == null) continue;
                styleSlots = Math.Max(styleSlots, Math.Max(0, style.BeforeSlots) + Math.Max(0, style.AfterSlots));
            }
        }

        foreach (var block in document.Blocks)
        {
            budget += 8 + styleSlots;
            if (block == null) continue;
            if (block.SlotCount.HasValue) budget += Math.Max(0, block.SlotCount.Value);
            if (block.Runs == null) continue;
            foreach (var run in block.Runs)
                budget += run == null ? 1 : run.Text.Length + 1;
        }

        return budget * 2;
    }

    private static bool Advance(int budget, ref int steps, List<Diagnostic> diagnostics, Block block)
    {
        steps++;
        if (steps <= budget) return true;
        diagnostics.Add(Problem(DiagnosticCodes.ENoLegalBreak, "排版循环没有消费内容。", DiagnosticStage.Layout, block.SourceRef));
        return false;
    }

    private static List<Diagnostic> Validate(Document document, InstitutionStandard standard, LayoutTemplate template)
    {
        var diagnostics = new List<Diagnostic>();
        if (!IsSchemaV1(document.SchemaVersion))
            diagnostics.Add(Problem(DiagnosticCodes.ESchemaVersion, "文档 schemaVersion 不是受支持的 1.x。", DiagnosticStage.Protocol, null));
        if (document.Blocks == null)
            diagnostics.Add(Problem(DiagnosticCodes.ESchemaInvalid, "文档缺少块列表。", DiagnosticStage.Protocol, null));
        if (string.IsNullOrWhiteSpace(standard.StandardId) || string.IsNullOrWhiteSpace(standard.Version))
            diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "院标 id 和 version 不能为空。", DiagnosticStage.Standard, null));
        if (!Positive(standard.TextHeight))
            diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "字高必须是正数。", DiagnosticStage.Standard, null));
        if (!Positive(standard.WidthFactor))
            diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "宽度系数必须是正数。", DiagnosticStage.Standard, null));
        if (!Finite(standard.ObliqueAngle))
            diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "倾斜角必须是有限数值。", DiagnosticStage.Standard, null));
        if (!Positive(standard.RowPitch))
            diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "行距必须是正数。", DiagnosticStage.Standard, null));
        if (!Finite(standard.MeasurementTolerance) || standard.MeasurementTolerance < 0)
            diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "测量公差必须是不小于 0 的有限数值。", DiagnosticStage.Standard, null));
        if (LineBreakRules.IsPending(standard.LineBreakRuleVersion))
            diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "换行规则版本尚未标定，不能排版。", DiagnosticStage.Standard, null));
        else if (!LineBreakRules.IsImplemented(standard.LineBreakRuleVersion))
            diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "排版引擎不认识换行规则版本 " + standard.LineBreakRuleVersion + "。", DiagnosticStage.Standard, null));
        if (LineBreakRules.IsPending(standard.SymbolMapVersion))
            diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "符号映射版本尚未标定，不能排版。", DiagnosticStage.Standard, null));

        RequireStyle(diagnostics, standard, "heading1");
        RequireStyle(diagnostics, standard, "heading2");
        RequireStyle(diagnostics, standard, "body");

        if (string.IsNullOrWhiteSpace(template.TemplateId) || string.IsNullOrWhiteSpace(template.Version))
            diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "模板 id 和 version 不能为空。", DiagnosticStage.Standard, null));
        if (template.Status != TemplateStatus.Calibrated)
            diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "模板尚未标定，不能排版。", DiagnosticStage.Standard, null));
        if (template.StandardRef == null || template.StandardRef.Id != standard.StandardId || template.StandardRef.Version != standard.Version)
            diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "模板引用的院标与本次院标不一致。", DiagnosticStage.Standard, null));
        if (template.PageStep == null || !Finite(template.PageStep.Value.X) || !Finite(template.PageStep.Value.Y))
            diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "pageStep 不能为空，也不能是非法数值。", DiagnosticStage.Standard, null));
        if (template.Columns == null || template.Columns.Count == 0)
        {
            diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "模板没有栏。", DiagnosticStage.Standard, null));
            return diagnostics;
        }

        for (var index = 0; index < template.Columns.Count; index++)
        {
            var column = template.Columns[index];
            if (column == null || !(column.Right > column.Left) || !(column.Top > column.Bottom) || column.RowCount <= 0 || !Finite(column.FirstBaselineY))
            {
                diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "栏 " + index + " 的边界或行数无效。", DiagnosticStage.Standard, null));
                continue;
            }

            if (!Positive(standard.RowPitch)) continue;
            var last = column.FirstBaselineY - (column.RowCount - 1) * standard.RowPitch;
            if (column.FirstBaselineY > column.Top + 1e-9 || last < column.Bottom - 1e-9)
                diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "栏 " + index + " 的首行或末行基线超出栏高。", DiagnosticStage.Standard, null));
        }

        return diagnostics;
    }

    private static void RequireStyle(List<Diagnostic> diagnostics, InstitutionStandard standard, string role)
    {
        if (standard.Styles == null || !standard.Styles.TryGetValue(role, out var style) || style == null)
        {
            diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, "缺少样式 " + role + "。", DiagnosticStage.Standard, null));
            return;
        }

        if (!Finite(style.Indent) || style.Indent < 0 || !Finite(style.HangingIndent) || style.HangingIndent < 0)
            diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, role + " 的缩进必须是不小于 0 的有限数值。", DiagnosticStage.Standard, null));
        if (style.BeforeSlots < 0 || style.AfterSlots < 0)
            diagnostics.Add(Problem(DiagnosticCodes.ETemplateInvalid, role + " 的前后槽不能为负。", DiagnosticStage.Standard, null));
        if (string.IsNullOrWhiteSpace(style.FontRef) || !TryFont(standard, style.FontRef, out _))
            diagnostics.Add(Problem(DiagnosticCodes.EFontMissing, role + " 没有可用的字体引用。", DiagnosticStage.Standard, null));
    }

    private static bool TryResolve(InstitutionStandard standard, Block block, out ResolvedStyle style, out StyleDefinition definition, out Diagnostic? error)
    {
        style = new ResolvedStyle();
        definition = new StyleDefinition();
        error = null;
        var role = block.Type == BlockType.Heading1 ? "heading1" : block.Type == BlockType.Heading2 ? "heading2" : block.Type == BlockType.Paragraph ? "body" : null;
        if (role == null)
        {
            error = Problem(DiagnosticCodes.ESchemaInvalid, "不支持的块类型。", DiagnosticStage.Protocol, block.SourceRef);
            return false;
        }

        if (standard.Styles == null || !standard.Styles.TryGetValue(role, out var found) || found == null || !TryFont(standard, found.FontRef, out var font))
        {
            error = Problem(DiagnosticCodes.ETemplateInvalid, "缺少样式 " + role + "。", DiagnosticStage.Standard, block.SourceRef);
            return false;
        }

        definition = found;
        style = new ResolvedStyle
        {
            StyleId = role,
            Semantic = RunSemantic.Normal,
            Font = new FontEntry { Family = font.Family, BigFont = font.BigFont, FileIdentity = font.FileIdentity },
            TextHeight = standard.TextHeight,
            WidthFactor = standard.WidthFactor,
            ObliqueAngle = standard.ObliqueAngle
        };
        return true;
    }

    private static bool HasScriptRun(Block block)
    {
        if (block?.Runs == null) return false;
        foreach (var run in block.Runs)
        {
            if (run != null && run.Semantic != RunSemantic.Normal) return true;
        }

        return false;
    }

    private static char? FirstMissingGlyph(Block block, ResolvedStyle style)
    {
        if (block?.Runs == null) return null;
        var font = style.Font;
        foreach (var run in block.Runs)
        {
            if (run?.Text == null) continue;
            var missing = FontGlyphCoverage.FirstMissingGlyph(font?.FileIdentity, font?.BigFont, run.Text);
            if (missing != null) return missing;
        }

        return null;
    }

    private static string Describe(char character)
    {
        var code = ((int)character).ToString("X4", CultureInfo.InvariantCulture);
        var printable = character >= 0x20 && character <= 0x7E ? character.ToString() : "U+" + code;
        return "「" + printable + "」（U+" + code + "）";
    }

    private static bool TryFont(InstitutionStandard standard, string? family, out FontEntry font)
    {
        font = new FontEntry();
        if (string.IsNullOrWhiteSpace(family) || standard.FontProfile?.Fonts == null) return false;
        foreach (var candidate in standard.FontProfile.Fonts)
        {
            if (candidate != null && candidate.Family == family)
            {
                font = candidate;
                return true;
            }
        }

        return false;
    }

    private static LayoutResult Finish(string documentHash, InstitutionStandard standard, LayoutTemplate template, string profileHash, List<LayoutPage> pages, List<Diagnostic> diagnostics, Stopwatch watch)
    {
        if (watch.IsRunning) watch.Stop();
        return new LayoutResult
        {
            SchemaVersion = LayoutEngineInfo.SchemaVersion,
            DocumentHash = documentHash,
            StandardRef = new StandardRef { Id = standard.StandardId ?? string.Empty, Version = standard.Version ?? string.Empty },
            TemplateRef = new TemplateRef { Id = template.TemplateId ?? string.Empty, Version = template.Version ?? string.Empty },
            EngineVersion = LayoutEngineInfo.EngineVersion,
            MeasurementProfileHash = profileHash,
            Pages = pages,
            Diagnostics = diagnostics,
            Statistics = Count(pages, diagnostics, watch.Elapsed)
        };
    }

    private static LayoutStatistics Count(List<LayoutPage> pages, List<Diagnostic> diagnostics, TimeSpan elapsed)
    {
        var rows = 0;
        var objects = 0;
        var warnings = 0;
        foreach (var page in pages)
        {
            if (page?.Columns == null) continue;
            foreach (var column in page.Columns)
            {
                if (column?.Rows == null) continue;
                rows += column.Rows.Count;
                foreach (var row in column.Rows)
                {
                    if (row?.Occupancy == Occupancy.Text && row.VisualLine?.RenderRuns != null)
                        objects += row.VisualLine.RenderRuns.Count;
                }
            }
        }

        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic != null && diagnostic.Severity == Severity.Warning) warnings++;
        }

        return new LayoutStatistics
        {
            PageCount = pages.Count,
            RowCount = rows,
            ObjectCount = objects,
            WarningCount = warnings,
            Elapsed = elapsed
        };
    }

    private static string DocumentHash(Document document)
    {
        var contentHash = document.Source == null ? null : document.Source.ContentHash;
        if (contentHash != null && contentHash.Trim().Length > 0) return contentHash;
        var builder = new StringBuilder();
        if (document.Blocks != null)
        {
            foreach (var block in document.Blocks)
            {
                if (block == null)
                {
                    builder.Append('\n');
                    continue;
                }

                builder.Append((int)block.Type).Append('|').Append(block.Numbering?.Label ?? string.Empty).Append('|');
                builder.Append(block.SlotCount.HasValue ? block.SlotCount.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
                if (block.Runs != null)
                {
                    foreach (var run in block.Runs)
                        builder.Append('|').Append(run == null ? string.Empty : run.Semantic.ToString()).Append(':').Append(run == null ? string.Empty : run.Text);
                }

                builder.Append('\n');
            }
        }

        return Sha256(builder.ToString());
    }

    private static string ProfileHash(InstitutionStandard standard)
    {
        var builder = new StringBuilder();
        builder.Append(standard.StandardId).Append('|').Append(standard.Version).Append('|');
        builder.Append(standard.TextHeight.ToString("R", CultureInfo.InvariantCulture)).Append('|');
        builder.Append(standard.WidthFactor.ToString("R", CultureInfo.InvariantCulture)).Append('|');
        builder.Append(standard.ObliqueAngle.ToString("R", CultureInfo.InvariantCulture)).Append('|');
        builder.Append(standard.RowPitch.ToString("R", CultureInfo.InvariantCulture)).Append('|');
        builder.Append(standard.LineBreakRuleVersion).Append('|').Append(standard.SymbolMapVersion).Append('|');
        builder.Append(standard.MeasurementTolerance.ToString("R", CultureInfo.InvariantCulture)).Append('|');
        builder.Append((standard.SuperscriptScale ?? -1).ToString("R", CultureInfo.InvariantCulture)).Append('|');
        builder.Append((standard.SuperscriptRise ?? -1).ToString("R", CultureInfo.InvariantCulture)).Append('|');
        builder.Append((standard.SubscriptScale ?? -1).ToString("R", CultureInfo.InvariantCulture)).Append('|');
        builder.Append((standard.SubscriptDrop ?? -1).ToString("R", CultureInfo.InvariantCulture));
        if (standard.FontProfile?.Fonts != null)
        {
            foreach (var font in standard.FontProfile.Fonts)
            {
                if (font == null) continue;
                builder.Append('|').Append(font.Family).Append('|').Append(font.FileIdentity).Append('|').Append(font.BigFont);
            }
        }

        return Sha256(builder.ToString());
    }

    private static string Sha256(string text)
    {
        using (var sha = SHA256.Create())
        {
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var value in bytes)
                builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }

    private static bool IsSchemaV1(string version)
    {
        if (string.IsNullOrEmpty(version) || version.Length < 3 || version[0] != '1' || version[1] != '.') return false;
        for (var index = 2; index < version.Length; index++)
        {
            if (version[index] < '0' || version[index] > '9') return false;
        }

        return true;
    }

    private static bool HasError(List<Diagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic != null && diagnostic.Severity == Severity.Error) return true;
        }

        return false;
    }

    private static bool Positive(double value) => Finite(value) && value > 0;

    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static Diagnostic Cancelled(string message)
    {
        return new Diagnostic
        {
            Code = DiagnosticCodes.Cancelled,
            Severity = Severity.Error,
            Stage = DiagnosticStage.Layout,
            Message = message
        };
    }

    private static Diagnostic Problem(string code, string message, DiagnosticStage stage, SourceRef? source)
    {
        return new Diagnostic
        {
            Code = code,
            Severity = Severity.Error,
            Stage = stage,
            Message = message,
            SourceRef = source
        };
    }

    private sealed class Flow
    {
        public Flow(List<LayoutPage> pages)
        {
            Pages = pages;
        }

        public List<LayoutPage> Pages { get; }

        public int Page { get; set; }

        public int Column { get; set; }

        public int Row { get; set; }
    }

    private readonly struct Destination
    {
        public Destination(int page, int column, int row, ColumnGeometry geometry)
        {
            Page = page;
            Column = column;
            Row = row;
            Geometry = geometry;
        }

        public int Page { get; }

        public int Column { get; }

        public int Row { get; }

        public ColumnGeometry Geometry { get; }
    }
}
