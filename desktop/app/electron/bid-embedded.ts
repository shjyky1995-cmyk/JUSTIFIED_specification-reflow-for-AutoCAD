import { app, BrowserWindow, WebContentsView, dialog, ipcMain, net, protocol, session } from 'electron'
import { spawn, type ChildProcess } from 'node:child_process'
import { existsSync, mkdirSync } from 'node:fs'
import { join, resolve, sep } from 'node:path'
import { pathToFileURL } from 'node:url'
import { createRequire } from 'node:module'

protocol.registerSchemesAsPrivileged([{ scheme: 'yibiao-asset', privileges: { standard: true, secure: true, supportFetchAPI: true, corsEnabled: true } }])
type Wire = { version: number; kind: string; [key: string]: any }
type Pending = { resolve: (value: any) => void; reject: (error: Error) => void; timer: ReturnType<typeof setTimeout> }

export function registerEmbeddedBidding(appRoot: string, dataRoot: string) {
  const data = join(dataRoot, 'bidding-framework')
  const pending = new Map<number, Pending>(), guards = new Map<number, (allowed: boolean) => void>()
  const installed = new Set<string>(), installedEvents = new Set<string>()
  let allowed = new Set<string>(), allowedEvents = new Set<string>()
  let engine: ChildProcess | null = null, view: WebContentsView | null = null, parent: BrowserWindow | null = null
  let nextId = 0, opening: Promise<{ success: boolean; message: string }> | null = null, disposing = false
  let wire: {encode:(value:unknown)=>unknown;decode:(value:unknown)=>any}
  const owner = (sender: Electron.WebContents) => !!view && !view.webContents.isDestroyed() && sender === view.webContents
  const send = (message: Wire) => { if (!engine?.connected) throw new Error('投标引擎未连接，请返回首页重新进入。'); engine.send(wire.encode(message) as any) }
  const rpc = (channel: string, args: unknown[]) => new Promise<any>((resolveResult, reject) => {
    if (!allowed.has(channel)) { reject(new Error('该投标操作尚未就绪，请稍后重试。')); return }
    const id = ++nextId
    const timer = setTimeout(() => { pending.delete(id); reject(new Error('投标操作暂未响应，请重试。')) }, 300_000)
    pending.set(id, { resolve: resolveResult, reject, timer })
    try { send({ version: 1, kind: 'invoke', id, channel, args }) } catch (error) { clearTimeout(timer); pending.delete(id); reject(error) }
  })
  const registry = (message: Wire) => {
    allowed = new Set((message.invoke || []).filter((value: unknown) => typeof value === 'string'))
    allowedEvents = new Set((message.events || []).filter((value: unknown) => typeof value === 'string'))
    for (const channel of allowed) {
      if (installed.has(channel)) continue
      ipcMain.handle(channel, (event, ...args) => { if (!owner(event.sender)) throw new Error('此操作仅供平台投标页面使用。'); return rpc(channel, args) })
      installed.add(channel)
    }
    for (const channel of allowedEvents) {
      if (installedEvents.has(channel)) continue
      ipcMain.on(channel, (event, ...args) => { if (owner(event.sender) && allowedEvents.has(channel)) send({ version: 1, kind: 'send', channel, args }) })
      installedEvents.add(channel)
    }
  }
  const notifyFailure = (message: string) => {
    if (parent && !parent.isDestroyed()) parent.webContents.send('bid-framework-failed', message)
  }
  const dispose = async () => {
    if (disposing) return
    disposing = true
    const oldView = view, oldEngine = engine, host = parent
    view = null; engine = null; opening = null; allowed.clear(); allowedEvents.clear()
    if (oldView) { host?.contentView.removeChildView(oldView); if (!oldView.webContents.isDestroyed()) oldView.webContents.close() }
    for (const value of pending.values()) { clearTimeout(value.timer); value.reject(new Error('投标模块已离开。')) } pending.clear()
    for (const resolveGuard of guards.values()) resolveGuard(false); guards.clear()
    if (oldEngine && oldEngine.exitCode === null && oldEngine.signalCode === null) {
      await new Promise<void>(resolveDone => {
        const timer = setTimeout(() => { oldEngine.kill(); resolveDone() }, 8000)
        oldEngine.once('exit', () => { clearTimeout(timer); resolveDone() })
        if (oldEngine.connected) oldEngine.send(wire.encode({ version: 1, kind: 'shutdown' }) as any)
        else { oldEngine.kill(); clearTimeout(timer); resolveDone() }
      })
    }
    disposing = false
  }
  const requestClose = async () => {
    if (!view || view.webContents.isDestroyed()) return true
    const id = ++nextId
    return await new Promise<boolean>(resolveResult => {
      const timer = setTimeout(() => { guards.delete(id); resolveResult(false) }, 30_000)
      guards.set(id, value => { clearTimeout(timer); guards.delete(id); resolveResult(value) })
      view!.webContents.send('engispace:close-request', id)
    })
  }
  ipcMain.on('engispace:close-response', (event, id: number, value: boolean) => { if (owner(event.sender)) guards.get(id)?.(value === true) })
  ipcMain.handle('engispace:return-home', event => {
    if (!owner(event.sender)) throw new Error('只能从投标页面返回平台。')
    setTimeout(() => { void dispose().then(() => { if (parent && !parent.isDestroyed()) parent.webContents.send('bid-framework-closed') }) }, 50)
    return { success: true }
  })
  ipcMain.handle('bid-framework-open', async event => {
    if (disposing) return { success: false, message: '投标模块正在完成退出，请稍后重新进入。' }
    if (view && !view.webContents.isDestroyed()) { view.webContents.focus(); return { success: true, message: '已进入投标模块。' } }
    if (opening) return opening
    parent = BrowserWindow.fromWebContents(event.sender)
    if (!parent) throw new Error('未找到平台窗口。')
    opening = (async () => {
      try {
        mkdirSync(data, { recursive: true })
        const bundled = join(process.resourcesPath, 'bidding-framework')
        const root = app.isPackaged && existsSync(bundled) ? bundled : resolve(appRoot, '../bidding-framework/client')
        const packaged = existsSync(join(root, 'EngiSpace-Bidding.exe'))
        const source = packaged ? join(root, 'resources/app.asar') : root
        const command = packaged ? join(root, 'EngiSpace-Bidding.exe') : join(root, 'node_modules/electron/dist/electron.exe')
        if (!existsSync(command) || !existsSync(source)) throw new Error('缺少完整投标引擎，请使用完整平台试用包。')
        const env = { ...process.env, ENGISPACE_BIDDING_DATA_ROOT: data } as NodeJS.ProcessEnv
        delete env.ELECTRON_RUN_AS_NODE; delete env.ELECTRON_RENDERER_URL; delete env.DSS_FRAMEWORK_HIDE
        wire = createRequire(import.meta.url)(join(source,'electron/services/engispaceWire.cjs'))
        const child = spawn(command, packaged ? ['--engispace-backend'] : [root, '--engispace-backend'], { cwd: root, env, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe', 'ipc'], serialization: 'json' })
        engine = child; child.stdout?.resume(); child.stderr?.resume()
        await new Promise<void>((resolveReady, reject) => {
          const timer = setTimeout(() => reject(new Error('投标引擎启动超时，平台保留，请重新进入。')), 30_000)
          child.once('error', error => { clearTimeout(timer); reject(error) })
          child.once('exit', () => { clearTimeout(timer); reject(new Error('投标引擎启动未完成，请关闭旧投标窗口后重试。')) })
          child.on('message', async raw => {
            let message:Wire;try{message=wire.decode(raw)}catch{return;}
            if (message?.version !== 1) return
            if (message.kind === 'ready') { registry(message); clearTimeout(timer); resolveReady(); return }
            if (message.kind === 'registry') { registry(message); return }
            if (message.kind === 'result') { const value = pending.get(message.id); if (value) { clearTimeout(value.timer); pending.delete(message.id); if (message.error) value.reject(new Error(message.error.message)); else value.resolve(message.value) }; return }
            if (message.kind === 'event') { if (view && !view.webContents.isDestroyed()) view.webContents.send(message.channel, ...(message.args || [])); return }
            if (message.kind === 'fatal') { clearTimeout(timer); reject(new Error(message.message)); return }
            if (message.kind === 'native-dialog') {
              try {
                if (!parent || parent.isDestroyed()) throw new Error('平台窗口已关闭。')
                let value: unknown
                if (message.method === 'showOpenDialog') value = await dialog.showOpenDialog(parent, message.options)
                else if (message.method === 'showSaveDialog') value = await dialog.showSaveDialog(parent, message.options)
                else if (message.method === 'showMessageBox') value = await dialog.showMessageBox(parent, message.options)
                else throw new Error('不支持的原生对话框。')
                if (child.connected) child.send(wire.encode({ version: 1, kind: 'native-result', id: message.id, value }) as any)
              } catch (error) { if (child.connected) child.send(wire.encode({ version: 1, kind: 'native-result', id: message.id, error: error instanceof Error ? error.message : String(error) }) as any) }
            }
          })
        })
        child.on('exit', () => { if (engine === child && !disposing) { notifyFailure('投标引擎已断开，原稿保留，请重新进入。'); void dispose() } })
        const partition = session.fromPartition('persist:engispace-bidding')
        const { isUpstreamService, recordLocalRequest } = createRequire(import.meta.url)(join(source, 'electron/services/engispaceLocalAnalytics.cjs'))
        partition.webRequest.onBeforeRequest({ urls: ['http://*/*', 'https://*/*'] }, (details, callback) => {
          if (isUpstreamService(details.url)) {
            const bytes = (details.uploadData || []).filter(item => item.bytes).map(item => item.bytes!)
            recordLocalRequest(data, details.url, bytes.length ? Buffer.concat(bytes).toString('utf8') : undefined)
            callback({ cancel: true })
          } else callback({})
        })
        if (!await partition.protocol.isProtocolHandled('yibiao-asset')) partition.protocol.handle('yibiao-asset', async request => {
          const origin = request.headers.get('Origin')
          const headers = { 'Access-Control-Allow-Origin': origin || 'null' }
          if (origin && !['null', 'file://'].includes(origin)) return new Response('Forbidden', { status: 403 })
          const url = new URL(request.url)
          if (!['credential-library', 'generated-images', 'imported-images'].includes(url.hostname)) return new Response('Not found', { status: 404, headers })
          const base = resolve(data, 'workspace', url.hostname), file = resolve(base, decodeURIComponent(url.pathname.replace(/^\/+/, '')))
          if (!file.startsWith(base + sep)) return new Response('Forbidden', { status: 403, headers })
          if (!existsSync(file)) return new Response('Not found', { status: 404, headers })
          const response = await net.fetch(pathToFileURL(file).href)
          const responseHeaders = new Headers(response.headers);responseHeaders.set('Access-Control-Allow-Origin', origin || 'null')
          return new Response(response.body, { status: response.status, headers: responseHeaders })
        })
        const panel = new WebContentsView({ webPreferences: { preload: join(source, 'electron/preload.cjs'), contextIsolation: true, nodeIntegration: false, sandbox: false, backgroundThrottling: false, session: partition, additionalArguments: ['--engispace-embedded'] } })
        view = panel; parent!.contentView.addChildView(panel)
        const resize = () => { if (view === panel && parent && !parent.isDestroyed()) { const [width, height] = parent.getContentSize(); panel.setBounds({ x: 0, y: 0, width, height }) } }
        resize(); parent!.on('resize', resize); panel.webContents.once('destroyed', () => parent?.removeListener('resize', resize))
        panel.webContents.setWindowOpenHandler(() => ({ action: 'deny' }))
        panel.webContents.on('will-navigate', navigation => navigation.preventDefault())
        await panel.webContents.loadFile(join(source, 'dist/index.html'))
        panel.webContents.focus()
        return { success: true, message: '已进入平台内投标模块。' }
      } catch (error) { await dispose(); throw error }
      finally { opening = null }
    })()
    return opening
  })
  return { prepareClose: requestClose, dispose, isActive: () => !!view && !view.webContents.isDestroyed() }
}
