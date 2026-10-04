import { dialog, ipcMain, shell } from 'electron'
import { basename, extname, resolve } from 'node:path'
import { readFileSync, statSync, writeFileSync } from 'node:fs'
import { createBidStore } from '../src/features/bidding/store.ts'
import { bidDocument, isoNow, type BidSource } from '../src/features/bidding/model.ts'

export function registerBidIpc(root: string, runWorker: (request: { operation: string; path?: string; [key: string]: unknown }) => Promise<unknown>, allowPath: (path: string) => void) {
  const store = createBidStore(root)
  ipcMain.handle('bid-list', () => store.list())
  ipcMain.handle('bid-load', (_event, id: string) => store.load(id))
  ipcMain.handle('bid-save', (_event, value: unknown) => store.save(value))
  ipcMain.handle('bid-duplicate', (_event, id: string) => store.duplicate(id))
  ipcMain.handle('bid-import-source', async () => {
    const result = await dialog.showOpenDialog({ title: '导入本地招标资料或证明附件', properties: ['openFile'], filters: [{ name: '资料', extensions: ['docx','pdf','png','jpg','jpeg','txt'] }] })
    if (result.canceled || !result.filePaths[0]) return null
    const source = store.importAsset(result.filePaths[0])
    const path = store.assetPath(source.asset)
    if (extname(path) === '.docx') {
      try {
        const extracted = await runWorker({ operation: 'bid-extract', path }) as { success: boolean; message: string; sourceBlocks?: BidSource['blocks']; diagnostics?: { message: string }[] }
        source.blocks = extracted.sourceBlocks ?? []
        source.warnings = extracted.success ? (extracted.diagnostics ?? []).map(d => d.message) : ['自动摘录失败：' + extracted.message + '；原文件已保存，请人工核对。']
      } catch (error) { source.warnings = ['自动摘录失败，原文件已保存：' + (error instanceof Error ? error.message : '')] }
    } else if (extname(path) === '.txt') {
      if (source.bytes > 500_000) source.warnings = ['文本超过预览限额，请打开原文人工摘录。']
      else { source.blocks = [{ location: '文本全文', text: readFileSync(path, 'utf8').slice(0, 50_000) }]; source.warnings = ['按UTF-8读取；若乱码请转换编码后重新导入。', ...(source.bytes > 50_000 ? ['仅预览前5万字符，请核对原文件。'] : [])] }
    } else source.warnings = ['本阶段未自动解析PDF/图片，请打开原文件人工摘录要求；未执行OCR。']
    return source
  })
  ipcMain.handle('bid-open-source', async (_event, id: string, sourceId: string) => {
    const source = store.load(id).bid.sources.find(s => s.id === sourceId)
    if (!source) throw new Error('资料不存在。')
    store.assetBytes(source)
    const error = await shell.openPath(store.assetPath(source.asset)); if (error) throw new Error(error)
  })
  ipcMain.handle('bid-export', async (_event, id: string, mode: 'draft' | 'reviewed') => {
    if (!['draft','reviewed'].includes(mode)) throw new Error('导出方式无效。')
    const bid = store.load(id).bid
    for (const source of bid.sources) store.assetBytes(source)
    const document = bidDocument(bid, mode)
    const safeName = bid.title.replace(/[<>:"/\\|?*\x00-\x1f]/g, '_').slice(0, 80) || '投标文件'
    const choice = await dialog.showSaveDialog({ title: '导出可编辑投标Word', defaultPath: safeName + (mode === 'draft' ? '-草稿' : '-已核对稿') + '.docx', filters: [{ name: 'Word', extensions: ['docx'] }] })
    if (choice.canceled || !choice.filePath) return null
    const path = resolve(choice.filePath)
    if (extname(path).toLowerCase() !== '.docx') throw new Error('请选择DOCX扩展名。')
    const result = await runWorker({ operation: 'bid-export', path, bidDocument: document }) as { success: boolean; message: string; path: string }
    if (result.success) {
      allowPath(path)
      bid.exports = [...bid.exports.slice(-99), { at: isoNow(), mode, fileName: basename(path), revision: bid.revision }]
      const saved = store.save(bid)
      if (!saved.ok) result.message += ' 导出成功，但记录保存失败：' + saved.error
    }
    return result
  })
  ipcMain.handle('bid-backup', async (_event, id: string) => {
    const json = store.backup(id)
    const choice = await dialog.showSaveDialog({ title: '备份此投标及全部引用附件', defaultPath: `投标备份-${Date.now()}.json`, filters: [{ name: '投标备份', extensions: ['json'] }] })
    if (choice.canceled || !choice.filePath) return null
    writeFileSync(choice.filePath, json, { flag: 'wx', encoding: 'utf8' }); return choice.filePath
  })
  ipcMain.handle('bid-restore', async () => {
    const choice = await dialog.showOpenDialog({ title: '恢复投标备份为新副本', properties: ['openFile'], filters: [{ name: '投标备份', extensions: ['json'] }] })
    if (choice.canceled || !choice.filePaths[0]) return null
    if (statSync(choice.filePaths[0]).size > 115_000_000) throw new Error('备份过大。')
    return store.restore(readFileSync(choice.filePaths[0], 'utf8'))
  })
}
