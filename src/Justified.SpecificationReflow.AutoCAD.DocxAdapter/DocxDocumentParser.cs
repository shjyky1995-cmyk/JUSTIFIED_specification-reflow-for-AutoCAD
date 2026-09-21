using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.IO.Packaging;
using System.Xml;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using ModelDocument = Justified.SpecificationReflow.AutoCAD.Contracts.Documents.Document;
using ModelNumbering = Justified.SpecificationReflow.AutoCAD.Contracts.Documents.Numbering;
using Justified.SpecificationReflow.AutoCAD.Contracts.Ports;

namespace Justified.SpecificationReflow.AutoCAD.DocxAdapter;

public sealed class DocxDocumentParser : IDocumentParser
{
    public const string SchemaVersion = "1.0";

    private readonly DocxParseOptions _options;

    public DocxDocumentParser(DocxParseOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(options.DocumentId))
            throw new ArgumentException("DocumentId 不能为空。", nameof(options));
        if (string.IsNullOrWhiteSpace(options.DisciplineCode))
            throw new ArgumentException("DisciplineCode 不能为空。", nameof(options));
        if (options.StyleMap == null)
            throw new ArgumentException("StyleMap 不能为空。", nameof(options));
        if (options.StyleMap.Entries == null)
            throw new ArgumentException("StyleMap.Entries 不能为空。", nameof(options));
        foreach (var entry in options.StyleMap.Entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                throw new ArgumentException("样式映射项的 Key 不能为空。", nameof(options));
            if (entry.Target != BlockType.Heading1 && entry.Target != BlockType.Heading2 && entry.Target != BlockType.Paragraph)
                throw new ArgumentException("样式映射只能指向一级标题、二级标题或正文。", nameof(options));
        }

