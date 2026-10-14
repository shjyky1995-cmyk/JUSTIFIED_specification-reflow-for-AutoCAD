import { app, BrowserWindow, ipcMain } from 'electron'
import { spawn, type ChildProcess } from 'node:child_process'
import { appendFileSync, existsSync, mkdirSync } from 'node:fs'
import { join, resolve } from 'node:path'

type LaunchResult = { success: boolean; message: string }
type RunningModule = { child: ChildProcess; ready: boolean; result: Promise<LaunchResult>; command: string; args: string[]; env: NodeJS.ProcessEnv; root: string }

export function registerBidFramework(appRoot: string, dataRoot: string) {
  let running: RunningModule | null = null
  ipcMain.handle('bid-framework-open', async event => {
    const parent = BrowserWindow.fromWebContents(event.sender)
    const restoreParent = () => {
      if (!parent || parent.isDestroyed()) return
      if (parent.isMinimized()) parent.restore()
      parent.show(); parent.focus()
    }
    const hideParent = () => { if (parent && !parent.isDestroyed()) parent.hide() }
    if (running && running.child.exitCode === null && !running.child.killed) {
      if (!running.ready) return running.result
      const current = running
      // 再次进入时激活既有模块，不能只返回“已经打开”。
      const activated = await new Promise<boolean>(resolveResult => {
        const process = spawn(current.command, current.args, { cwd: current.root, env: current.env, windowsHide: false, stdio: 'ignore' })
        const timer = setTimeout(() => resolveResult(false), 8000)
        process.once('error', () => { clearTimeout(timer); resolveResult(false) })
        process.once('exit', code => { clearTimeout(timer); resolveResult(code === 0) })
      })
      if (!activated) return { success: false, message: '投标模块未能恢复显示，工作台仍保留，请重试。' }
      hideParent()
      return { success: true, message: '已进入投标模块。' }
    }
    const bundled = join(process.resourcesPath, 'bidding-framework')
    const root = app.isPackaged && existsSync(bundled) ? bundled : resolve(appRoot, '../bidding-framework/client')
    const packagedCommand = join(root, 'EngiSpace-Bidding.exe')
    const packaged = existsSync(packagedCommand)
    const entry = packaged ? (existsSync(join(root, 'resources/app.asar')) ? join(root, 'resources/app.asar') : join(root, 'resources/app/electron/engispace-entry.cjs')) : join(root, 'electron/engispace-entry.cjs')
    const command = packaged ? packagedCommand : join(root, 'node_modules/electron/dist/electron.exe')
    if (!existsSync(entry) || !existsSync(command)) throw new Error('未找到投标模块，请使用完整的框架试用包。')
    const directory = join(dataRoot, 'bidding-framework')
    mkdirSync(directory, { recursive: true })
    const env: NodeJS.ProcessEnv = { ...process.env, ENGISPACE_BIDDING_DATA_ROOT: directory }
    delete env.ELECTRON_RUN_AS_NODE; delete env.DSS_FRAMEWORK_HIDE; delete env.ELECTRON_RENDERER_URL
    if (parent) env.ENGISPACE_BIDDING_HOST_STATE = JSON.stringify({ bounds: parent.getBounds(), maximized: parent.isMaximized(), fullscreen: parent.isFullScreen() })
    const log = (state: string) => { try { appendFileSync(join(directory, 'module-startup.log'), JSON.stringify({ at: new Date().toISOString(), state }) + '\n') } catch {} }
    const args = packaged ? [] : [root]
    // 用户操作的GUI须显示；windowsHide=true会隐藏Electron首个窗口。
    const child = spawn(command, args, { cwd: root, env, windowsHide: false, stdio: ['ignore', 'pipe', 'pipe'] })
    child.stderr?.resume(); log('starting')
    let finish: (result: LaunchResult) => void = () => {}
    const result = new Promise<LaunchResult>(resolveResult => { finish = resolveResult })
    const current: RunningModule = { child, ready: false, result, command, args, env, root }
    running = current
    let settled = false, output = ''
    const complete = (value: LaunchResult) => { if (settled) return; settled = true; clearTimeout(timer); finish(value) }
    const timer = setTimeout(() => { log('startup-timeout'); restoreParent(); complete({ success: false, message: '投标模块尚未显示，工作台仍保留，请查看模块窗口或重试。' }) }, 30_000)
    child.stdout?.on('data', bytes => {
      if (current.ready) return
      output = (output + bytes.toString()).slice(-8000)
      const line = output.split(/\r?\n/).find(line => line.startsWith('ENGISPACE_BIDDING_READY '))
      if (!line) return
      try { if (JSON.parse(line.slice('ENGISPACE_BIDDING_READY '.length)).visible !== true) return } catch { return }
      current.ready = true; log('visible-ready'); hideParent(); complete({ success: true, message: '已进入投标模块。' })
    })
    child.once('error', () => { if (running === current) running = null; log('spawn-error'); restoreParent(); complete({ success: false, message: '投标模块启动失败，工作台仍保留，请检查完整程序目录。' }) })
    child.once('exit', code => { if (running === current) running = null; log(`exited-${code ?? 'unknown'}`); restoreParent(); complete({ success: false, message: `投标模块已退出（${code ?? '未知'}），工作台和原稿仍保留。` }) })
    return result
  })
}
