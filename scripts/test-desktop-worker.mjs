import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = resolve(fileURLToPath(new URL('..', import.meta.url)))
const dotnet = process.env.DSS_DOTNET_EXE || join(root, 'artifacts', 'dotnet10', 'dotnet.exe')
const worker = join(root, 'desktop', 'worker', 'bin', 'Debug', 'net10.0', 'DocxWorkbench.Worker.dll')

function call(request) {
  return new Promise((resolveResult, reject) => {
    const child = spawn(dotnet, [worker], { stdio: ['pipe', 'pipe', 'pipe'], windowsHide: true })
    let output = ''
    let error = ''
    child.stdout.setEncoding('utf8').on('data', chunk => { output += chunk })
    child.stderr.setEncoding('utf8').on('data', chunk => { error += chunk })
    child.on('error', reject)
    child.on('close', () => {
      try { resolveResult(JSON.parse(output)) }
      catch { reject(new Error(error || output || 'worker returned no JSON')) }
    })
    child.stdin.end(JSON.stringify(request))
  })
}

const directory = await mkdtemp(join(tmpdir(), 'dss-worker-'))
try {
  const path = join(directory, '设计说明.docx')
  const draft = {
    operation: 'generate', path, title: '结构设计说明', projectName: '测试工程', discipline: '结构',
    overview: '第一段。\n第二段。', basis: '设计依据。', requirements: '', other: '',
  }
  const created = await call(draft)
  assert.equal(created.success, true, JSON.stringify(created))
  assert.ok(created.path.endsWith('\\设计说明.docx'), JSON.stringify(created))
  assert.ok(created.blocks >= 6, JSON.stringify(created))
  const inspected = await call({ operation: 'inspect', path })
  assert.equal(inspected.success, true, JSON.stringify(inspected))
  assert.equal(inspected.blocks, created.blocks)
  const repeated = await call(draft)
  assert.equal(repeated.success, false, 'existing user file must not be overwritten')
  assert.match(repeated.message, /不会覆盖/)
  const empty = await call({ ...draft, path: join(directory, '空白.docx'), overview: '', basis: '' })
  assert.equal(empty.success, false, 'empty sections must be rejected')
  console.log(`DESKTOP_WORKER_OK blocks=${created.blocks} generated=1 inspected=1 overwriteBlocked=1`)
} finally {
  await rm(directory, { recursive: true, force: true })
}
