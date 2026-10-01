import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { existsSync } from 'node:fs'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  buildDocument,
  DISCIPLINES,
  templatesFor,
  toExportRequest,
} from '../desktop/app/src/shared/model.ts'

const root = resolve(fileURLToPath(new URL('..', import.meta.url)))
const dotnetCandidates = [
  process.env.DSS_DOTNET_EXE,
  join(root, 'artifacts', 'dotnet10', 'sdk', 'dotnet.exe'),
  resolve(root, '..', '..', 'artifacts', 'dotnet10', 'sdk', 'dotnet.exe'),
].filter(Boolean)
const dotnet = dotnetCandidates.find(candidate => existsSync(candidate)) ?? 'dotnet'
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

function documentHeadings(note) {
  return buildDocument(note).blocks
    .filter(block => block.kind === 'title' || block.kind === 'heading')
    .map(block => block.text)
}

function blockCount(note) {
  return buildDocument(note).blocks.length
}

function makeNote(discipline, index) {
  const template = templatesFor(discipline).find(item => !item.custom)
  assert.ok(template, `缺少 ${discipline} 预设模板`)
  const project = { name: `测试工程${index}`, number: `TS-${index}`, owner: '建设单位', location: '测试地点' }
  const note = {
    id: `note-${discipline}-${index}`,
    title: `${discipline}设计说明`,
    discipline,
    project,
    structural: discipline === 'structural'
      ? {
          siteCategory: 'II',
          seismicGrade: '乙',
          safetyLevel: '二级',
          foundationGrade: '乙级',
          designLifeYears: 50,
          corrosion: '地下水位以下土对混凝土结构具微腐蚀性。',
          protectionScheme: '中',
          protectionExtra: '预留防腐厚度',
          seismicIntensity: '7度（0.15g）',
        }
      : null,
    templateId: template.id,
    templateVersion: '1.0.0',
    sections: template.sectionIds.map((id, order) => ({ id, title: `章节${order}`, body: `第${order}段正文。\n第二行。`, custom: false })),
    createdAt: '2026-09-27T00:00:00.000Z',
    updatedAt: '2026-09-27T00:00:00.000Z',
  }
  return note
}

const directory = await mkdtemp(join(tmpdir(), 'dss-worker-'))
try {
  let generated = 0
  let inspected = 0
  for (const [index, discipline] of DISCIPLINES.entries()) {
    const note = makeNote(discipline.code, index)
    const document = buildDocument(note)
    const expectedHeadings = documentHeadings(note)
    const path = join(directory, `${discipline.label}设计说明.docx`)
    const created = await call(toExportRequest(note, path))
    assert.equal(created.success, true, `${discipline.label}: ${JSON.stringify(created)}`)
    assert.equal(created.path.split(/[\\/]/).pop(), path.split(/[\\/]/).pop(), `${discipline.label} 导出路径`)
    assert.equal(created.blocks, blockCount(note), `${discipline.label} 读回块数不符: ${JSON.stringify(created)}`)
    assert.deepEqual(created.headings, expectedHeadings, `${discipline.label} 标题轮廓不符: ${JSON.stringify(created)}`)
    if (discipline.code === 'structural') {
      assert.ok(created.headings.includes('结构设计参数'), '结构专业应包含结构设计参数标题')
      assert.ok(document.blocks.some(block => block.text.includes('设防烈度：7度（0.15g）')), '烈度应为人工选择值')
    } else {
      assert.ok(!created.headings.includes('结构设计参数'), `${discipline.label} 不应包含结构参数标题`)
    }
    generated += 1

    const readback = await call({ operation: 'inspect', path })
    assert.equal(readback.success, true, `${discipline.label} 读回失败: ${JSON.stringify(readback)}`)
    assert.equal(readback.blocks, created.blocks, `${discipline.label} 读回块数不一致`)
    inspected += 1

    const repeated = await call(toExportRequest(note, path))
    assert.equal(repeated.success, false, 'existing user file must not be overwritten')
    assert.match(repeated.message, /不会覆盖/)

    if (discipline.code === 'structural') {
      const missing = structuredClone(note)
      missing.structural.siteCategory = ''
      const draft = await call(toExportRequest(missing, join(directory, '缺参数.docx')))
      assert.equal(draft.success, true, '缺少结构参数仍可导出草稿')
      assert.throws(() => toExportRequest(missing, 'strict.docx', 'reviewed'), /场地类别/)
      const noIntensity = structuredClone(note)
      noIntensity.structural.seismicIntensity = '待核定'
      const intensityDraft = await call(toExportRequest(noIntensity, join(directory, '缺烈度.docx')))
      assert.equal(intensityDraft.success, true, '未选择抗震设防烈度仍可导出草稿')
      assert.throws(() => toExportRequest(noIntensity, 'strict.docx', 'reviewed'), /抗震设防烈度/)
    }
  }

  const emptyNote = makeNote('architecture', 99)
  emptyNote.sections = emptyNote.sections.map(section => ({ ...section, body: '   ' }))
  assert.throws(() => toExportRequest(emptyNote, join(directory, '空白.docx')), /至少填写/)

  const noTitle = makeNote('electrical', 98)
  noTitle.title = '  '
  const noTitleResult = await call(toExportRequest(noTitle, join(directory, '无标题.docx')))
  assert.equal(noTitleResult.success, true, '草稿空标题使用专业默认标题')
  assert.throws(() => toExportRequest(noTitle, 'strict.docx', 'reviewed'), /说明标题/)

  console.log(`DESKTOP_WORKER_OK disciplines=${generated} generated=${generated} inspected=${inspected} overwriteBlocked=${generated} structuralBlocked=1 intensityBlocked=1 emptyBlocked=1 titleBlocked=1`)
} finally {
  await rm(directory, { recursive: true, force: true })
}