        if (options.MaxSourceBytes is long max && max <= 0)
            throw new ArgumentException("MaxSourceBytes 必须为正数。", nameof(options));
        if (options.MaxUncompressedBytes is long uncompressed && uncompressed <= 0)
            throw new ArgumentException("MaxUncompressedBytes 必须为正数。", nameof(options));
        _options = options;
    }

    public DocumentParseResult Parse(IDocumentSource source, ParseProfile profile, System.Threading.CancellationToken cancellationToken)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] bytes;
            using (var input = source.OpenRead())
                bytes = ReadAll(input, _options.MaxSourceBytes, cancellationToken);
            CheckZip(bytes, _options.MaxUncompressedBytes);
            return ParsePackage(source, bytes, Sha256(bytes), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Cancelled();
        }
        catch (ResourceLimitException ex)
        {
            return Failure(DiagnosticCodes.EResourceLimit, ex.Message, null);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            return ReadFailure(source, ex);
        }
    }

    private DocumentParseResult ParsePackage(IDocumentSource source, byte[] bytes, string hash, System.Threading.CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        WordprocessingDocument package;
        try
        {
            package = WordprocessingDocument.Open(stream, false);
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            return ReadFailure(source, ex);
        }

        using (package)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var body = package.MainDocumentPart?.Document?.Body;
            if (body == null)
                return ReadFailure(source, null, "DOCX 缺少正文。请另存为普通 DOCX。");

            var session = new Session(this, source, hash, package, cancellationToken);
            session.Walk(body);
            session.ScanStories();
            return session.Complete();
        }
    }

    private static byte[] ReadAll(Stream stream, long? maxBytes, System.Threading.CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        long total = 0;
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            total += read;
            if (maxBytes is long limit && total > limit)
                throw new ResourceLimitException("DOCX 超过调用方给出的源文件上限 " + limit + " 字节。");
            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static void CheckZip(byte[] bytes, long? maxUncompressed)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            long total = 0;
            foreach (var entry in zip.Entries)
            {
                if (entry.Length < 0) continue;
                total += entry.Length;
                if (maxUncompressed is long limit && total > limit)
                    throw new ResourceLimitException("DOCX 解压后超过调用方给出的上限 " + limit + " 字节。");
            }
        }
        catch (InvalidDataException ex)
        {
            throw new IOException("文件不是可读的 DOCX 压缩包。", ex);
        }
    }

    private static bool IsReadFailure(Exception ex)
    {
        return ex is IOException
            || ex is UnauthorizedAccessException
            || ex is InvalidDataException
            || ex is XmlException
            || ex is ArgumentException
            || ex is InvalidOperationException
            || ex is OpenXmlPackageException
            || ex is FileFormatException;
    }

    private static DocumentParseResult ReadFailure(IDocumentSource source, Exception? ex, string? detail = null)
    {
        var where = string.IsNullOrWhiteSpace(source.Info.LocalPath) ? source.Info.Name : source.Info.LocalPath;
        var reason = detail ?? Trim(ex?.Message, 300);
        var message = "无法读取 DOCX（损坏、加密或被占用）。请另存为普通未加密的 DOCX。路径：" + where;
        var details = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(source.Info.Name)) details["name"] = source.Info.Name;
        if (!string.IsNullOrEmpty(reason)) details["reason"] = reason;
        return Failure(DiagnosticCodes.EDocxRead, message, details);
    }

    private static DocumentParseResult Cancelled()
    {
        return Failure(DiagnosticCodes.Cancelled, "解析已取消。", null);
    }

    private static DocumentParseResult Failure(string code, string message, Dictionary<string, string>? details)
    {
        return new DocumentParseResult(null, new[]
        {
            new Diagnostic
            {
                Code = code,
                Severity = Severity.Error,
                Stage = DiagnosticStage.Parse,
                Message = message,
                Details = details
            }
        });
    }

    private static string Sha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var value in hash)
            builder.Append(value.ToString("x2"));
        return builder.ToString();
    }

    private static string Trim(string? text, int max)
    {
        if (text == null || text.Length == 0) return string.Empty;
        var flat = text.Replace('\r', ' ').Replace('\n', ' ');
        return flat.Length <= max ? flat : flat.Substring(0, max);
    }

    private sealed class ResourceLimitException : Exception
    {
        public ResourceLimitException(string message) : base(message)
        {
        }
    }

    private sealed class Session
    {
        private readonly DocxDocumentParser _parser;
        private readonly IDocumentSource _source;
        private readonly string _hash;
        private readonly WordprocessingDocument _package;
        private readonly System.Threading.CancellationToken _cancellation;
        private readonly StyleCatalog _styles;
        private readonly NumberingSession _numbering;
        private readonly List<Diagnostic> _diagnostics = new List<Diagnostic>();
        private readonly List<Block> _blocks = new List<Block>();
        private int _paragraphCount;
        private int _lastParagraphIndex = -1;

        public Session(DocxDocumentParser parser, IDocumentSource source, string hash, WordprocessingDocument package, System.Threading.CancellationToken cancellation)
        {
            _parser = parser;
            _source = source;
            _hash = hash;
            _package = package;
            _cancellation = cancellation;
            _styles = StyleCatalog.Load(package.MainDocumentPart, parser._options.StyleMap);
            _numbering = NumberingSession.Load(package.MainDocumentPart);
        }

        public void Walk(OpenXmlElement container)
        {
            foreach (var child in container.ChildElements)
            {
                _cancellation.ThrowIfCancellationRequested();
                Dispatch(child);
            }
        }

        public void ScanStories()
        {
            var main = _package.MainDocumentPart;
            if (main == null) return;
            foreach (var header in main.HeaderParts)
                NoteStory(header.Header?.InnerText, "header", "页眉未进入说明正文。");
            foreach (var footer in main.FooterParts)
                NoteStory(footer.Footer?.InnerText, "footer", "页脚未进入说明正文。");
            if (main.VbaProjectPart != null)
            {
                _diagnostics.Add(new Diagnostic
                {
                    Code = DocxDiagnosticCodes.MacroNotExecuted,
                    Severity = Severity.Info,
                    Stage = DiagnosticStage.Parse,
                    Message = "文档含宏，解析过程不会执行宏。"
                });
            }
        }

        public DocumentParseResult Complete()
        {
            if (HasError()) return new DocumentParseResult(null, _diagnostics);
            if (_blocks.Count > 0
                && _blocks[_blocks.Count - 1].Type == BlockType.Spacer
                && _blocks[_blocks.Count - 1].SourceRef.ParagraphIndex == _lastParagraphIndex)
            {
                _blocks.RemoveAt(_blocks.Count - 1);
            }

            var name = string.IsNullOrWhiteSpace(_source.Info.Name) ? "document.docx" : _source.Info.Name;
            var document = new ModelDocument
            {
                SchemaVersion = SchemaVersion,
                DocumentId = _parser._options.DocumentId.Trim(),
                DisciplineCode = _parser._options.DisciplineCode.Trim(),
                Source = new SourceInfo
                {
                    Kind = DocumentSourceKind.Docx,
                    Name = name,
                    ContentHash = _hash,
                    LocalPath = _source.Info.LocalPath
                },
                Blocks = _blocks,
                Extensions = new Dictionary<string, object>()
            };
            return new DocumentParseResult(document, _diagnostics);
        }

        private void Dispatch(OpenXmlElement element)
        {
            switch (element)
            {
                case Paragraph paragraph:
                    ReadParagraph(paragraph);
                    break;
                case Table table:
                    Report(table, "table", "检测到表格。V1 不排版表格，请删除或改成正文后再试。", Excerpt(table.InnerText));
                    break;
                case SdtBlock sdt when sdt.SdtContentBlock != null:
                    Walk(sdt.SdtContentBlock);
                    break;
                case SectionProperties:
                    break;
                default:
                    if (element.LocalName == "altChunk")
                        Report(element, "altChunk", "检测到外部文档片段。请把内容保存为普通正文后再试。", Excerpt(element.InnerText));
                    else if (element.LocalName == "customXml" || element.LocalName == "smartTag")
                        Walk(element);
                    else if (element.LocalName == "AlternateContent")
                    {
                        var chosen = PreferredAlternate(element);
                        if (chosen != null) Walk(chosen);
                    }
                    else if (!string.IsNullOrEmpty(element.InnerText))
                        Report(element, "unknown", "检测到未支持的正文内容（" + element.LocalName + "）。", Excerpt(element.InnerText));
                    break;
            }
        }

        private void ReadParagraph(Paragraph paragraph)
        {
            var index = _paragraphCount;
            _paragraphCount++;
            _lastParagraphIndex = index;
            var state = new ParagraphState(index);
            if (ContainsRevision(paragraph))
                state.Flag("revision", At(index) + "含有未接受的修订。请接受或拒绝修订后另存。");

            var properties = paragraph.ParagraphProperties;
            if (properties?.PageBreakBefore != null && IsOn(properties.PageBreakBefore))
                state.Pagination.Add("pageBreakBefore");
            if (properties != null && HasLocal(properties, "sectPr"))
                state.Pagination.Add("section");
            NoteNumberingRevision(properties?.NumberingProperties, state);
            state.Format.Absorb(properties?.ParagraphMarkRunProperties);

            var styleId = StringOf(properties?.ParagraphStyleId?.Val);
            _styles.AccumulateFormat(styleId, state.Format);
            foreach (var child in paragraph.ChildElements)
            {
                if (child is ParagraphProperties) continue;
                ReadInline(child, state, null);
            }

            FlushContent(state, paragraph);
            var mapped = _styles.TryResolve(styleId, out var blockType, out var direct);
            if (!mapped)
            {
                var styleName = direct != null && direct.Names.Count > 0 ? direct.Names[0] : string.Empty;
                state.Unmapped = true;
                Add(index, DiagnosticCodes.EUnsupportedContent, Severity.Error,
                    At(index) + "使用了未映射样式。请改成已映射的一级标题、二级标题或正文。",
                    Details("kind", "unmappedStyle", "styleId", styleId ?? string.Empty, "styleName", styleName, "excerpt", Excerpt(paragraph.InnerText)));
            }

            var numbering = EffectiveNumbering(paragraph);
            if (numbering != null)
            {
                if (_numbering.TryNext(numbering.Value.NumId, numbering.Value.Level, out var label, out var failure))
                {
                    state.Numbering = new ModelNumbering
                    {
                        Label = label,
                        SourceKind = NumberingSourceKind.Automatic,
                        Level = numbering.Value.Level
                    };
                }
                else
                {
                    state.NumberingFailed = true;
                    Add(index, DiagnosticCodes.ENumbering, Severity.Error,
                        At(index) + failure,
                        Details("kind", "numbering", "excerpt", Excerpt(paragraph.InnerText)));
                }
            }

            if (state.Pagination.Count > 0)
            {
                Add(index, DocxDiagnosticCodes.IgnoredPagination, Severity.Info,
                    At(index) + "的页面、分栏或分节符不控制 CAD 分页，已忽略。",
                    Details("break", string.Join(",", state.Pagination)));
            }

            if (state.Fatal) return;
            if (state.Format.Any)
            {
                Add(index, DocxDiagnosticCodes.IgnoredFormat, Severity.Info,
                    At(index) + "的加粗、下划线、颜色或字体设置已忽略，文本仍保留。",
                    Details("ignored", state.Format.Summary()));
            }

            var hasContent = false;
            foreach (var run in state.Runs)
            {
                if (run.Text.Length > 0) hasContent = true;
            }

            if (!hasContent && state.Numbering == null && state.Pagination.Count > 0) return;

            var block = new Block
            {
                Id = "p" + index,
                SourceRef = new SourceRef { ParagraphIndex = index },
                Runs = state.Runs
            };
            if (!hasContent && state.Numbering == null)
            {
                block.Type = BlockType.Spacer;
                block.SlotCount = 1;
            }
            else
            {
                block.Type = blockType;
                block.Numbering = state.Numbering;
            }

            _blocks.Add(block);
        }

        private void ReadInline(OpenXmlElement element, ParagraphState state, RunSemantic? semantic)
        {
            switch (element)
            {
                case Run run:
                    ReadRun(run, state);
                    break;
                case Hyperlink hyperlink:
                    foreach (var child in hyperlink.ChildElements)
                        ReadInline(child, state, semantic);
                    break;
                case SdtRun sdt when sdt.SdtContentRun != null:
                    foreach (var child in sdt.SdtContentRun.ChildElements)
                        ReadInline(child, state, semantic);
                    break;
                case SimpleField:
                    state.Flag("field", At(state.Index) + "含有未固化的域。请转换为静态文本后再试。");
                    break;
                case InsertedRun:
                case DeletedRun:
                case MoveFromRun:
                case MoveToRun:
                    state.Flag("revision", At(state.Index) + "含有未接受的修订。请接受或拒绝修订后另存。");
                    break;
                default:
                    var local = element.LocalName;
                    if (local == "smartTag" || local == "customXml")
                    {
                        foreach (var child in element.ChildElements)
                            ReadInline(child, state, semantic);
                    }
                    else if (local == "commentRangeStart" || local == "commentRangeEnd")
                        state.Flag("comment", At(state.Index) + "含有批注。请删除批注后再试。");
                    else
                        ReadLeaf(element, state, semantic ?? RunSemantic.Normal);
                    break;
            }
        }

        private void ReadRun(Run run, ParagraphState state)
        {
            state.Format.Absorb(run.RunProperties);
            var semantic = FormatFlags.Vertical(run.RunProperties)
                ?? _styles.SemanticOfRunStyle(StringOf(run.RunProperties?.RunStyle?.Val));
            foreach (var child in run.ChildElements)
                ReadLeaf(child, state, semantic);
        }

        private void ReadLeaf(OpenXmlElement element, ParagraphState state, RunSemantic semantic)
        {
            switch (element)
            {
                case Text text:
                    state.AppendText(text.Text ?? string.Empty, semantic);
                    break;
                case TabChar:
                case PositionalTab:
                    state.Flag("tab", At(state.Index) + "含有制表符。请改成明确文本，V1 不猜测缩进。");
                    break;
                case Break lineBreak:
                    ReadBreak(lineBreak, state);
                    break;
                case CarriageReturn:
                    state.AppendBreak();
                    break;
                case LastRenderedPageBreak:
                    break;
                case SoftHyphen:
                    state.AppendText("\u00AD", semantic);
                    break;
                case NoBreakHyphen:
                    state.AppendText("\u2011", semantic);
                    break;
                case Drawing drawing:
                    ClassifyGraphic(drawing, state);
                    break;
                case Picture picture:
                    ClassifyGraphic(picture, state);
                    break;
                case EmbeddedObject:
                    state.Flag("embeddedObject", At(state.Index) + "含有嵌入对象。请删除后再试。");
                    break;
                case FootnoteReference:
                    state.Flag("footnote", At(state.Index) + "含有脚注。V1 不排版脚注。");
                    break;
                case EndnoteReference:
                    state.Flag("endnote", At(state.Index) + "含有尾注。V1 不排版尾注。");
                    break;
                case FieldChar:
                case FieldCode:
                    state.Flag("field", At(state.Index) + "含有未固化的域。请转换为静态文本后再试。");
                    break;
                case SymbolChar:
                    state.Flag("symbol", At(state.Index) + "含有符号字体字符。请改成普通 Unicode 文本。");
                    break;
                case Ruby:
                    state.Flag("ruby", At(state.Index) + "含有拼音指南。请改成普通文本。");
                    break;
                default:
                    var local = element.LocalName;
                    if (local == "txbxContent")
                        state.Flag("textBox", At(state.Index) + "含有文本框。V1 不排版文本框。");
                    else if (local == "oMath" || local == "oMathPara")
                        state.Flag("equation", At(state.Index) + "含有公式。V1 不排版公式。");
                    else if (local == "instrText" || local == "fldSimple" || IsDateField(local))
                        state.Flag("field", At(state.Index) + "含有未固化的域。请转换为静态文本后再试。");
                    else if (local == "commentReference" || local == "annotationRef")
                        state.Flag("comment", At(state.Index) + "含有批注。请删除批注后再试。");
                    else if (local == "AlternateContent")
                    {
                        var chosen = PreferredAlternate(element);
                        if (chosen == null) return;
                        foreach (var child in chosen.ChildElements)
                            ReadLeaf(child, state, semantic);
                    }
                    else if (IsSkippable(local))
                        return;
                    else if (element.ChildElements.Count > 0)
                    {
                        foreach (var child in element.ChildElements)
                            ReadLeaf(child, state, semantic);
                    }
                    else if (!string.IsNullOrEmpty(element.InnerText))
                        state.Flag("unknown:" + local, At(state.Index) + "含有未支持的内容（" + local + "）。");
                    break;
            }
        }

        private static void ReadBreak(Break lineBreak, ParagraphState state)
        {
            if (lineBreak.Type == null || !lineBreak.Type.HasValue || lineBreak.Type.Value == BreakValues.TextWrapping)
            {
                state.AppendBreak();
                return;
            }

            if (lineBreak.Type.Value == BreakValues.Page) state.Pagination.Add("page");
            else if (lineBreak.Type.Value == BreakValues.Column) state.Pagination.Add("column");
            else state.Pagination.Add(lineBreak.Type.Value.ToString());
        }

        private static void ClassifyGraphic(OpenXmlElement graphic, ParagraphState state)
        {
            if (HasLocal(graphic, "txbxContent"))
                state.Flag("textBox", At(state.Index) + "含有文本框。V1 不排版文本框。");
            else if (HasLocal(graphic, "oMath") || HasLocal(graphic, "oMathPara"))
                state.Flag("equation", At(state.Index) + "含有公式。V1 不排版公式。");
            else
                state.Flag("image", At(state.Index) + "含有图片。V1 不排版图片。");
        }

        private (int NumId, int Level)? EffectiveNumbering(Paragraph paragraph)
        {
            int? numId = null;
            int? level = null;
            var off = false;
            void Take(NumberingProperties? props)
            {
                if (props == null || off) return;
                var id = IntOf(props.NumberingId?.Val);
                if (id == 0)
                {
                    off = true;
                    return;
                }

                if (id != null) numId ??= id;
                var ilvl = IntOf(props.NumberingLevelReference?.Val);
                if (ilvl != null) level ??= ilvl;
            }

            Take(paragraph.ParagraphProperties?.NumberingProperties);
            if (off || (numId != null && level != null))
                return numId == null || numId == 0 ? null : (numId.Value, level ?? 0);

            var styleId = StringOf(paragraph.ParagraphProperties?.ParagraphStyleId?.Val) ?? _styles.DefaultParagraphStyleId;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var styles = _package.MainDocumentPart?.StyleDefinitionsPart?.Styles;
            while (styleId != null && styleId.Length > 0 && seen.Add(styleId) && styles != null)
            {
                Style? style = null;
                foreach (var candidate in styles.Elements<Style>())
                {
                    if (string.Equals(StringOf(candidate.StyleId), styleId, StringComparison.OrdinalIgnoreCase))
                    {
                        style = candidate;
                        break;
                    }
                }

                if (style == null) break;
                Take(style.StyleParagraphProperties?.NumberingProperties);
                if (off) return null;
                if (numId != null) break;
                styleId = StringOf(style.BasedOn?.Val);
            }

            if (numId == null || numId == 0) return null;
            return (numId.Value, level ?? 0);
        }

        private static void NoteNumberingRevision(NumberingProperties? props, ParagraphState state)
        {
            if (props?.NumberingChange != null || props?.Inserted != null)
                state.Flag("revision", At(state.Index) + "含有未接受的修订。请接受或拒绝修订后另存。");
        }

        private void FlushContent(ParagraphState state, Paragraph paragraph)
        {
            var excerpt = Excerpt(paragraph.InnerText);
            for (var i = 0; i < state.Kinds.Count; i++)
            {
                Add(state.Index, DiagnosticCodes.EUnsupportedContent, Severity.Error, state.Messages[i],
                    Details("kind", state.Kinds[i], "excerpt", excerpt));
            }
        }

        private void Report(OpenXmlElement element, string kind, string message, string excerpt)
        {
            var index = _paragraphCount;
            Add(index, DiagnosticCodes.EUnsupportedContent, Severity.Error, "位置 " + (index + 1) + "：" + message,
                Details("kind", kind, "excerpt", excerpt));
        }

        private void NoteStory(string? text, string part, string message)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _diagnostics.Add(new Diagnostic
            {
                Code = DocxDiagnosticCodes.IgnoredHeaderFooter,
                Severity = Severity.Info,
                Stage = DiagnosticStage.Parse,
                Message = message,
                Details = Details("part", part)
            });
        }

        private void Add(int? paragraphIndex, string code, Severity severity, string message, Dictionary<string, string>? details)
        {
            _diagnostics.Add(new Diagnostic
            {
                Code = code,
                Severity = severity,
                Stage = DiagnosticStage.Parse,
                Message = message,
                SourceRef = paragraphIndex is int index ? new SourceRef { ParagraphIndex = index } : null,
                Details = details
            });
        }

        private bool HasError()
        {
            foreach (var diagnostic in _diagnostics)
            {
                if (diagnostic.Severity == Severity.Error) return true;
            }

            return false;
        }

        private static bool ContainsRevision(OpenXmlElement element)
        {
            foreach (var descendant in element.Descendants())
            {
                var name = descendant.LocalName;
                if (name == "ins" || name == "del" || name == "moveFrom" || name == "moveTo"
                    || name == "moveFromRangeStart" || name == "moveFromRangeEnd"
                    || name == "moveToRangeStart" || name == "moveToRangeEnd")
                    return true;
            }

            return false;
        }

        private static bool HasLocal(OpenXmlElement element, string name)
        {
            if (element.LocalName == name) return true;
            foreach (var descendant in element.Descendants())
            {
                if (descendant.LocalName == name) return true;
            }

            return false;
        }

        private static OpenXmlElement? PreferredAlternate(OpenXmlElement alternate)
        {
            OpenXmlElement? choice = null;
            OpenXmlElement? fallback = null;
            foreach (var child in alternate.ChildElements)
            {
                if (child.LocalName == "Choice" && choice == null) choice = child;
                if (child.LocalName == "Fallback" && fallback == null) fallback = child;
            }

            if (choice != null && choice.ChildElements.Count > 0) return choice;
            return fallback ?? choice;
        }

        private static bool IsDateField(string local)
        {
            return local == "dayShort" || local == "monthShort" || local == "yearShort"
                || local == "dayLong" || local == "monthLong" || local == "yearLong";
        }

        private static bool IsSkippable(string local)
        {
            return local == "bookmarkStart" || local == "bookmarkEnd"
                || local == "permStart" || local == "permEnd"
                || local == "proofErr" || local == "lastRenderedPageBreak"
                || local == "rPr" || local == "pPr";
        }

        private static bool IsOn(OnOffType element)
        {
            if (element.Val == null || !element.Val.HasValue) return true;
            return element.Val;
        }

        private static int? IntOf(DocumentFormat.OpenXml.Int32Value? value)
        {
            if (value == null || !value.HasValue) return null;
            return value.Value;
        }

        private static string? StringOf(DocumentFormat.OpenXml.StringValue? value)
        {
            if (value == null || !value.HasValue) return null;
            return value.Value;
        }

        private static string Excerpt(string? text)
        {
            return Trim(text, 80);
        }

        private static string At(int index)
        {
            return "第 " + (index + 1) + " 段";
        }

        private static Dictionary<string, string> Details(params string[] pairs)
        {
            var details = new Dictionary<string, string>();
            for (var i = 0; i + 1 < pairs.Length; i += 2)
            {
                if (!string.IsNullOrEmpty(pairs[i + 1])) details[pairs[i]] = pairs[i + 1];
            }

            return details;
        }

        private sealed class ParagraphState
        {
            private readonly HashSet<string> _kinds = new HashSet<string>(StringComparer.Ordinal);

            public ParagraphState(int index)
            {
                Index = index;
            }

            public int Index { get; }

            public List<TextRun> Runs { get; } = new List<TextRun>();

            public FormatFlags Format { get; } = new FormatFlags();

            public List<string> Pagination { get; } = new List<string>();

            public ModelNumbering? Numbering { get; set; }

            public bool Unmapped { get; set; }

            public bool NumberingFailed { get; set; }

            public bool Fatal => _kinds.Count > 0 || Unmapped || NumberingFailed;

            public void Flag(string kind, string message)
            {
                if (!_kinds.Add(kind)) return;
                Messages.Add(message);
                Kinds.Add(kind);
            }

            public List<string> Messages { get; } = new List<string>();

            public List<string> Kinds { get; } = new List<string>();

            public void AppendText(string text, RunSemantic semantic)
            {
                if (text.Length == 0) return;
                if (Runs.Count > 0 && Runs[Runs.Count - 1].Semantic == semantic && Runs[Runs.Count - 1].Text != "\n")
                    Runs[Runs.Count - 1].Text += text;
                else
                    Runs.Add(new TextRun { Text = text, Semantic = semantic });
            }

            public void AppendBreak()
            {
                Runs.Add(new TextRun { Text = "\n", Semantic = RunSemantic.Normal });
            }
        }
    }
}
