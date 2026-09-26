import { app, BrowserWindow, dialog, ipcMain, shell } from 'electron'
import { spawn } from 'node:child_process'
import { existsSync } from 'node:fs'
import { dirname, extname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const maxPayload = 450_000
const selectedPaths = new Set<string>()

type WorkRequest = { operation: 'generate' | 'inspect'; path?: string; [key: string]: unknown }

function workerCommand(): { command: string; args: string[] } {
  const explicit = process.env.DSS_WORKER_EXE
  if (explicit && existsSync(explicit)) return { command: explicit, args: [] }
  const bundled = join(process.resourcesPath, 'worker', 'DocxWorkbench.Worker.exe')
  if (existsSync(bundled)) return { command: bundled, args: [] }
  const local = resolve(here, '../../worker/bin/Debug/net10.0/DocxWorkbench.Worker.dll')
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
    backgroundColor: '#101a2d',
    title: 'Word 设计说明工作台',
    show: false,
    webPreferences: {
      preload: join(here, '../electron/preload.cjs'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
    },
  })
  window.setMenuBarVisibility(false)
  window.once('ready-to-show', () => window.show())
  void window.loadFile(join(here, '../dist/index.html'))
}

app.whenReady().then(() => {
  ipcMain.handle('choose-save', async () => {
    const result = await dialog.showSaveDialog({ title: '保存说明 DOCX', defaultPath: '设计说明.docx', filters: [{ name: 'Word 文档', extensions: ['docx'] }] })
    if (result.canceled || !result.filePath) return null
    selectedPaths.add(docxPath(result.filePath))
    return result.filePath
  })
  ipcMain.handle('choose-open', async () => {
    const result = await dialog.showOpenDialog({ title: '选择 DOCX', properties: ['openFile'], filters: [{ name: 'Word 文档', extensions: ['docx'] }] })
    if (result.canceled || !result.filePaths[0]) return null
    selectedPaths.add(docxPath(result.filePaths[0]))
    return result.filePaths[0]
  })
  ipcMain.handle('work', async (_event, request: WorkRequest) => {
    if (!request || typeof request !== 'object') throw new Error('请求无效。')
    request.path = docxPath(request.path)
    if (!selectedPaths.has(request.path)) throw new Error('请先从桌面窗口选择文件。')
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
