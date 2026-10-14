import { app, BrowserWindow, dialog, ipcMain, shell } from 'electron'
import { spawn } from 'node:child_process'
import { existsSync, readdirSync, readFileSync, writeFileSync, mkdirSync } from 'node:fs'
import { extname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { createStore, type DesktopStore, type SaveResult } from '../src/shared/store.ts'
import { parseNote, serializeNote, type Note, type NoteSummary, type StoredProject } from '../src/shared/model.ts'
import { workerDotnetCommand } from '../src/shared/dotnet.ts'
import { findProjectCatalogPath } from '../src/shared/catalog-path.ts'
import { registerBidIpc } from './bid-ipc.ts'
import { registerEmbeddedBidding } from './bid-embedded.ts'

const here = fileURLToPath(new URL('.', import.meta.url))
const appRoot = resolve(here, '../..')
const maxPayload = 450_000
const selectedPaths = new Set<string>()
const closeAllowed = new Set<number>()
const closePending = new Set<number>()
let biddingModule: ReturnType<typeof registerEmbeddedBidding> | undefined
// 便携候选的数据、Chromium缓存和临时文件与程序同盘，避免写入 C 盘。
const portable = app.isPackaged && existsSync(join(process.resourcesPath, '.engispace-portable'))
const configuredData = process.env.DSS_USER_DATA_ROOT || (portable ? resolve(process.resourcesPath, '../../portable-data') : '')
if (configuredData) {
  const userData = resolve(configuredData), temporary = join(userData, 'temp')
  mkdirSync(temporary, { recursive: true })
  app.setPath('userData', userData); app.setPath('sessionData', userData); app.setPath('temp', temporary)
  process.env.TEMP = temporary; process.env.TMP = temporary
}
const hasLock = app.requestSingleInstanceLock()
if (!hasLock) app.quit()
app.on('second-instance', () => {
  const window = BrowserWindow.getAllWindows()[0]
  if (window) { if (window.isMinimized()) window.restore(); window.show(); window.focus() }
})

type WorkRequest = { operation: string; path?: string; document?: unknown; [key: string]: unknown }

function workerCommand(): { command: string; args: string[] } {
  const explicit = process.env.DSS_WORKER_EXE
  if (explicit && existsSync(explicit)) return { command: explicit, args: [] }
  const bundled = join(process.resourcesPath, 'worker', 'DocxWorkbench.Worker.exe')
  if (existsSync(bundled)) return { command: bundled, args: [] }
  const local = resolve(here, '../../../worker/bin/Debug/net10.0/DocxWorkbench.Worker.dll')
  if (existsSync(local)) {
    const dotnet = process.env.DSS_DOTNET_EXE || workerDotnetCommand(appRoot).command
    return { command: dotnet, args: [local] }
  }
  throw new Error('尚未找到 DOCX 工作进程，请先构建桌面程序。')
}

function runWorker(request: WorkRequest): Promise<unknown> {
  if (!['generate', 'inspect', 'bid-extract', 'bid-export'].includes(request.operation)) throw new Error('不支持的操作。')
  const payload = JSON.stringify(request)
  if (payload.length > (request.operation.startsWith('bid-') ? 2_000_000 : maxPayload)) throw new Error('填写内容过长。')
  const { command, args } = workerCommand()
  return new Promise((resolveResult, reject) => {
    const child = spawn(command, args, { windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] })
    let output = ''
    let errorOutput = ''
    let timedOut = false
    const timer = setTimeout(() => { timedOut = true; child.kill() }, 30_000)
    child.stdout.setEncoding('utf8')
    child.stderr.setEncoding('utf8')
    child.stdout.on('data', (chunk: string) => {
      output += chunk
      if (output.length > 4_000_000) child.kill()
    })
    child.stderr.on('data', (chunk: string) => { errorOutput += chunk.slice(0, 2000) })
    child.on('error', error => { clearTimeout(timer); reject(error) })
    child.on('close', () => {
      clearTimeout(timer)
      if (timedOut) { reject(new Error(request.operation === 'bid-extract' ? '资料提取超过30秒，原件已保留，请拆分后导入或人工摘录。' : '导出用时过长，请重试或减少单份说明内容。')); return }
      try { resolveResult(JSON.parse(output)) }
      catch { reject(new Error(errorOutput || 'DOCX 工作进程没有返回结果。')) }
    })
    child.stdin.end(payload)
  })
}

