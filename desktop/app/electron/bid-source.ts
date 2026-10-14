import { readFileSync } from 'node:fs'
import { extname } from 'node:path'
import type { BidSource, SourceBlock } from '../src/features/bidding/model.ts'

type Extraction = { success: boolean; message: string; sourceBlocks?: SourceBlock[]; diagnostics?: { message: string }[] }
export function extractBidText(bytes: Uint8Array): Pick<BidSource, 'blocks' | 'warnings'> {
  if (bytes.length > 500_000) return { blocks: [], warnings: ['TXT超过50万字节提取限额，原件已保留，请拆分后重新导入；未保留部分摘录。'] }
  let text: string
  try {
    const encoding = bytes[0] === 0xff && bytes[1] === 0xfe ? 'utf-16le' : bytes[0] === 0xfe && bytes[1] === 0xff ? 'utf-16be' : 'utf-8'
    text = new TextDecoder(encoding, { fatal: true }).decode(bytes)
    if (text.includes('\0')) throw new Error('非文字内容')
  } catch { return { blocks: [], warnings: ['TXT编码无法可靠识别，原件已保留；请转为UTF-8或带BOM的UTF-16后重新导入。'] } }
  const blocks: SourceBlock[] = []
  const lines = text.replace(/\r\n?/g, '\n').split('\n')
  for (let line = 0; line < lines.length; line++) {
    if (!lines[line].trim()) continue
    let offset = 0, part = 0
    while (offset < lines[line].length) {
      let end = Math.min(offset + 20_000, lines[line].length)
      if (end < lines[line].length && /[\uD800-\uDBFF]/.test(lines[line][end - 1])) end--
      blocks.push({ location: `文本行 ${line + 1} / 文字块 ${++part}`, text: lines[line].slice(offset, end) })
      if (blocks.length > 3000) return { blocks: [], warnings: ['TXT摘录块数超过3000块，请拆分后重新导入；未保留部分摘录。'] }
      offset = end
    }
  }
  return { blocks, warnings: blocks.length ? ['TXT已逐行完整提取，行号包含空行；请对照原件核对编码与内容。'] : ['TXT无可提取正文，请打开原件核对。'] }
}

export async function extractBidSource(source: BidSource, path: string, runWorker: (request: { operation: string; path: string }) => Promise<unknown>): Promise<BidSource> {
  const extension = extname(path).toLowerCase()
  if (extension === '.txt') return { ...source, ...extractBidText(readFileSync(path)) }
  if (extension !== '.docx' && extension !== '.pdf') return { ...source, warnings: ['图片原件已保存，尚未执行OCR，请打开原件人工摘录要求。'] }
  try {
    const extracted = await runWorker({ operation: 'bid-extract', path }) as Extraction
    return { ...source, blocks: extracted.success ? extracted.sourceBlocks ?? [] : [], warnings: extracted.success ? (extracted.diagnostics ?? []).map(d => d.message) : ['自动摘录失败：' + extracted.message + '；原文件已保存，请人工核对。'] }
  } catch { return { ...source, blocks: [], warnings: ['自动摘录失败或超过处理时限，原文件已保存；请拆分资料或打开原件人工核对。'] } }
}
