// 开发期 .NET 10 SDK 定位：系统 PATH 不可用时，回退到仓库本机 SDK（被忽略的 artifacts/dotnet10）。
// 正式安装包内嵌 worker.exe 后由 Electron 主进程优先使用 resourcesPath，不经过这里。
import { existsSync } from 'node:fs'
import { resolve } from 'node:path'

export function findDotnetExe(appDir: string): string | null {
  const candidates = [
    resolve(appDir, '../../artifacts/dotnet10/sdk/dotnet.exe'),
    resolve(appDir, '../../../../artifacts/dotnet10/sdk/dotnet.exe'),
  ]
  for (const candidate of candidates) {
    if (existsSync(candidate)) return candidate
  }
  return null
}

export function workerDotnetCommand(appDir: string): { command: string; args: string[] } {
  return { command: findDotnetExe(appDir) ?? 'dotnet', args: [] }
}
