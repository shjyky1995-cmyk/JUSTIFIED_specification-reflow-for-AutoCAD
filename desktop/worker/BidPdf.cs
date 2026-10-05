using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace DocxWorkbench.Worker;

// 仅在本机读取文字层，不运行PDF动作、不展开附件、不进行OCR。
internal static class BidPdf
{
    internal static WorkerResult Extract(string? source)
    {
        if (string.IsNullOrWhiteSpace(source) || source.Length > 1000) throw new ArgumentException("请选择PDF文件。");
        var path = Path.GetFullPath(source);
        if (!Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase) || new FileInfo(path).Length > 20_000_000)
            throw new ArgumentException("请选择20MB以内的PDF。");
        try
        {
            using var document = PdfDocument.Open(path);
            if (document.NumberOfPages is < 1 or > 500) throw new ArgumentException("PDF页数须在1至500页之间，请拆分文件后导入；本次未截断摘录。");
            var blocks = new List<BidSourceBlock>();
            var noText = new List<int>();
            var images = new List<int>();
            var uncertain = new List<int>();
            var total = 0;
            for (int number = 1; number <= document.NumberOfPages; number++)
            {
                var page = document.GetPage(number);
                if (page.Letters.Count > 200_000) throw new ArgumentException("PDF单页文字量超过限额，请拆分后导入；本次未截断摘录。");
                var text = ContentOrderTextExtractor.GetText(page).Replace("\r\n", "\n").Replace('\r', '\n');
                total += text.Length;
                if (total > 600_000) throw new ArgumentException("PDF文字超过60万字符限额，请拆分后导入；本次未保留部分摘录。");
                if (string.IsNullOrWhiteSpace(text)) { noText.Add(number); continue; }
                if (page.GetImages().Any()) images.Add(number);
                if (text.Any(c => c is '\uFFFD' or '\0' || (char.IsControl(c) && c is not '\n' and not '\t'))) uncertain.Add(number);
                // 每块不超过要求摘录限额，长页优先在换行处分块，保留全部字符。
                int offset = 0, part = 0;
                while (offset < text.Length)
                {
                    int length = Math.Min(20_000, text.Length - offset);
                    if (offset + length < text.Length)
                    {
                        int newline = text.LastIndexOf('\n', offset + length - 1, length);
                        if (newline > offset) length = newline - offset + 1;
                        if (char.IsHighSurrogate(text[offset + length - 1])) length--;
                    }
                    if (blocks.Count >= 3000) throw new ArgumentException("PDF摘录块数超过限额，请拆分文件；本次未保留部分摘录。");
                    blocks.Add(new($"PDF物理页 {number} / 文字块 {++part}", text.Substring(offset, length)));
                    offset += length;
                }
            }
            var warnings = new List<WorkerDiagnostic>
            {
                new("BID_PDF_REVIEW", "warning", $"共{document.NumberOfPages}个物理页，{document.NumberOfPages - noText.Count}页提取到文字。页序从封面起按文件顺序计数，不等于印刷页码。提取文字不保证双栏、表格、页眉页脚与阅读顺序准确；批注、表单值、附件及图片文字未解析，请对照原件核对。")
            };
            void WarnPages(string code, string message, List<int> pages)
            {
                foreach (var group in pages.Chunk(80)) warnings.Add(new(code, "warning", message + string.Join("、", group) + "（物理页）。"));
            }
            WarnPages("BID_PDF_NO_TEXT", "没有可提取文字，可能为扫描或空白页；未执行OCR：", noText);
            WarnPages("BID_PDF_IMAGES", "同时含图片，图片内文字未识别：", images);
            WarnPages("BID_PDF_ENCODING", "文字含异常字符，需人工核对：", uncertain);
            return new WorkerResult(true, blocks.Count == 0 ? "PDF原件已保留，未发现可提取文字；请人工摘录。" : "PDF文字已按物理页提取；仍需对照原件核对。", path, blocks.Count, Array.Empty<string>(), warnings) { SourceBlocks = blocks };
        }
        catch (Exception error) when (error is not ArgumentException and not IOException and not UnauthorizedAccessException and not OutOfMemoryException)
        {
            // PDF库的加密/格式/字体错误统一进入既有可恢复失败路径；不输出业务原文。
            throw new InvalidDataException("PDF无法可靠提取，可能需要密码、文件损坏或含不支持的字体/结构；原件保留，请打开原件核对或转为无密码文字型PDF后重新导入。", error);
        }
    }
}
