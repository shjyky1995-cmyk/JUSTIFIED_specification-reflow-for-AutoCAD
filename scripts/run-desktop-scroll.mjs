import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'
const root = dirname(dirname(fileURLToPath(import.meta.url)))
const env = { ...process.env }
delete env.ELECTRON_RUN_AS_NODE
const result = spawnSync(join(root, 'desktop/app/node_modules/electron/dist/electron.exe'), [join(root, 'scripts/test-desktop-scroll.cjs')], { env, cwd: root, encoding: 'utf8', windowsHide: true, timeout: 30_000 })
if (result.stdout) process.stdout.write(result.stdout)
if (result.stderr) process.stderr.write(result.stderr)
if (result.error) console.error(result.error.message)
if (!result.stdout?.includes('DESKTOP_SCROLL_OK')) process.exitCode = 1
else process.exitCode = result.status ?? 1
