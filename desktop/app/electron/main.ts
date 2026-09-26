import { app, BrowserWindow, dialog, ipcMain, shell } from 'electron'
import { spawn } from 'node:child_process'
import { existsSync } from 'node:fs'
import { extname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { createStore, type DesktopStore, type SaveResult } from '../src/shared/store.ts'
import { parseNote, serializeNote, type Note, type NoteSummary, type StoredProject } from '../src/shared/model.ts'

const here = fileURLToPath(new URL('.', import.meta.url))
const appRoot = resolve(here, '../..')
const maxPayload = 450_000
const selectedPaths = new Set<string>()

type WorkRequest = { operation: 'generate' | 'inspect'; path?: string; document?: unknown; [key: string]: unknown }

function workerCommand(): { command: string; args: string[] } {
  const explicit = process.env.DSS_WORKER_EXE
  if (explicit && existsSync(explicit)) return { command: explicit, args: [] }
  const bundled = join(process.resourcesPath, 'worker', 'DocxWorkbench.Worker.exe')
  if (existsSync(bundled)) return { command: bundled, args: [] }
  const local = resolve(here, '../../../worker/bin/Debug/net10.0/DocxWorkbench.Worker.dll')
  if (existsSync(local)) {
    const dotnet = process.env.DSS_DOTNET_EXE || 'dotnet'
    return { command: dotnet, args: [local] }
  }
  throw new Error('尚未找到 DOCX 工作进程，请先构建桌面程序。')
}

function runWorker(request: WorkRequest): Promise<unknown> {
  if (!['generate', 'inspect'].includes(request.operation)) throw new Error('不支持的操作。')
  const payload = JSON.stringify(request)
  if (payload.length > maxPayload) throw new Error('填写内容过长。')
  const { command, args } = workerCommand()
  return new Promise((resolveResult, reject) => {
    const child = spawn(command, args, { windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] })
    let output = ''
    let errorOutput = ''
    const timer = setTimeout(() => child.kill(), 30_000)
    child.stdout.setEncoding('utf8')
    child.stderr.setEncoding('utf8')
    child.stdout.on('data', (chunk: string) => {
      output += chunk
      if (output.length > 2_000_000) child.kill()
    })
    child.stderr.on('data', (chunk: string) => { errorOutput += chunk.slice(0, 2000) })
    child.on('error', error => { clearTimeout(timer); reject(error) })
    child.on('close', () => {
      clearTimeout(timer)
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
  const window = new BrowserWindow({
    width: 1280,
    height: 820,
    minWidth: 1050,
    minHeight: 680,
    backgroundColor: '#eef3fa',
    title: '设计说明工作台',
    show: false,
    webPreferences: {
      preload: join(appRoot, 'electron/preload.cjs'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
    },
  })
  window.setMenuBarVisibility(false)
  window.once('ready-to-show', () => window.show())
  void window.loadFile(join(appRoot, 'dist/index.html'))
}

app.whenReady().then(() => {
  const store: DesktopStore = createStore(join(app.getPath('userData'), 'data'))

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

  ipcMain.handle('choose-save', async () => {
    const result = await dialog.showSaveDialog({ title: '导出说明 DOCX', defaultPath: '设计说明.docx', filters: [{ name: 'Word 文档', extensions: ['docx'] }] })
    if (result.canceled || !result.filePath) return null
    selectedPaths.add(docxPath(result.filePath))
    return result.filePath
  })

  ipcMain.handle('work', async (_event, request: WorkRequest) => {
    if (!request || typeof request !== 'object') throw new Error('请求无效。')
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
