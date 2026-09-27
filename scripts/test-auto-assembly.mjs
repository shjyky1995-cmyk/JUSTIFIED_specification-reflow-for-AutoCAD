// 只读取本机私有包，测试脚本与输出不包含旧工程正文。
import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { existsSync, readFileSync } from 'node:fs'
import { unlink } from 'node:fs/promises'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { assembleNote } from '../desktop/app/src/shared/assembly.ts'
import { findProjectCatalogPath } from '../desktop/app/src/shared/catalog-path.ts'
import { parseContentCatalog } from '../desktop/app/src/shared/content.ts'
import { buildDocument, createNote, findIssues, parseNote, toExportRequest } from '../desktop/app/src/shared/model.ts'
import { createStore } from '../desktop/app/src/shared/store.ts'

const path = process.env.DSS_CONTENT_LIBRARY_PATH
if (!path) throw new Error('请设置 DSS_CONTENT_LIBRARY_PATH 指向 G 盘项目 content-library/private/catalog.json')
const catalog = parseContentCatalog(JSON.parse(readFileSync(path, 'utf8')))
const repoRoot = resolve(fileURLToPath(new URL('..', import.meta.url)))
assert.equal(findProjectCatalogPath(join(repoRoot, 'desktop', 'app')), resolve(path))
const externalStoreRoot = join(dirname(path), `no-copy-${process.pid}`)
const externalStore = createStore(externalStoreRoot, path)
assert.equal(externalStore.loadCatalog()?.packageId, catalog.packageId)
assert.ok(!existsSync(join(externalStoreRoot, 'content-library', 'catalog.json')))
const testDocx = join(dirname(path), `auto-assembly-${process.pid}.docx`)

function callWorker(request) {
  const root = resolve(fileURLToPath(new URL('..', import.meta.url)))
  const worker = join(root, 'desktop', 'worker', 'bin', 'Debug', 'net10.0', 'DocxWorkbench.Worker.dll')
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
const cases = [
  ['tpl-struct-pool', 'structural', '结构设计说明（构筑物）.docx'],
  ['tpl-struct-frame', 'structural', '结构设计说明（框架）.docx'],
  ['tpl-struct-pool-frame', 'structural', '结构设计说明（构筑物加框架）.docx'],
  ['tpl-arch-standard', 'architecture', '建筑设计说明厂房.docx'],
  ['tpl-plumb-standard', 'plumbing', '工艺设计说明总图.docx'],
  ['tpl-elec-standard', 'electrical', '电气设计说明总图.docx'],
  ['tpl-hvac-standard', 'hvac', '暖通设计说明.docx'],
]

for (const [templateId, discipline, sourceFile] of cases) {
  const original = createNote(discipline, templateId, { name: '脱敏测试工程', number: '', owner: '', location: '' })
  const result = assembleNote(original, templateId, catalog)
  const note = result.note
  const source = catalog.sourceDigest.find(item => item.file === sourceFile)
  assert.equal(result.sourceFile, sourceFile)
  assert.ok(result.selectedClauses > 0)
  assert.ok(result.excludedClauses > 0)
  assert.ok(note.sections.length > 0)
  assert.equal(note.assemblyPackageId, catalog.packageId)
  assert.ok(note.sections.every(section => section.id.startsWith('lib-CH')))
  const modules = note.sections.flatMap(section => section.modules ?? [])
  assert.equal(modules.length, result.selectedClauses)
  assert.ok(modules.every(module => module.sourceRefs.some(ref => ref.sourceId === source.id)))
  assert.ok(modules.every(module => !module.flags.some(flag => ['condition', 'table', 'possible_project_literal', 'source_note', 'suspect_reference'].includes(flag))))
  assert.ok(modules.every(module => !/山东省|新疆|乌苏|济南|腊山河|尉犁/.test(module.template)))
  assert.ok(findIssues(note).some(issue => issue.message.includes('整篇说明')))
  assert.ok(buildDocument(note).blocks.some(block => block.text.includes('旧工程候选初稿')))
  const reopened = parseNote(JSON.parse(JSON.stringify(note)))
  assert.equal(reopened.sections.flatMap(section => section.modules ?? []).length, modules.length)
  console.log(`${templateId}: ${modules.length} 条，${note.sections.length} 章，排除 ${result.excludedClauses} 条`)
}

const pool = assembleNote(createNote('structural', 'tpl-struct-pool', { name: '脱敏测试工程', number: '', owner: '', location: '' }), 'tpl-struct-pool', catalog).note
assert.ok(!pool.sections.flatMap(section => section.modules ?? []).some(module => module.clauseId === 'C0079'))
const frame = assembleNote(createNote('structural', 'tpl-struct-frame', { name: '脱敏测试工程', number: '', owner: '', location: '' }), 'tpl-struct-frame', catalog).note
assert.ok(!frame.sections.flatMap(section => section.modules ?? []).some(module => ['C0014', 'C0141', 'C0142'].includes(module.clauseId)))
pool.structural = { ...pool.structural, siteCategory: 'II', seismicGrade: '乙', safetyLevel: '二级', foundationGrade: '乙级', protectionScheme: '中', seismicIntensity: '7度（0.10g）' }
for (const fieldId of Object.keys(pool.fieldDefinitions)) {
  if (!['project_name', 'project_location', 'structural_safety_level', 'foundation_design_grade', 'seismic_intensity', 'site_class', 'design_life', 'seismic_fortification_category'].includes(fieldId)) pool.fieldValues[fieldId] = '测试值'
}
pool.assemblyReviewConfirmed = true
assert.equal(findIssues(pool).filter(issue => issue.level === 'error').length, 0)
assert.ok(toExportRequest(pool, testDocx).document.sections.length > 0)
if (process.env.DSS_DOTNET_EXE && existsSync(process.env.DSS_DOTNET_EXE)) {
  try {
    const generated = await callWorker(toExportRequest(pool, testDocx))
    assert.equal(generated.success, true, JSON.stringify(generated))
    const inspected = await callWorker({ operation: 'inspect', path: testDocx })
    assert.equal(inspected.success, true, JSON.stringify(inspected))
    assert.ok(inspected.headings.includes('设计依据'))
    console.log('AUTO_DOCX_OK')
  } finally {
    if (existsSync(testDocx)) await unlink(testDocx)
  }
}
console.log('AUTO_ASSEMBLY_OK')
