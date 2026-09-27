// 验证内容库导入、章节条款快照、工程字段、保存恢复与导出阻断。
import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { existsSync } from 'node:fs'
import { mkdtemp, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { availableChapters, clausesFor, parseContentCatalog, selectedModule } from '../desktop/app/src/shared/content.ts'
import { buildDocument, createNote, findIssues, setFieldValue, setModuleTemplate, toExportRequest } from '../desktop/app/src/shared/model.ts'
import { createStore } from '../desktop/app/src/shared/store.ts'

const sandbox = await mkdtemp(join(tmpdir(), 'dss-content-'))
const root = resolve(fileURLToPath(new URL('..', import.meta.url)))
const worker = join(root, 'desktop', 'worker', 'bin', 'Debug', 'net10.0', 'DocxWorkbench.Worker.dll')

function callWorker(request) {
  return new Promise((resolveResult, reject) => {
    const child = spawn(process.env.DSS_DOTNET_EXE, [worker], { stdio: ['pipe', 'pipe', 'pipe'], windowsHide: true })
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
try {
  const sample = {
    schemaVersion: 1, packageId: 'sample-1', generatedAt: '', title: '脱敏测试包', reviewStatus: 'pending',
    chapters: [{ id: 'CH01', title: '工程概况' }],
    fields: [{ id: 'capacity', label: '处理规模', unit: 'm³/d', scope: 'project_or_note', reviewStatus: 'pending', aliases: [] }],
    clauses: [{ id: 'C0001', chapterId: 'CH01', disciplines: ['process'], kind: '单项目事实', template: '设计规模为{capacity}。', fieldIds: ['capacity'], refs: [], sources: [{ sourceId: 'S001', para: 2 }], note: '', reviewStatus: 'pending', flags: [], usableAsText: true }],
    sourceDigest: [{ id: 'S001', file: '脱敏样本.docx', sha256: '0'.repeat(64), discipline: '工艺', versionRelation: '', rightsStatus: '待核定' }],
  }
  const path = join(sandbox, 'sample.json')
  await writeFile(path, JSON.stringify(sample), 'utf8')
  const store = createStore(join(sandbox, 'data'))
  const catalog = store.importCatalog(path)
  assert.deepEqual(store.loadCatalog(), catalog)
  assert.equal(availableChapters(catalog, 'plumbing').length, 1, '工艺候选应在给排水面板出现')
  assert.equal(availableChapters(catalog, 'structural').length, 0, '工艺候选不能混入结构')
  const clause = clausesFor(catalog, 'plumbing', 'CH01')[0]
  const note = createNote('plumbing', 'tpl-plumb-standard', { name: '测试工程', number: '', owner: '', location: '' })
  note.sections = [{ id: 'lib-CH01', title: '工程概况', body: '', custom: false, modules: [selectedModule(clause, catalog)] }]
  note.fieldDefinitions = { capacity: { label: '处理规模', unit: 'm³/d' } }
  assert.ok(findIssues(note).some(issue => issue.message.includes('尚未确认')))
  assert.ok(findIssues(note).some(issue => issue.message.includes('处理规模')))
  assert.throws(() => toExportRequest(note, join(sandbox, 'draft.docx')))
  note.sections[0].modules[0].confirmedForNote = true
  note.fieldValues.capacity = '80000 m³/d'
  assert.equal(findIssues(note).filter(issue => issue.level === 'error').length, 0)
  assert.ok(buildDocument(note).blocks.some(block => block.text === '设计规模为80000 m³/d。'))
  assert.equal(store.saveNote(note).ok, true)
  const reopened = store.loadNote(note.id)
  assert.equal(reopened.sections[0].modules[0].template, clause.template)
  assert.equal(reopened.fieldValues.capacity, '80000 m³/d')
  assert.equal(toExportRequest(reopened, join(sandbox, 'final.docx')).document.sections[0].body, '设计规模为80000 m³/d。')
  const valueChanged = setFieldValue(reopened, 'capacity', '90000 m³/d')
  assert.equal(valueChanged.sections[0].modules[0].confirmedForNote, false, '更改工程取值后须重新确认条款')
  const textChanged = setModuleTemplate(reopened, 'lib-CH01', 'C0001', '本工程规模为{capacity}。')
  assert.equal(textChanged.sections[0].modules[0].edited, true)
  assert.equal(textChanged.sections[0].modules[0].confirmedForNote, false, '改写条款后须重新确认')
  const unknownField = setModuleTemplate(reopened, 'lib-CH01', 'C0001', '规模为{unknown_field}。')
  assert.ok(findIssues(unknownField).some(issue => issue.message.includes('未定义的占位符')))
  if (process.env.DSS_DOTNET_EXE && existsSync(worker)) {
    const docxPath = join(sandbox, 'final.docx')
    const generated = await callWorker(toExportRequest(reopened, docxPath))
    assert.equal(generated.success, true, JSON.stringify(generated))
    const inspected = await callWorker({ operation: 'inspect', path: docxPath })
    assert.equal(inspected.success, true, JSON.stringify(inspected))
    assert.ok(inspected.headings.includes('工程概况'))
  }
  const broken = structuredClone(sample)
  broken.clauses[0].fieldIds = ['missing']
  assert.throws(() => parseContentCatalog(broken), /字段不存在/)

  if (process.argv[2]) {
    const full = store.importCatalog(process.argv[2])
    assert.equal(full.chapters.length, 25)
    assert.equal(full.clauses.length, 1405)
    assert.equal(full.fields.length, 368)
    assert.ok(full.clauses.every(item => item.reviewStatus === 'pending'))
    assert.ok(full.clauses.filter(item => !item.usableAsText).length >= 32)
    console.log(`CONTENT_CATALOG_OK chapters=${full.chapters.length} clauses=${full.clauses.length} fields=${full.fields.length}`)
  } else {
    console.log('CONTENT_CATALOG_OK sample')
  }
} finally {
  await rm(sandbox, { recursive: true, force: true })
}
