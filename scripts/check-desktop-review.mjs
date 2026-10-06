// 核对定位、改写后的引用和七模板来源覆盖；不启动桌面界面。
import assert from 'node:assert/strict'
import { mkdirSync, writeFileSync } from 'node:fs'
import { resolve, join } from 'node:path'
import { createNote, findIssues, setModuleTemplate, toExportRequest } from '../desktop/app/src/shared/model.ts'
import { reviewQueue, reviewTarget, referenceIndex, sourceIndex, extractReferences } from '../desktop/app/src/shared/review.ts'
import { assembleNote, templateReadiness } from '../desktop/app/src/shared/assembly.ts'
import { standardsLibrary, addStandardToNote } from '../desktop/app/src/shared/standards.ts'
import { createStore } from '../desktop/app/src/shared/store.ts'

const note = createNote('structural', 'tpl-struct-pool', { name: '', number: '', owner: '', location: '' })
note.assemblyPackageId = 'sample-v1'
note.fieldDefinitions = { shared_value: { label: '基础持力层', unit: '' }, custom_value: { label: '工艺取值', unit: '' }, src_a_groundwater_effect: { label: '本工程地下水影响结论', unit: '' }, src_b_groundwater_effect: { label: '本工程地下水影响结论', unit: '' } }
const module = (id, para, template) => ({ id, clauseId: id, packageId: 'sample-v1', template, baseTemplate: template, fieldIds: ['shared_value'], sourceRefs: [{ sourceId: 'S1', file: '脱敏来源.docx', para }], refs: ['GB50010-2010'], flags: ['requires_applicability_review'], reviewStatus: 'pending', confirmedForNote: false, edited: false })
note.sections = [{ id: 'section-1', title: '基础', body: '', custom: false, modules: [module('p1', 1, 'GB 50010-2010；持力层{shared_value}'), module('p2', 2, '持力层{shared_value}')], layoutBlocks: [{ kind: 'paragraph', moduleId: 'p1' }, { kind: 'paragraph', moduleId: 'p2' }, { kind: 'table', sourcePara: 3, rows: [['值', '{shared_value}'], ['标准', '16G101-1']], reviewNote: '条件：核对构件类型', confirmedForNote: false }] }]
const issues = findIssues(note)
const queue = reviewQueue(note, issues)
assert.equal(queue.filter(item => item.issue.field === 'shared_value').length, 1)
assert.equal(queue.find(item => item.issue.field === 'shared_value').occurrences.length, 3)
assert.equal(queue.find(item => item.issue.field === 'shared_value').target.page, 'parameters')
assert.equal(issues.find(item => item.message.includes('第 1 段的适用')).moduleId, 'p1')
assert.equal(issues.find(item => item.message.includes('处表格的适用')).tablePara, 3)
assert.equal(reviewTarget(note, { field: 'seismic_acceleration' }).field, 'structural.seismicIntensity')
assert.equal(reviewTarget(note, { field: 'src_b_groundwater_effect' }).field, 'groundwater_effect')
note.fieldValues = { src_a_groundwater_effect: '值一', src_b_groundwater_effect: '值二' }
assert.equal(reviewTarget(note, { field: 'src_b_groundwater_effect' }).field, 'src_b_groundwater_effect')
assert.equal(reviewTarget(note, { field: 'custom_value', sectionId: 'section-1' }).page, 'editor')
assert.deepEqual(extractReferences('GB/T 50046—2018、JGJ 3-2010、16G101-1、GB/T50046-2018'), ['GB/T50046-2018', 'JGJ3-2010', '16G101-1'])
assert.ok(referenceIndex(note).some(ref => ref.code === '16G101-1' && ref.occurrences[0].tablePara === 3))
const edited = setModuleTemplate(note, 'section-1', 'p1', 'GB/T 50046-2018；持力层{shared_value}')
assert.ok(!referenceIndex(edited).some(ref => ref.code === 'GB50010-2010'))
assert.equal(referenceIndex(edited).find(ref => ref.code === 'GB/T50046-2018').occurrences[0].edited, true)
assert.equal(toExportRequest(edited, 'draft.docx').document.exportMode, 'draft')
assert.throws(() => toExportRequest(edited, 'reviewed.docx', 'reviewed'))
const catalog = { packageId: 'sample-v1', sourceDigest: [{ id: 'S1', sha256: 'sample', file: '脱敏来源.docx' }] }
assert.equal(sourceIndex(note, catalog)[0].digest.sha256, 'sample')
assert.equal(sourceIndex(note, { ...catalog, packageId: 'sample-v2' })[0].digest, undefined)

