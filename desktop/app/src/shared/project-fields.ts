// 本机桌面参数目录；不改变 CAD 协议。项目参数只在步骤 01 录入。
import type { Note, DisciplineCode } from './model.ts'

export type ProjectField = { id: string; label: string; unit: string; group: string; disciplines?: DisciplineCode[] }
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
    ['concrete_environment_class', '混凝土结构环境类别', ''], ['frost_depth', '标准冻深', 'm'],
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
  return CORE_FIELD_IDS.has(id) || PROJECT_FIELDS.some(field => field.id === id) || /工程名称|分项名称|高程基准|对应高程|勘察|基础持力层|地下水|名称或地区/.test(label)
}

export function projectFields(note: Note): ProjectField[] {
  const fields = PROJECT_FIELDS.filter(field => !field.disciplines || field.disciplines.includes(note.discipline))
  const ids = new Set(fields.map(field => field.id))
  for (const [id, definition] of Object.entries(note.fieldDefinitions)) {
    if (CORE_FIELD_IDS.has(id) || ids.has(id) || !isProjectField(id, definition.label)) continue
    fields.push({ id, ...definition, group: '工程资料' })
    ids.add(id)
  }
  return fields
}
