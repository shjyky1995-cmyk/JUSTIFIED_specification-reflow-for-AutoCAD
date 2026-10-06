import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { readFileSync, writeFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'
import { createStore } from '../desktop/app/src/shared/store.ts'
import { assembleNote } from '../desktop/app/src/shared/assembly.ts'
import { createNote, parseNote, findIssues, buildDocument, toExportRequest } from '../desktop/app/src/shared/model.ts'
import { parseContentLayout, renderTemplate } from '../desktop/app/src/shared/content.ts'
const catalogPath = process.env.DSS_CONTENT_LIBRARY_PATH
assert.ok(catalogPath, '请设置本机私有资料目录')
const dir = dirname(catalogPath)
const catalog = createStore(join(dir, 'test-source-store'), catalogPath).loadCatalog()
assert.equal(catalog.layouts.length, 7)
assert.equal(renderTemplate('{design_life} 年，{seismic_intensity} 度', { design_life: '50年', seismic_intensity: '7度（0.10g）' }).text, '50 年，7 度')
const reports = []
for (const layout of catalog.layouts) {
  const discipline = layout.templateId.includes('struct') ? 'structural' : layout.templateId.includes('arch') ? 'architecture' : layout.templateId.includes('plumb') ? 'plumbing' : layout.templateId.includes('elec') ? 'electrical' : 'hvac'
  const note = assembleNote(createNote(discipline, layout.templateId, { name: '测试工程', number: '', owner: '', location: '测试地点' }), layout.templateId, catalog).note
  assert.equal(note.title, layout.sourceTitle)
  assert.ok(toExportRequest(note, 'draft.docx').document.reviewNotices.length > 0)
  assert.throws(() => toExportRequest(note, 'test.docx', 'reviewed'))
  const reopened = parseNote(JSON.parse(JSON.stringify(note)))
  assert.deepEqual(reopened.sections, note.sections)
  if (note.structural) Object.assign(note.structural, { siteCategory: 'II', seismicGrade: '乙', safetyLevel: '二级', foundationGrade: '乙级', protectionScheme: '中', seismicIntensity: '7度（0.10g）' })
  for (const field of Object.keys(note.fieldDefinitions)) note.fieldValues[field] = '测试值'
  for (const section of note.sections) {
    for (const module of section.modules ?? []) module.confirmedForNote = true
    for (const block of section.layoutBlocks ?? []) if (block.kind === 'table') block.confirmedForNote = true
  }
  note.assemblyReviewConfirmed = true
  assert.deepEqual(findIssues(note).filter(issue => issue.level === 'error'), [])
  const blocks = buildDocument(note).blocks
  assert.deepEqual(blocks.filter(block => block.kind === 'meta').map(block => block.text), ['资料状态：旧工程候选初稿，规范版本与适用性待核定'])
  assert.deepEqual(blocks.filter(b => b.kind !== 'title' && b.kind !== 'meta').map(b => b.kind), layout.sections.flatMap(section => ['heading', ...section.blocks.map(b => b.kind)]))
  assert.ok(!JSON.stringify(blocks).includes('待填写'))
  const table = note.sections.flatMap(s => s.layoutBlocks ?? []).find(b => b.kind === 'table' && /\{/.test(b.rows.flat().join('')))
  if (table) {
    const id = table.rows.flat().join('').match(/\{([a-z][a-z0-9_]*)\}/)[1]
    const old = note.fieldValues[id]; delete note.fieldValues[id]
    assert.ok(findIssues(note).some(issue => issue.field === id))
    assert.throws(() => toExportRequest(note, 'test.docx', 'reviewed'))
    note.fieldValues[id] = old
  }
  const output = join(dir, `${layout.templateId}-${process.pid}.docx`)
  const request = toExportRequest(note, output)
  const result = spawnSync(process.env.DSS_DOTNET_EXE, [fileURLToPath(new URL('../desktop/worker/bin/Debug/net10.0/DocxWorkbench.Worker.dll', import.meta.url))], { input: JSON.stringify(request), encoding: 'utf8', windowsHide: true })
  assert.equal(result.status, 0, result.stderr)
  const response = JSON.parse(result.stdout)
  assert.equal(response.success, true, result.stdout)
  assert.equal(response.headings.length, layout.sections.length + 1)
  reports.push({ templateId: layout.templateId, output, request })
}
const bad = structuredClone(catalog.layouts[0]); bad.sections.flatMap(s => s.blocks).find(b => b.kind === 'table').columnWidths = [-1]
assert.throws(() => parseContentLayout(bad), /列宽/)
writeFileSync(join(dir, 'source-export-tests.json'), JSON.stringify(reports, null, 2))
console.log(JSON.stringify({ status: 'SOURCE_FIDELITY_OK', templates: reports.length, outputs: reports.map(r => r.output) }))