const output = resolve(`artifacts/desktop-review-check-${Date.now()}`)
mkdirSync(output, { recursive: true })
const templates = []
if (process.env.DSS_CONTENT_LIBRARY_PATH) {
  const local = createStore(join(output, 'catalog'), process.env.DSS_CONTENT_LIBRARY_PATH).loadCatalog()
  for (const layout of local.layouts) {
    const discipline = layout.templateId.startsWith('tpl-struct') ? 'structural' : layout.templateId.startsWith('tpl-arch') ? 'architecture' : layout.templateId.startsWith('tpl-plumb') ? 'plumbing' : layout.templateId.startsWith('tpl-elec') ? 'electrical' : 'hvac'
    const generated = assembleNote(createNote(discipline, layout.templateId, { name: '', number: '', owner: '', location: '' }), layout.templateId, local).note
    const readiness = templateReadiness(layout.templateId, local)
    assert.equal(readiness.chapters, generated.sections.length)
    assert.equal(readiness.paragraphs, generated.sections.reduce((sum, section) => sum + section.modules.length, 0))
    assert.equal(readiness.tables, generated.sections.reduce((sum, section) => sum + section.layoutBlocks.filter(block => block.kind === 'table').length, 0))
    for (const section of generated.sections) for (const item of section.modules) {
      const expected = [...new Set(item.clauseId.split('+').flatMap(id => local.clauses.find(clause => clause.id === id)?.refs ?? []))]
      assert.deepEqual(item.refs, expected)
    }
    const generatedQueue = reviewQueue(generated, findIssues(generated))
    templates.push({ template: layout.templateId, chapters: readiness.chapters, paragraphs: readiness.paragraphs, tables: readiness.tables, rawIssues: findIssues(generated).length, groupedIssues: generatedQueue.length, references: referenceIndex(generated).length })
  }
  assert.equal(templates.length, 7)
  assert.equal(templateReadiness('tpl-struct-steel', local).available, false)
  assert.equal(templateReadiness('tpl-other-standard', local).available, false)
  const pool = assembleNote(createNote('structural', 'tpl-struct-pool', { name: '', number: '', owner: '', location: '' }), 'tpl-struct-pool', local).note
  // 材料/环境/防水等稳定参数按原稿登记值作默认值，不再逐项待填写。
  assert.equal(pool.fieldDefinitions.cement_type?.defaultValue, '普通硅酸盐水泥')
  assert.equal(pool.fieldValues.cement_type, '普通硅酸盐水泥')
  assert.ok(!findIssues(pool).some(issue => issue.message.includes('水泥品种')))
  // 活荷载改由 01 参数表管理：默认值来自原稿登记值，清空后再报缺项。
  const stairs = (pool.liveLoads ?? []).find(row => row.item === '楼梯')
  assert.equal(stairs?.value, '3.5')
  const loadBlock = pool.sections.flatMap(section => section.layoutBlocks ?? []).find(block => block.kind === 'table' && block.liveLoad)
  assert.ok(loadBlock)
  stairs.value = ''
  assert.ok(findIssues(pool).some(issue => issue.field === stairs.fieldId))
  stairs.value = '3.5'
  // 规范库与设计依据清单：可汇总、可加入、加入后可导出引用。
  const library = standardsLibrary(local)
  assert.ok(library.length > 10)
  const basis = pool.sections.find(section => section.title.includes('设计依据'))
  assert.ok(basis)
  const withStandard = addStandardToNote(pool, basis.id, 'GB 50010-2010', '混凝土结构设计规范')
  assert.ok(withStandard.sections.find(section => section.id === basis.id).modules.some(module => module.template.includes('GB 50010-2010')))
}
writeFileSync(join(output, 'report.json'), JSON.stringify({ status: 'DESKTOP_REVIEW_OK', templates, output }, null, 2))
console.log(JSON.stringify({ status: 'DESKTOP_REVIEW_OK', templates, output }))
