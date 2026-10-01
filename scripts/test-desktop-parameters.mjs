// 参数只填一次、来源固定正文、防腐切换及导出阻断的领域回归；使用脱敏自建样本。
import assert from 'node:assert/strict'
import { createNote, parseNote, setFieldValue, effectiveFieldValues, buildDocument } from '../desktop/app/src/shared/model.ts'
import { projectFields, isProjectField, FIXED_MATERIAL_FIELDS } from '../desktop/app/src/shared/project-fields.ts'
import { assembleNote } from '../desktop/app/src/shared/assembly.ts'
import { renderTemplate } from '../desktop/app/src/shared/content.ts'
import { applyCorrosionScheme, corrosionIssues } from '../desktop/app/src/shared/corrosion.ts'

const note = createNote('structural', 'tpl-struct-frame', { name: '脱敏工程', number: '01', owner: '建设单位', location: '地点' })
note.structural.seismicIntensity = '7度（0.15g）'
note.sourceReferenceValues = { concrete_grade_main: 'C30', concrete_water_binder_ratio: '0.38', concrete_chloride_max_content: '0.08', concrete_max_alkali_content: '3.0', cover_thickness_column_2b: '35', cover_thickness_slab_2b: '25' }
note.fieldDefinitions = { foundation_type: { label: '基础形式', unit: '' }, concrete_grade_main: { label: '混凝土强度', unit: '' }, cover_thickness_column_2b: { label: '柱保护层', unit: 'mm' }, cover_thickness_slab_2b: { label: '板保护层', unit: 'mm' } }
note.sections = [{ id: 'overview', title: '工程概况', custom: false, body: '', modules: [{ id: 'a', clauseId: 'a', packageId: 'test', template: '采用{foundation_type}，混凝土{concrete_grade_main}。', baseTemplate: '', edited: false, fieldIds: ['foundation_type', 'concrete_grade_main'], sourceRefs: [], refs: [], flags: [], reviewStatus: 'pending', confirmedForNote: true }], layoutBlocks: [{ kind: 'paragraph', moduleId: 'a' }] }]
const changed = setFieldValue(note, 'foundation_type', '筏板基础')
assert.equal(renderTemplate('{foundation_type}', effectiveFieldValues(changed)).text, '筏板基础')
assert.equal(parseNote(JSON.parse(JSON.stringify(changed))).fieldValues.foundation_type, '筏板基础')
assert.equal(effectiveFieldValues(changed).seismic_acceleration, '0.15g')
for (const id of ['foundation_type', 'structure_system', 'structural_calculation_software', 'structure_importance_coef', 'seismic_group', 'characteristic_period', 'seismic_grade_frame', 'geotechnical_survey_institute', 'geotechnical_survey_no', 'soil_corrosiveness']) assert.ok(projectFields(changed).some(field => field.id === id), id)
assert.ok(isProjectField('src_old_combined', '高程基准 / ±0.000 对应高程值'))
for (const id of FIXED_MATERIAL_FIELDS) assert.equal(isProjectField(id), false)
const other = createNote('electrical', 'tpl-elec-standard', note.project)
assert.ok(!projectFields(other).some(field => field.id === 'seismic_grade_frame'))
assert.equal(applyCorrosionScheme(other, '强'), other)

let weak = applyCorrosionScheme(changed, '弱')
assert.equal(weak.fieldValues.concrete_grade_main, 'C30')
assert.equal(weak.fieldValues.concrete_water_binder_ratio, '0.38')
assert.equal(weak.fieldValues.concrete_chloride_max_content, '0.08')
assert.equal(weak.fieldValues.cover_thickness_slab_2b, '30mm')
assert.ok(corrosionIssues(weak).some(issue => !issue.field))
let medium = applyCorrosionScheme(weak, '中')
assert.equal(medium.fieldValues.concrete_grade_main, 'C35')
assert.equal(medium.fieldValues.concrete_min_binder_content, '320kg/m³')
let strong = applyCorrosionScheme(medium, '强')
assert.equal(strong.fieldValues.concrete_grade_main, 'C40')
assert.equal(strong.fieldValues.cover_thickness_column_2b, '40mm')
assert.equal(strong.fieldValues.cover_thickness_slab_2b, '35mm')
assert.match(strong.fieldValues.external_anticorrosion_coating, /500μm/)
strong.corrosionDesign.scopeConfirmed = true
assert.deepEqual(corrosionIssues(strong), [])
assert.deepEqual(parseNote(JSON.parse(JSON.stringify(strong))).corrosionDesign, strong.corrosionDesign)
assert.ok(buildDocument(strong).blocks.some(block => block.text?.includes('胶凝材料用量不小于340kg/m³')))
const unsafe = setFieldValue(strong, 'concrete_grade_main', 'C30')
assert.ok(corrosionIssues(unsafe).some(issue => issue.field === 'concrete_grade_main'))
const stricter = setFieldValue(strong, 'concrete_grade_main', 'C50')
assert.equal(applyCorrosionScheme(stricter, '中').fieldValues.concrete_grade_main, 'C50')
assert.equal(applyCorrosionScheme(strong, '弱').fieldValues.concrete_grade_main, 'C30')
const micro = applyCorrosionScheme(strong, '微')
assert.equal(micro.corrosionDesign, undefined)
assert.equal(micro.fieldValues.concrete_grade_main, 'C30')
assert.equal(micro.fieldValues.foundation_type, '筏板基础')
const longLife = { ...changed, structural: { ...changed.structural, designLifeYears: 100 } }
assert.equal(applyCorrosionScheme(longLife, '强').corrosionDesign, undefined)
assert.equal(renderTemplate('{x}mm，{y}%，{z}kg/m³', { x: '50mm', y: '0.08%', z: '3.0kg/m³' }).text, '50mm，0.08%，3.0kg/m³')

// 从第一步输入到选模板，项目值不能被清空。
const catalog = { packageId: 'test', fields: [{ id: 'foundation_type', label: '基础形式', unit: '' }], clauses: [], sourceDigest: [{ id: 's', file: '结构设计说明（框架）.docx' }], layouts: [{ templateId: 'tpl-struct-frame', sourceId: 's', sourceTitle: '说明', sections: [{ id: 's1', title: '概况', blocks: [{ kind: 'paragraph', sourcePara: 1, template: '{foundation_type}', clauseIds: [], fieldIds: ['foundation_type'], reviewNote: '来源' }] }] }] }
assert.equal(assembleNote(changed, 'tpl-struct-frame', catalog).note.fieldValues.foundation_type, '筏板基础')
assert.equal(assembleNote(stricter, 'tpl-struct-frame', catalog).note.fieldValues.concrete_grade_main, 'C50', '第一步人工提高的材料值必须跨模板保留')
console.log('DESKTOP_PARAMETERS_OK project-link save-reopen source-default corrosion-levels unsafe-block')
