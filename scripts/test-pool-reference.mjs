// Exercises the private source-ordered layout without committing source content.
import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { resolve, dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { assembleNote } from '../desktop/app/src/shared/assembly.ts'
import { parseContentCatalog, parseContentLayout } from '../desktop/app/src/shared/content.ts'
import { buildDocument, createNote, findIssues, parseNote, toExportRequest } from '../desktop/app/src/shared/model.ts'
import { createStore } from '../desktop/app/src/shared/store.ts'

const catalogPath = process.env.DSS_CONTENT_LIBRARY_PATH
if (!catalogPath) throw new Error('请设置 DSS_CONTENT_LIBRARY_PATH')
const layoutPath = join(dirname(catalogPath), 'pool-layout.json')
const catalog = parseContentCatalog(JSON.parse(readFileSync(catalogPath, 'utf8')))
catalog.layouts = [parseContentLayout(JSON.parse(readFileSync(layoutPath, 'utf8')))]
assert.equal(createStore(join(dirname(catalogPath), 'test-store'), catalogPath).loadCatalog()?.layouts?.[0].templateId, 'tpl-struct-pool')
const original = createNote('structural', 'tpl-struct-pool', { name: '脱敏测试工程', number: '', owner: '', location: '' })
const note = assembleNote(original, 'tpl-struct-pool', catalog).note
assert.deepEqual(note.sections.map(section => section.title), catalog.layouts[0].sections.map(section => section.title))
assert.equal(note.sections.length, 8)
assert.equal(note.sections.flatMap(section => section.layoutBlocks ?? []).filter(block => block.kind === 'table').length, 7)
assert.equal(note.sections.flatMap(section => section.modules ?? []).length, 158)
assert.equal(note.sections.flatMap(section => section.modules ?? []).filter(module => module.flags.includes('requires_rewrite')).length, 2)
assert.equal(note.sections.flatMap(section => section.modules ?? []).filter(module => module.flags.includes('requires_applicability_review')).length, 9)
assert.ok(findIssues(note).some(issue => issue.message.includes('旧工程事实')))
assert.ok(findIssues(note).some(issue => issue.message.includes('表格仍有本工程取值')))
assert.ok(findIssues(note).some(issue => issue.message.includes('适用条件尚未确认')))
const document = buildDocument(note)
assert.equal(document.blocks.filter(block => block.kind === 'table').length, 7)
assert.deepEqual(document.blocks.filter(block => block.kind === 'heading').map(block => block.text), note.sections.map(section => section.title))
assert.ok(!JSON.stringify(note).match(/乌苏|塔城|济南|尉犁|腊山河|XXXX/))
const reopened = parseNote(JSON.parse(JSON.stringify(note)))
assert.equal(reopened.sections.flatMap(section => section.layoutBlocks ?? []).length, 165)

// Fill a synthetic copy only to verify the complete DOCX path. These are test
// values and the file remains in the ignored private directory.
const testNote = reopened
testNote.structural = { ...testNote.structural, siteCategory: 'II', seismicGrade: '乙', safetyLevel: '二级', foundationGrade: '乙级', protectionScheme: '中', seismicIntensity: '7度（0.10g）' }
for (const fieldId of Object.keys(testNote.fieldDefinitions)) if (!['project_name', 'project_location', 'structural_safety_level', 'foundation_design_grade', 'seismic_intensity', 'site_class', 'design_life', 'seismic_fortification_category'].includes(fieldId)) testNote.fieldValues[fieldId] = '测试待核定'
for (const section of testNote.sections) {
  for (const module of section.modules ?? []) {
    if (module.flags.includes('requires_rewrite')) { module.template = '【测试：该段已由设计人员改写】'; module.edited = true }
    if (module.flags.includes('requires_applicability_review')) module.confirmedForNote = true
  }
  for (const block of section.layoutBlocks ?? []) if (block.kind === 'table') { block.rows = block.rows.map(row => row.map(cell => cell === '【待核定：本工程取值】' ? '测试待核定' : cell)); block.confirmedForNote = true }
}
testNote.assemblyReviewConfirmed = true
assert.deepEqual(findIssues(testNote).filter(issue => issue.level === 'error'), [])
const output = join(dirname(catalogPath), `pool-reference-test-${process.pid}.docx`)
const root = resolve(fileURLToPath(new URL('..', import.meta.url)))
const worker = join(root, 'desktop', 'worker', 'bin', 'Debug', 'net10.0', 'DocxWorkbench.Worker.dll')
function callWorker(request) {
  return new Promise((resolveResult, reject) => {
    const child = spawn(process.env.DSS_DOTNET_EXE, [worker], { stdio: ['pipe', 'pipe', 'pipe'], windowsHide: true })
    let outputText = ''
    let errorText = ''
    child.stdout.setEncoding('utf8').on('data', chunk => { outputText += chunk })
    child.stderr.setEncoding('utf8').on('data', chunk => { errorText += chunk })
    child.on('error', reject)
    child.on('close', () => { try { resolveResult(JSON.parse(outputText)) } catch { reject(new Error(errorText || outputText)) } })
    child.stdin.end(JSON.stringify(request))
  })
}
const result = await callWorker(toExportRequest(testNote, output))
assert.equal(result.success, true, JSON.stringify(result))
assert.equal(result.headings.length, 9)
assert.ok(result.diagnostics.some(item => item.code === 'W_TABLE_CAD_UNSUPPORTED'))
const inspected = await callWorker({ operation: 'inspect', path: output })
assert.equal(inspected.success, false, '含表格文档不得被现有 CAD 导入检查误报为通过')
assert.ok(inspected.message.includes('需要修正'), JSON.stringify(inspected))
console.log(JSON.stringify({ status: 'POOL_REFERENCE_OK', chapters: 8, paragraphs: 158, tables: 7, output }))
