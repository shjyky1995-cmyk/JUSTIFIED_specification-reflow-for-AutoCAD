using System;
using System.Collections.Generic;
using System.Threading;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Layout;
using Justified.SpecificationReflow.AutoCAD.Contracts.Ports;
using Justified.SpecificationReflow.AutoCAD.Contracts.Rendering;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.Contracts.Templates;

namespace Justified.SpecificationReflow.AutoCAD.Application;

public sealed class NoteGenerationResult
{
    public bool Success { get; set; }

    public Document? Document { get; set; }

    public LayoutResult? Layout { get; set; }

    public List<PlacedText> Texts { get; set; } = new List<PlacedText>();

    public List<Diagnostic> Diagnostics { get; set; } = new List<Diagnostic>();
}

// 解析、换行、换成世界坐标。写图由宿主完成；这里不删除旧文字，也没有实体编号。
public sealed class NoteGenerationService
{
    private readonly IDocumentParser _parser;
    private readonly ILayoutEngine _engine;
    private readonly ITextMeasureService _measure;

    public NoteGenerationService(IDocumentParser parser, ILayoutEngine engine, ITextMeasureService measure)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _measure = measure ?? throw new ArgumentNullException(nameof(measure));
    }

    public NoteGenerationResult Generate(IDocumentSource source, InstitutionStandard standard, LayoutTemplate template, RenderTransform transform, CancellationToken cancellationToken)
    {
        return Generate(source, standard, template, transform, cancellationToken, null);
    }

    public NoteGenerationResult Generate(IDocumentSource source, InstitutionStandard standard, LayoutTemplate template, RenderTransform transform, CancellationToken cancellationToken, GenerationLimits? limits)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        var result = new NoteGenerationResult();
        if (cancellationToken.IsCancellationRequested)
        {
            result.Diagnostics.Add(Problem(DiagnosticCodes.Cancelled, "生成已取消。"));
            return result;
        }

        var parsed = _parser.Parse(source, new ParseProfile
        {
            Standard = new StandardRef { Id = standard.StandardId, Version = standard.Version }
        }, cancellationToken);
        Add(result.Diagnostics, parsed.Diagnostics);
        if (!parsed.Success || parsed.Document == null) return result;
        result.Document = parsed.Document;
        if (ExceedsDocument(parsed.Document, limits, out var documentMessage))
        {
            result.Diagnostics.Add(Problem(DiagnosticCodes.EResourceLimit, documentMessage));
            return result;
        }

        var layout = _engine.Layout(parsed.Document, standard, template, _measure, cancellationToken);
        result.Layout = layout;
        var placed = NotePlacement.Create(layout, template, standard, transform, cancellationToken);
        Add(result.Diagnostics, placed.Diagnostics);
        if (!placed.Success || HasError(result.Diagnostics)) return result;
        if (ExceedsLayout(layout, placed, limits, out var layoutMessage))
        {
            result.Texts.Clear();
            result.Diagnostics.Add(Problem(DiagnosticCodes.EResourceLimit, layoutMessage));
            return result;
        }

        result.Texts = placed.Texts;
        result.Success = true;
        return result;
    }

    private static bool ExceedsDocument(Document document, GenerationLimits? limits, out string message)
    {
        message = string.Empty;
        if (limits == null) return false;
        if (document.Blocks != null && document.Blocks.Count > limits.MaxBlocks)
        {
            message = "段落块数 " + document.Blocks.Count + " 超过单次上限 " + limits.MaxBlocks + "。请拆小说明或提高限额。";
            return true;
        }

        var characters = 0;
        if (document.Blocks != null)
        {
            foreach (var block in document.Blocks)
            {
                if (block?.Runs == null) continue;
                foreach (var run in block.Runs)
                {
                    if (run?.Text == null) continue;
                    characters += run.Text.Length;
                }
            }
        }

        if (characters > limits.MaxCharacters)
        {
            message = "字符数 " + characters + " 超过单次上限 " + limits.MaxCharacters + "。请拆小说明或提高限额。";
            return true;
        }

        return false;
    }

    private static bool ExceedsLayout(LayoutResult layout, NotePlacementResult placed, GenerationLimits? limits, out string message)
    {
        message = string.Empty;
        if (limits == null) return false;
        if (layout.Pages != null && layout.Pages.Count > limits.MaxPages)
        {
            message = "页数 " + layout.Pages.Count + " 超过单次上限 " + limits.MaxPages + "。请拆小说明或提高限额。";
            return true;
        }

        if (placed.Texts.Count > limits.MaxTexts)
        {
            message = "单行文字数 " + placed.Texts.Count + " 超过单次上限 " + limits.MaxTexts + "。请拆小说明或提高限额。";
            return true;
        }

        return false;
    }

    private static void Add(List<Diagnostic> target, IReadOnlyList<Diagnostic>? source)
    {
        if (source == null) return;
        foreach (var diagnostic in source)
        {
            if (diagnostic != null) target.Add(diagnostic);
        }
    }

    private static bool HasError(List<Diagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic != null && diagnostic.Severity == Severity.Error) return true;
        }

        return false;
    }

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
