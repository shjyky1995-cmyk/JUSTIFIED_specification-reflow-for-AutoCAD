// 本机桌面参数目录；不改变 CAD 协议。项目参数只在步骤 01 录入。
import type { Note, DisciplineCode } from './model.ts'

export type ProjectField = { id: string; label: string; unit: string; group: string; disciplines?: DisciplineCode[]; aliases?: string[] }
const structural: DisciplineCode[] = ['structural']
export const PROJECT_FIELDS: ProjectField[] = [
  { id: 'subproject_name', label: '分项 / 单体名称', unit: '', group: '工程资料' },
  { id: 'elevation_datum', label: '高程基准', unit: '', group: '工程资料' },
  { id: 'elevation_zero', label: '±0.000 对应高程', unit: 'm', group: '工程资料' },
  ...[
    ['foundation_type', '基础形式', ''], ['structure_system', '结构体系', ''],
    ['structural_calculation_software', '结构计算软件及版本', ''], ['pool_structure_calculation_software', '池体结构计算软件及版本', ''],
    ['structure_importance_coef', '结构重要性系数', ''], ['seismic_group', '设计地震分组', ''],
    ['characteristic_period', '场地设计特征周期', 's'], ['seismic_grade_frame', '框架抗震等级', ''],
    ['frost_depth', '标准冻深', 'm'],
  ].map(([id, label, unit]) => ({ id, label, unit, group: '结构与抗震', disciplines: structural })),
  ...[
    ['geotechnical_survey_institute', '岩土工程勘察单位', ''], ['geotechnical_survey_no', '勘察报告工程编号', ''],
    ['soil_layer_bearing', '基础持力层', ''], ['bearing_capacity', '地基承载力特征值', 'kPa'],
    ['replacement_bearing_capacity', '换填后地基承载力特征值', 'kPa'], ['groundwater_level', '稳定地下水位', 'm'],
    ['soil_corrosiveness', '场地土腐蚀性结论（按地勘报告）', ''],
  ].map(([id, label, unit]) => ({ id, label, unit, group: '勘察与地基', disciplines: structural })),
  ...[
    ['wind_pressure', '基本风压', 'kN/m²'], ['snow_pressure', '基本雪压', 'kN/m²'],
    ['ground_surcharge', '地面堆载', 'kN/m²'], ['construction_load_before_covering', '覆土完成前施工荷载', 'kPa'],
  ].map(([id, label, unit]) => ({ id, label, unit, group: '荷载', disciplines: structural })),
]

export const CORE_FIELD_IDS = new Set(['project_name', 'project_location', 'project_number', 'project_owner', 'structural_safety_level', 'foundation_design_grade', 'seismic_intensity', 'site_class', 'design_life', 'seismic_fortification_category', 'seismic_acceleration'])
export const FIXED_MATERIAL_FIELDS = new Set(['main_rebar_grades', 'embedded_part_steel_grade', 'welding_electrode_grades', 'rebar_grade_hpb', 'rebar_grade_hrb', 'electrode_grade_hpb', 'electrode_grade_hrb'])

export function isProjectField(id: string, label = ''): boolean {
  return CORE_FIELD_IDS.has(id) || PROJECT_FIELDS.some(field => field.id === id) || /工程名称|分项名称|高程基准|对应高程|勘察|基础持力层|地下水|名称或地区|适用地区|签发地区|标高/.test(label)
}

export function projectFields(note: Note): ProjectField[] {
  const fields = PROJECT_FIELDS.filter(field => !field.disciplines || field.disciplines.includes(note.discipline)).map(field => ({ ...field }))
  const ids = new Set(fields.map(field => field.id))
  for (const [id, definition] of Object.entries(note.fieldDefinitions ?? {})) {
    if (CORE_FIELD_IDS.has(id) || ids.has(id) || !isProjectField(id, definition.label)) continue
    const field = describeProjectField(note, id, definition)
    const canonical = field.id
    const existing = fields.find(item => item.id === canonical)
    // 旧草稿存在不同值时分别呈现，不能悄悄合并或覆盖。
    const values = [...new Set([canonical, id, ...(existing?.aliases ?? [])].map(key => note.fieldValues?.[key]?.trim()).filter(Boolean))]
    if (existing && values.length < 2) existing.aliases = [...(existing.aliases ?? []), id]
    else fields.push({ ...field, id: values.length > 1 ? id : canonical, aliases: [id], ...(values.length > 1 ? { label: `${field.label}（已有不同取值：${id}）` } : {}) })
    ids.add(id)
  }
  return fields
}

export function canonicalProjectField(id: string): string {
  if (/_groundwater_effect$/.test(id)) return 'groundwater_effect'
  if (/_groundwater_observation$/.test(id)) return 'groundwater_observation'
  return id
}

export function projectFieldValues(note: Note): Record<string, string> {
  const values = { ...note.fieldValues }
  for (const field of projectFields(note)) {
    const candidates = [field.id, ...(field.aliases ?? [])].map(id => values[id])
    const value = candidates.find(value => value?.trim()) ?? candidates.find(value => value !== undefined)
    if (value === undefined) continue
    values[field.id] = value
    for (const alias of field.aliases ?? []) values[alias] = value
  }
  return values
}

export function fieldLabelsFor(note: Note): Record<string, string> {
  const labels = Object.fromEntries(Object.entries(note.fieldDefinitions ?? {}).map(([id, definition]) => [id, definition.label]))
  for (const field of projectFields(note)) for (const id of [field.id, ...(field.aliases ?? [])]) labels[id] = field.label
  return labels
}

function describeProjectField(note: Note, id: string, definition: { label: string; unit: string }): ProjectField {
  if (/_groundwater_effect$/.test(id)) return { id: 'groundwater_effect', label: '地下水对基础的影响结论', unit: '', group: '勘察与地基' }
  if (/_groundwater_observation$/.test(id)) return { id: 'groundwater_observation', label: '地下水勘察情况', unit: '', group: '勘察与地基' }
  if (definition.label === '本工程对应名称或地区') {
    for (const section of note.sections) {
      for (const block of section.layoutBlocks ?? []) {
        if (block.kind !== 'table') continue
        const row = block.rows.find(row => row.some(cell => cell.includes(`{${id}}`)))
        if (row && row.some(cell => cell.includes('标高'))) return { id, label: `${row[0].trim()}标高`, unit: 'm', group: '荷载' }
      }
      const template = section.modules?.find(module => module.fieldIds.includes(id))?.template ?? ''
      const label = /标准图集/.test(template) ? '标准图集适用地区'
        : /认定证书/.test(template) ? '墙材认定证书签发地区'
        : /人民政府令/.test(template) ? '墙材管理规定适用地区'
        : /设计任务书/.test(template) ? '设计任务书项目名称前缀'
        : /消防设计/.test(template) ? '消防设计指南适用地区'
        : /公共建筑节能/.test(template) ? '节能标准适用地区'
        : /绿色建筑/.test(template) ? '绿色建筑标准适用地区' : `工程名称或地区（${section.title}）`
      const regionIds: Record<string, string> = { '标准图集适用地区': 'drawing_standard_region', '墙材认定证书签发地区': 'wall_certificate_region', '墙材管理规定适用地区': 'wall_regulation_region', '消防设计指南适用地区': 'fire_design_region', '节能标准适用地区': 'energy_standard_region', '绿色建筑标准适用地区': 'green_standard_region' }
      if (template) return { id: regionIds[label] ?? id, label, unit: definition.unit, group: '工程资料' }
    }
  }
  return { id, ...definition, group: '工程资料' }
}
