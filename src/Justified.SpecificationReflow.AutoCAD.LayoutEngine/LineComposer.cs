using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Geometry;
using Justified.SpecificationReflow.AutoCAD.Contracts.Layout;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;

namespace Justified.SpecificationReflow.AutoCAD.LayoutEngine;

// 每次只为当前栏宽产出一行。调用方在换栏后用新宽度继续，不能先按首栏切完全文。
internal sealed class LineComposer
{
    internal sealed class Atom
    {
        public string Text { get; set; } = string.Empty;

        public bool HardBreak { get; set; }

        public bool CollapsibleSpace { get; set; }

        public bool StickyWithNext { get; set; }

        public bool Splittable { get; set; }

        public bool Numbering { get; set; }

        public string? Origin { get; set; }

        public int OriginIndex { get; set; }

        public RunSemantic Semantic { get; set; }

        public int ParagraphIndex { get; set; }

        public int? RunIndex { get; set; }

        public int TextStart { get; set; }

        public int TextLength { get; set; }
    }

    internal sealed class Cursor
    {
        public Block Block { get; set; } = new Block();

        public List<Atom> Atoms { get; set; } = new List<Atom>();

        public int Index { get; set; }

        public bool TextLineEmitted { get; set; }

        public List<Atom> DeferredBreaks { get; set; } = new List<Atom>();

        public VisualLine? LastLine { get; set; }
    }

    internal sealed class Decision
    {
        public bool Done { get; set; }

        public bool Spacer { get; set; }

        public VisualLine? Line { get; set; }

        public Diagnostic? Error { get; set; }

        public List<Diagnostic> Warnings { get; } = new List<Diagnostic>();
    }

    private readonly CachedMeasure _measure;

    public LineComposer(CachedMeasure measure)
    {
        _measure = measure ?? throw new ArgumentNullException(nameof(measure));
    }

    public static bool TryCreate(Block block, out Cursor cursor, out Diagnostic? error)
    {
        cursor = new Cursor { Block = block };
        error = null;
        var paragraph = block.SourceRef.ParagraphIndex;
        var runs = block.Runs ?? new List<TextRun>();
        var hasBody = false;
        for (var index = 0; index < runs.Count; index++)
        {
            var run = runs[index];
            if (run == null)
            {
                error = Problem(DiagnosticCodes.ESchemaInvalid, "文本 run 为空。", Source(paragraph, index, null));
                return false;
            }

            if (run.Text == "\n")
            {
                cursor.Atoms.Add(new Atom
                {
                    Text = "\n",
                    HardBreak = true,
                    ParagraphIndex = paragraph,
                    RunIndex = index,
                    TextStart = 0,
                    TextLength = 1
                });
                continue;
            }

            if (run.Text.IndexOf('\n') >= 0)
            {
                error = Problem(DiagnosticCodes.ENoLegalBreak, "硬换行必须是单独的 run，不能嵌在文字中间。", Source(paragraph, index, null));
                return false;
            }

            if (!TextTokenizer.TryTokenize(run.Text, out var tokens, out var tokenError))
            {
                error = Problem(DiagnosticCodes.ENoLegalBreak, tokenError ?? "无法切分文本。", Source(paragraph, index, null));
                return false;
            }

            foreach (var token in tokens)
            {
                hasBody = true;
                cursor.Atoms.Add(new Atom
                {
                    Text = token.Text,
                    CollapsibleSpace = token.CollapsibleSpace,
                    Splittable = token.Splittable,
                    ParagraphIndex = paragraph,
                    RunIndex = index,
                    TextStart = token.Start,
                    TextLength = token.Text.Length,
                    Semantic = run.Semantic
                });
            }
        }

        if (block.Numbering != null && !string.IsNullOrEmpty(block.Numbering.Label))
        {
            cursor.Atoms.Insert(0, new Atom
            {
                Text = hasBody ? block.Numbering.Label + "\u0020\u0020" : block.Numbering.Label,
                Numbering = true,
                StickyWithNext = hasBody,
                Splittable = false,
                ParagraphIndex = paragraph
            });
        }

        return true;
    }

