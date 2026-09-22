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

        var layout = _engine.Layout(parsed.Document, standard, template, _measure, cancellationToken);
        result.Layout = layout;
        var placed = NotePlacement.Create(layout, template, standard, transform, cancellationToken);
        Add(result.Diagnostics, placed.Diagnostics);
        if (!placed.Success || HasError(result.Diagnostics)) return result;
        result.Texts = placed.Texts;
        result.Success = true;
        return result;
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
