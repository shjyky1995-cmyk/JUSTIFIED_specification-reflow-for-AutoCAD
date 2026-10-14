// 候选包必要开发检查：缺项真实导出、参数别名、备份恢复；不替代界面与 Word/WPS 人工验收。
import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { resolve, join } from 'node:path'
import { createNote, toExportRequest, effectiveFieldValues, buildDocument } from '../desktop/app/src/shared/model.ts'
import { projectFields, projectFieldValues } from '../desktop/app/src/shared/project-fields.ts'
import { createStore } from '../desktop/app/src/shared/store.ts'
import { assembleNote } from '../desktop/app/src/shared/assembly.ts'

const output = resolve(process.env.DSS_CHECK_DIRECTORY || `artifacts/desktop-candidate-check-${Date.now()}`)
mkdirSync(output, { recursive: true })
const worker = resolve(process.env.DSS_WORKER_EXE || 'artifacts/desktop-worker-release/DocxWorkbench.Worker.exe')
function generate(note, name) {
  const request = toExportRequest(note, join(output, name + '.docx'))
  const response = spawnSync(worker, [], { input: JSON.stringify(request), encoding: 'utf8', windowsHide: true, timeout: 30_000 })
  assert.equal(response.error, undefined)
  const result = JSON.parse(response.stdout)
  assert.equal(result.success, true, JSON.stringify(result))
  return { request, result }
}

const note = createNote('structural', 'structural-custom', { name: '', number: '', owner: '', location: '' }, '')
note.structural.siteCategory = ''
note.structural.seismicIntensity = '待核定'
note.fieldDefinitions = { input_value: { label: '基础持力层', unit: '' } }
note.sections = [{ id: 'custom-1', title: '工程资料', custom: true, body: '补充正文：可在 Word 修改。', modules: [{ id: 'p1', template: '持力层为{input_value}。', baseTemplate: '', fieldIds: ['input_value'], sourceRefs: [], refs: [], flags: [], reviewStatus: 'pending', confirmedForNote: false }], layoutBlocks: [{ kind: 'paragraph', moduleId: 'p1' }, { kind: 'table', rows: [['项目', '取值'], ['持力层', '{input_value}']], sourcePara: 1, reviewNote: '待核定' }] }]
assert.throws(() => toExportRequest(note, 'reviewed.docx', 'reviewed'))
const draft = generate(note, '缺项含表格草稿')
assert.ok(draft.request.document.reviewNotices.some(value => value.includes('待完善')))
assert.ok(draft.request.document.sections[0].body.includes('【待填写：基础持力层】'))
const empty = { ...note, sections: [] }
assert.throws(() => toExportRequest(empty, 'empty.docx'), /至少填写/)

const legacy = structuredClone(note)
legacy.fieldDefinitions = { src_a_groundwater_effect: { label: '本工程地下水影响结论', unit: '' }, src_b_groundwater_effect: { label: '本工程地下水影响结论', unit: '' } }
legacy.fieldValues = { src_a_groundwater_effect: '脱敏地勘结论' }
assert.equal(projectFields(legacy).filter(field => field.label === '地下水对基础的影响结论').length, 1)
assert.equal(projectFieldValues(legacy).src_b_groundwater_effect, '脱敏地勘结论')
legacy.fieldValues.src_b_groundwater_effect = '另一结论'
assert.equal(projectFieldValues(legacy).src_b_groundwater_effect, '另一结论')
assert.equal(projectFieldValues(legacy).src_a_groundwater_effect, '脱敏地勘结论')
assert.equal(effectiveFieldValues(legacy).src_b_groundwater_effect, '另一结论')

const store = createStore(join(output, 'data'))
assert.equal(store.saveNote(note).ok, true)
const edited = { ...note, title: '已编辑标题' }
assert.equal(store.saveNote(edited).ok, true)
const copy = store.duplicateNote(note.id)
assert.notEqual(copy.id, note.id)
const backup = store.backup()
assert.equal(store.restoreBackup(backup), 2)
assert.equal(store.listNotes().length, 4)
writeFileSync(join(store.root, 'notes', note.id + '.json'), '{damaged', 'utf8')
const recovered = store.loadNote(note.id)
assert.ok(recovered.recoveryMessage)
assert.equal(recovered.title, note.title)
assert.equal(store.saveNote(recovered).ok, true)
assert.throws(() => store.restoreBackup('{"kind":"other"}'), /备份/)
assert.equal(store.listNotes().length, 4)

const reports = [{ name: '缺项含表格草稿', blocks: draft.result.blocks }]
if (process.env.DSS_CONTENT_LIBRARY_PATH) {
  const catalog = createStore(join(output, 'catalog-data'), process.env.DSS_CONTENT_LIBRARY_PATH).loadCatalog()
  for (const layout of catalog.layouts) {
    const discipline = layout.templateId.startsWith('tpl-struct') ? 'structural' : layout.templateId.startsWith('tpl-arch') ? 'architecture' : layout.templateId.startsWith('tpl-plumb') ? 'plumbing' : layout.templateId.startsWith('tpl-elec') ? 'electrical' : 'hvac'
    const generated = assembleNote(createNote(discipline, layout.templateId, { name: '', number: '', owner: '', location: '' }), layout.templateId, catalog).note
    assert.ok(!projectFields(generated).some(field => field.label === '本工程对应名称或地区'))
    const { request, result } = generate(generated, layout.templateId + '-缺项')
    assert.ok(request.document.reviewNotices.length > 0)
    assert.equal(result.blocks, buildDocument(generated).blocks.length)
    reports.push({ name: layout.templateId, blocks: result.blocks })
  }
}
writeFileSync(join(output, 'report.json'), JSON.stringify({ status: 'DESKTOP_CANDIDATE_OK', reports, output }, null, 2))
console.log(JSON.stringify({ status: 'DESKTOP_CANDIDATE_OK', generated: reports.length, output }))