    public Decision Next(Cursor cursor, double available, double indent, ResolvedStyle style, double tolerance, System.Threading.CancellationToken cancellationToken)
    {
        var splitBudget = 2;
        foreach (var atom in cursor.Atoms)
            splitBudget += Math.Max(1, atom.Text.Length);

        var splits = 0;
        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
                return new Decision { Error = Cancelled() };
            if (cursor.Index >= cursor.Atoms.Count)
                return new Decision { Done = true };
            if (cursor.Atoms[cursor.Index].HardBreak)
            {
                cursor.DeferredBreaks.Add(cursor.Atoms[cursor.Index]);
                cursor.Index++;
                return new Decision { Spacer = true };
            }

            var start = cursor.Index;
            var segmentEnd = start;
            while (segmentEnd < cursor.Atoms.Count && !cursor.Atoms[segmentEnd].HardBreak)
                segmentEnd++;

            var best = -1;
            for (var end = start + 1; end <= segmentEnd; end++)
            {
                var fit = Fit(cursor.Atoms, start, end, style, available, tolerance, cancellationToken);
                if (fit.Error != null) return new Decision { Error = fit.Error };
                if (!fit.Fits) break;
                if (IsLegal(cursor.Atoms, start, end)) best = end;
            }

            if (best >= 0)
            {
                var takeBreak = segmentEnd < cursor.Atoms.Count && best == segmentEnd;
                var built = BuildLine(cursor, start, best, takeBreak ? cursor.Atoms[segmentEnd] : null, indent, style, cancellationToken);
                if (built.Error != null) return built;
                var nextIndex = best + (takeBreak ? 1 : 0);
                if (nextIndex <= start) return new Decision { Error = NoProgress(cursor.Block) };
                cursor.DeferredBreaks.Clear();
                cursor.Index = nextIndex;
                cursor.TextLineEmitted = true;
                cursor.LastLine = built.Line;
                return built;
            }

            if (splits++ > splitBudget)
                return new Decision { Error = NoLegal(cursor, start, available) };
            var split = TrySplit(cursor.Atoms, start, segmentEnd, style, available, tolerance, cancellationToken);
            if (split.Error != null) return new Decision { Error = split.Error };
            if (!split.Split) return new Decision { Error = NoLegal(cursor, start, available) };
        }
    }

    public void FinishParagraph(Cursor cursor)
    {
        if (cursor.LastLine == null || cursor.DeferredBreaks.Count == 0)
        {
            cursor.DeferredBreaks.Clear();
            return;
        }

        foreach (var atom in cursor.DeferredBreaks)
            AddSource(cursor.LastLine.SourceSlices, atom);
        cursor.DeferredBreaks.Clear();
    }

    private FitResult Fit(List<Atom> atoms, int start, int end, ResolvedStyle style, double available, double tolerance, System.Threading.CancellationToken cancellationToken)
    {
        var display = DisplayString(atoms, start, end);
        if (display.Length == 0) return FitResult.EmptyFits();
        var measured = _measure.Measure(display, style, cancellationToken);
        if (measured.Error != null) return FitResult.Failed(measured.Error);
        var widthOk = measured.Advance <= available + tolerance + 1e-9
            && measured.Ink.MaxX <= available + tolerance + 1e-9
            && measured.Ink.MinX >= -tolerance - 1e-9;
        return FitResult.Width(widthOk);
    }

    private Decision BuildLine(Cursor cursor, int start, int best, Atom? terminatingBreak, double indent, ResolvedStyle style, System.Threading.CancellationToken cancellationToken)
    {
        var display = DisplayString(cursor.Atoms, start, best);
        if (display.Length == 0)
            return new Decision { Error = NoLegal(cursor, start, 0) };

        var measured = _measure.Measure(display, style, cancellationToken);
        if (measured.Error != null) return new Decision { Error = measured.Error };

        var slices = new List<SourceRef>();
        foreach (var deferred in cursor.DeferredBreaks)
            AddSource(slices, deferred);
        for (var index = start; index < best; index++)
            AddSource(slices, cursor.Atoms[index]);
        if (terminatingBreak != null)
            AddSource(slices, terminatingBreak);

        // 普通、上标、下标各成一个 DBText 段。段宽按各自缩放后的字高实测，
        // 整行宽度仍按正文字高测量（保守），行高检查用含上下标伸出的并集。
        var renderRuns = new List<RenderRun>();
        var ink = new Bounds2
        {
            MinX = indent + measured.Ink.MinX,
            MinY = measured.Ink.MinY,
            MaxX = indent + measured.Ink.MaxX,
            MaxY = measured.Ink.MaxY
        };
        var originX = indent;
        var index2 = start;
        while (index2 < best)
        {
            if (cursor.Atoms[index2].HardBreak)
            {
                index2++;
                continue;
            }

            var semantic = cursor.Atoms[index2].Semantic;
            var segmentStart = index2;
            while (index2 < best && !cursor.Atoms[index2].HardBreak && cursor.Atoms[index2].Semantic == semantic)
                index2++;
            var segment = DisplayString(cursor.Atoms, segmentStart, index2);
            if (segment.Length == 0) continue;

            var resolved = ScriptCalibration.Apply(style, semantic);
            var segmentMeasured = _measure.Measure(segment, resolved.Style, cancellationToken);
            if (segmentMeasured.Error != null) return new Decision { Error = segmentMeasured.Error };
            renderRuns.Add(new RenderRun
            {
                Text = segment,
                RelativeOrigin = new Point2 { X = originX, Y = 0 },
                BaselineOffset = resolved.BaselineOffset,
                ResolvedStyle = resolved.Style,
                MeasuredAdvance = segmentMeasured.Advance,
                InkBounds = new Bounds2
                {
                    MinX = originX + segmentMeasured.Ink.MinX,
                    MinY = resolved.BaselineOffset + segmentMeasured.Ink.MinY,
                    MaxX = originX + segmentMeasured.Ink.MaxX,
                    MaxY = resolved.BaselineOffset + segmentMeasured.Ink.MaxY
                }
            });
            ink = Union(ink, new Bounds2
            {
                MinX = originX + segmentMeasured.Ink.MinX,
                MinY = resolved.BaselineOffset + segmentMeasured.Ink.MinY,
                MaxX = originX + segmentMeasured.Ink.MaxX,
                MaxY = resolved.BaselineOffset + segmentMeasured.Ink.MaxY
            });
            originX += segmentMeasured.Advance;
        }

        var decision = new Decision
        {
            Line = new VisualLine
            {
                Text = display,
                MeasuredWidth = measured.Advance,
                InkBounds = ink,
                SourceSlices = slices,
                RenderRuns = renderRuns
            }
        };
        decision.Warnings.AddRange(measured.Warnings);
        AddSplitWarnings(decision, cursor.Atoms, start, best);
        return decision;
    }

    private static Bounds2 Union(Bounds2 left, Bounds2 right)
    {
        return new Bounds2
        {
            MinX = Math.Min(left.MinX, right.MinX),
            MinY = Math.Min(left.MinY, right.MinY),
            MaxX = Math.Max(left.MaxX, right.MaxX),
            MaxY = Math.Max(left.MaxY, right.MaxY)
        };
    }

    private static void AddSplitWarnings(Decision decision, List<Atom> atoms, int start, int best)
    {
        for (var index = start; index < best; index++)
        {
            var atom = atoms[index];
            if (atom.Origin == null) continue;
            if (index + 1 < best && ReferenceEquals(atoms[index + 1].Origin, atom.Origin)) continue;
            var cut = atom.OriginIndex + atom.Text.Length;
            if (cut >= atom.Origin.Length) continue;
            decision.Warnings.Add(new Diagnostic
            {
                Code = DiagnosticCodes.WTokenSplit,
                Severity = Severity.Warning,
                Stage = DiagnosticStage.Layout,
                Message = "超长单元按字符边界断开。",
                SourceRef = Source(atom.ParagraphIndex, atom.RunIndex, new TextRange { Start = atom.TextStart, Length = atom.TextLength }),
                Details = new Dictionary<string, string>
                {
                    ["token"] = atom.Origin,
                    ["breakIndex"] = cut.ToString(CultureInfo.InvariantCulture)
                }
            });
        }
    }

    private SplitResult TrySplit(List<Atom> atoms, int start, int segmentEnd, ResolvedStyle style, double available, double tolerance, System.Threading.CancellationToken cancellationToken)
    {
        for (var index = start; index < segmentEnd; index++)
        {
            var atom = atoms[index];
            if (atom.HardBreak) continue;
            var measured = _measure.Measure(atom.Text, style, cancellationToken);
            if (measured.Error != null) return SplitResult.Failed(measured.Error);
            var alone = Math.Max(measured.Advance, measured.Ink.MaxX);
            if (alone <= available + tolerance + 1e-9 && measured.Ink.MinX >= -tolerance - 1e-9) continue;
            if (!atom.Splittable) return SplitResult.None();
            var parts = Explode(atom);
            if (parts.Count <= 1) return SplitResult.None();
            atoms.RemoveAt(index);
            atoms.InsertRange(index, parts);
            return SplitResult.DidSplit();
        }

        return SplitResult.None();
    }

    private static List<Atom> Explode(Atom atom)
    {
        var parts = new List<Atom>();
        var origin = atom.Origin ?? atom.Text;
        var originBase = atom.Origin == null ? 0 : atom.OriginIndex;
        var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(atom.Text);
        while (enumerator.MoveNext())
        {
            var text = enumerator.GetTextElement();
            var local = enumerator.ElementIndex;
            parts.Add(new Atom
            {
                Text = text,
                Splittable = false,
                CollapsibleSpace = atom.CollapsibleSpace,
                Origin = origin,
                OriginIndex = originBase + local,
                ParagraphIndex = atom.ParagraphIndex,
                RunIndex = atom.RunIndex,
                TextStart = atom.TextStart + local,
                TextLength = text.Length,
                Semantic = atom.Semantic
            });
        }

        return parts;
    }

    private static bool IsLegal(List<Atom> atoms, int start, int end)
    {
        var displayEnd = DisplayEnd(atoms, start, end);
        if (displayEnd <= start) return false;
        var last = atoms[displayEnd - 1];
        if (last.HardBreak) return false;
        if (LineBreakRules.IsLineEndForbidden(last.Text) && MoreTextAfter(atoms, end)) return false;
        var next = FirstDisplayedAfter(atoms, end);
        if (next != null && LineBreakRules.IsLineStartForbidden(next.Text)) return false;
        for (var index = start; index < end; index++)
        {
            if (!atoms[index].StickyWithNext) continue;
            var follower = index + 1;
            if (follower < atoms.Count && atoms[follower].HardBreak) continue;
            if (follower >= end) return false;
        }

        return true;
    }

    private static int DisplayEnd(List<Atom> atoms, int start, int end)
    {
        var last = end;
        if (!MoreTextAfter(atoms, end)) return last;
        var trimmed = last;
        while (trimmed > start && atoms[trimmed - 1].CollapsibleSpace)
            trimmed--;
        return trimmed > start ? trimmed : last;
    }

    private static string DisplayString(List<Atom> atoms, int start, int end)
    {
        var displayEnd = DisplayEnd(atoms, start, end);
        var builder = new StringBuilder();
        for (var index = start; index < displayEnd; index++)
        {
            if (!atoms[index].HardBreak) builder.Append(atoms[index].Text);
        }

        return builder.ToString();
    }

    private static bool MoreTextAfter(List<Atom> atoms, int end)
    {
        for (var index = end; index < atoms.Count; index++)
        {
            if (atoms[index].HardBreak) return false;
            if (!atoms[index].CollapsibleSpace) return true;
        }

        return false;
    }

    private static Atom? FirstDisplayedAfter(List<Atom> atoms, int end)
    {
        for (var index = end; index < atoms.Count; index++)
        {
            if (atoms[index].HardBreak) return null;
            if (atoms[index].CollapsibleSpace) continue;
            return atoms[index];
        }

        return null;
    }

    private static void AddSource(List<SourceRef> slices, Atom atom)
    {
        if (atom.Numbering || atom.RunIndex == null)
        {
            if (slices.Count == 0 || slices[slices.Count - 1].RunIndex != null || slices[slices.Count - 1].ParagraphIndex != atom.ParagraphIndex)
                slices.Add(new SourceRef { ParagraphIndex = atom.ParagraphIndex });
            return;
        }

        if (slices.Count > 0)
        {
            var last = slices[slices.Count - 1];
            if (last.RunIndex == atom.RunIndex
                && last.ParagraphIndex == atom.ParagraphIndex
                && last.TextRange != null
                && last.TextRange.Start + last.TextRange.Length == atom.TextStart)
            {
                last.TextRange.Length += atom.TextLength;
                return;
            }
        }

        slices.Add(new SourceRef
        {
            ParagraphIndex = atom.ParagraphIndex,
            RunIndex = atom.RunIndex,
            TextRange = new TextRange { Start = atom.TextStart, Length = atom.TextLength }
        });
    }

    private static Diagnostic NoLegal(Cursor cursor, int start, double available)
    {
        var excerpt = new StringBuilder();
        for (var index = start; index < cursor.Atoms.Count && excerpt.Length < 24; index++)
        {
            if (!cursor.Atoms[index].HardBreak) excerpt.Append(cursor.Atoms[index].Text);
        }

        return new Diagnostic
        {
            Code = DiagnosticCodes.ENoLegalBreak,
            Severity = Severity.Error,
            Stage = DiagnosticStage.Layout,
            Message = "无法形成不越界的合法行。",
            SourceRef = cursor.Block.SourceRef,
            Details = new Dictionary<string, string>
            {
                ["paragraphIndex"] = cursor.Block.SourceRef.ParagraphIndex.ToString(CultureInfo.InvariantCulture),
                ["availableWidth"] = available.ToString("G17", CultureInfo.InvariantCulture),
                ["text"] = excerpt.ToString()
            }
        };
    }

    private static Diagnostic NoProgress(Block block)
    {
        return Problem(DiagnosticCodes.ENoLegalBreak, "排版循环没有消费内容。", block.SourceRef);
    }

    private static Diagnostic Cancelled()
    {
        return new Diagnostic
        {
            Code = DiagnosticCodes.Cancelled,
            Severity = Severity.Error,
            Stage = DiagnosticStage.Layout,
            Message = "排版已取消。"
        };
    }

    private static Diagnostic Problem(string code, string message, SourceRef? source)
    {
        return new Diagnostic
        {
            Code = code,
            Severity = Severity.Error,
            Stage = code == DiagnosticCodes.ESchemaInvalid ? DiagnosticStage.Protocol : DiagnosticStage.Layout,
            Message = message,
            SourceRef = source
        };
    }

    private static SourceRef Source(int paragraph, int? run, TextRange? range)
    {
        return new SourceRef { ParagraphIndex = paragraph, RunIndex = run, TextRange = range };
    }

    private readonly struct FitResult
    {
        private FitResult(bool fits, Diagnostic? error)
        {
            Fits = fits;
            Error = error;
        }

        public bool Fits { get; }

        public Diagnostic? Error { get; }

        public static FitResult Width(bool fits) => new FitResult(fits, null);

        public static FitResult EmptyFits() => new FitResult(true, null);

        public static FitResult Failed(Diagnostic error) => new FitResult(false, error);
    }

    private readonly struct SplitResult
    {
        private SplitResult(bool split, Diagnostic? error)
        {
            Split = split;
            Error = error;
        }

        public bool Split { get; }

        public Diagnostic? Error { get; }

        public static SplitResult DidSplit() => new SplitResult(true, null);

        public static SplitResult None() => new SplitResult(false, null);

        public static SplitResult Failed(Diagnostic error) => new SplitResult(false, error);
    }
}