function docxPath(value: unknown): string {
  if (typeof value !== 'string' || extname(value).toLowerCase() !== '.docx') throw new Error('请选择 DOCX 文件。')
  return resolve(value)
}

function makeWindow(): void {
  const assetDirectory = join(appRoot, 'dist/assets')
  const iconFile = existsSync(assetDirectory) ? readdirSync(assetDirectory).find(name => name.startsWith('product-icon-') && name.endsWith('.png')) : undefined
  const window = new BrowserWindow({
    width: 1280,
    height: 820,
    minWidth: 900,
    minHeight: 680,
    backgroundColor: '#f9f9fb',
    title: 'EngiSpace · 工程编制工作台',
    ...(iconFile ? { icon: join(assetDirectory, iconFile) } : {}),
    show: false,
    webPreferences: {
      preload: join(appRoot, 'electron/preload.cjs'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
    },
  })
  window.setMenuBarVisibility(false)
  window.webContents.setWindowOpenHandler(() => ({ action: 'deny' }))
  window.webContents.on('will-navigate', event => event.preventDefault())
  window.on('close', event => {
    if (closeAllowed.has(window.webContents.id)) return
    event.preventDefault()
    if (closePending.has(window.webContents.id)) return
    closePending.add(window.webContents.id)
    window.webContents.send('before-close')
    setTimeout(() => {
      if (!window.isDestroyed() && closePending.has(window.webContents.id)) {
        void dialog.showMessageBox(window, { type: 'warning', title: '窗口暂未响应', message: '尚未收到保存结果。继续等待可保护最新修改；强制关闭后可恢复最后成功保存的草稿。', buttons: ['继续编辑', '强制关闭'], defaultId: 0, cancelId: 0 }).then(answer => {
          if (window.isDestroyed()) return
          closePending.delete(window.webContents.id)
          if (answer.response === 1) { closeAllowed.add(window.webContents.id); window.close() }
        })
      }
    }, 15_000)
  })
  window.once('ready-to-show', () => window.show())
  void window.loadFile(join(appRoot, 'dist/index.html'))
}

app.whenReady().then(() => {
  if (!hasLock) return
  const dataRoot = join(app.getPath('userData'), 'data')
  mkdirSync(dataRoot, { recursive: true })
  const settingsFile = join(app.getPath('userData'), 'settings.json')
  let configuredLibrary: string | undefined
  try { configuredLibrary = JSON.parse(readFileSync(settingsFile, 'utf8')).catalogPath } catch { /* 首次启动没有配置。 */ }
  const bundledLibrary = join(process.resourcesPath, 'content-library', 'catalog.json')
  let catalogPath = process.env.DSS_CONTENT_LIBRARY_PATH || configuredLibrary || (existsSync(bundledLibrary) ? bundledLibrary : findProjectCatalogPath(appRoot))
  let store: DesktopStore = createStore(dataRoot, catalogPath)
  registerBidIpc(dataRoot, runWorker, path => selectedPaths.add(path))
  biddingModule = registerEmbeddedBidding(appRoot, dataRoot)

  ipcMain.on('close-ready', async (event, saved: boolean) => {
    const window = BrowserWindow.fromWebContents(event.sender)
    if (!window || !closePending.has(event.sender.id)) return
    if (saved !== true) {
      const answer = await dialog.showMessageBox(window, { type: 'warning', title: '草稿尚未保存', message: '本次修改保存失败。继续编辑可以重试保存，或先导出备份。', buttons: ['继续编辑', '放弃未保存更改并关闭'], defaultId: 0, cancelId: 0 })
      if (answer.response === 0) { closePending.delete(event.sender.id); return }
    }
    if (biddingModule?.isActive()) {
      const allowed = await biddingModule.prepareClose()
      if (!allowed) { closePending.delete(event.sender.id); return }
      await biddingModule.dispose()
    }
    closeAllowed.add(event.sender.id)
    closePending.delete(event.sender.id)
    window.close()
  })

  ipcMain.handle('app-info', () => ({ version: app.getVersion(), dataRoot, catalogPath, packaged: app.isPackaged }))
  ipcMain.handle('open-data-folder', () => shell.openPath(dataRoot))
  ipcMain.handle('note-duplicate', (_event, id: string) => store.duplicateNote(id))
  ipcMain.handle('backup-export', async () => {
    const result = await dialog.showSaveDialog({ title: '备份本机全部草稿与项目', defaultPath: `设计说明备份-${Date.now()}.json`, filters: [{ name: '设计说明备份', extensions: ['json'] }] })
    if (result.canceled || !result.filePath) return null
    writeFileSync(result.filePath, store.backup(), { encoding: 'utf8', flag: 'wx' })
    return result.filePath
  })
  ipcMain.handle('backup-import', async () => {
    const result = await dialog.showOpenDialog({ title: '恢复设计说明备份（新增副本）', properties: ['openFile'], filters: [{ name: '设计说明备份', extensions: ['json'] }] })
    if (result.canceled || !result.filePaths[0]) return null
    return store.restoreBackup(readFileSync(result.filePaths[0], 'utf8'))
  })
  ipcMain.handle('catalog-choose', async () => {
    const result = await dialog.showOpenDialog({ title: '选择本机资料库 catalog.json', properties: ['openFile'], filters: [{ name: '资料库 JSON', extensions: ['json'] }] })
    if (result.canceled || !result.filePaths[0]) return null
    const nextPath = result.filePaths[0]
    const nextStore = createStore(dataRoot, nextPath)
    const catalog = nextStore.loadCatalog()
    if (!catalog) throw new Error('资料库不存在。')
    writeFileSync(settingsFile, JSON.stringify({ catalogPath: nextPath }), 'utf8')
    catalogPath = nextPath
    store = nextStore
    return catalog
  })

  ipcMain.handle('notes-list', (): NoteSummary[] => store.listNotes())

  ipcMain.handle('note-load', (_event, id: unknown): Note => {
    if (typeof id !== 'string' || id.length === 0) throw new Error('说明编号无效。')
    return store.loadNote(id)
  })

  ipcMain.handle('note-save', (_event, note: unknown): SaveResult => {
    const validated = parseNote(note)
    return store.saveNote(validated)
  })

  ipcMain.handle('note-delete', (_event, id: unknown): void => {
    if (typeof id !== 'string' || id.length === 0) throw new Error('说明编号无效。')
    store.deleteNote(id)
  })

  ipcMain.handle('projects-list', (): StoredProject[] => store.listProjects())

  ipcMain.handle('project-save', (_event, project: unknown): StoredProject[] => {
    if (!project || typeof project !== 'object') throw new Error('项目资料无效。')
    return store.saveProject(project as StoredProject)
  })

  ipcMain.handle('catalog-load', () => store.loadCatalog())

  ipcMain.handle('choose-save', async (_event, name: unknown) => {
    const fileName = (typeof name === 'string' ? name : '设计说明').replace(/[<>:"/\\|?*\x00-\x1f]/g, '_').trim().slice(0, 100) || '设计说明'
    const result = await dialog.showSaveDialog({ title: '导出说明 DOCX', defaultPath: fileName + '.docx', filters: [{ name: 'Word 文档', extensions: ['docx'] }] })
    if (result.canceled || !result.filePath) return null
    selectedPaths.add(docxPath(result.filePath))
    return result.filePath
  })

  ipcMain.handle('work', async (_event, request: WorkRequest) => {
    if (!request || typeof request !== 'object') throw new Error('请求无效。')
    if (!['generate', 'inspect'].includes(request.operation)) throw new Error('请使用对应的模块操作。')
    request.path = docxPath(request.path)
    if (!selectedPaths.has(request.path)) throw new Error('请先从桌面窗口选择文件。')
    if (request.operation === 'generate' && request.document !== undefined) {
      const serialized = serializeNoteLength(request.document)
      if (serialized > maxPayload) throw new Error('填写内容过长。')
    }
    return runWorker(request)
  })

  ipcMain.handle('open-docx', async (_event, path: unknown) => {
    const selected = docxPath(path)
    if (!selectedPaths.has(selected)) throw new Error('请先从桌面窗口选择文件。')
    if (!existsSync(selected)) throw new Error('文件已移动或删除。')
    const error = await shell.openPath(selected)
    if (error) throw new Error(error)
  })

  makeWindow()
  app.on('activate', () => { if (BrowserWindow.getAllWindows().length === 0) makeWindow() })
})

app.on('window-all-closed', () => { if (process.platform !== 'darwin') app.quit() })

function serializeNoteLength(document: unknown): number {
  try {
    return JSON.stringify(document).length
  } catch {
    throw new Error('说明内容无法序列化。')
  }
}
