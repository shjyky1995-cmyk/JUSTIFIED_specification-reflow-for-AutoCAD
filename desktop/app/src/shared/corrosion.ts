import type { Note } from './model.ts'
import { CORROSION_RULES, CORROSION_RULE_VERSION } from './corrosion-rules.ts'

export const CORROSION_FIELD_LABELS: Record<string, { label: string; unit: string }> = {
  concrete_grade_main: { label: '受腐蚀部位主体混凝土强度等级', unit: '' },
  concrete_grade_pad: { label: '基础垫层材料 / 厚度', unit: '' },
  concrete_water_binder_ratio: { label: '最大水胶比', unit: '' },
  concrete_chloride_max_content: { label: '胶凝材料中最大氯离子质量比', unit: '%' },
  concrete_max_alkali_content: { label: '最大碱含量', unit: 'kg/m³' },
  concrete_min_binder_content: { label: '最小胶凝材料用量', unit: 'kg/m³' },
  corrosion_cover_flat: { label: '受腐蚀板 / 墙最小保护层', unit: 'mm' },
  corrosion_cover_bar: { label: '受腐蚀梁 / 柱最小保护层', unit: 'mm' },
  corrosion_cover_foundation: { label: '基础、直接接触腐蚀介质地下外墙 / 底板最小保护层', unit: 'mm' },
  external_anticorrosion_coating: { label: '地下外表面防护（候选做法）', unit: '' },
}

export function isCorrosionField(id: string): boolean {
  return id in CORROSION_FIELD_LABELS || /^(cover_thickness_|concrete_|env_class_)/.test(id)
}

function numeric(value: string | undefined): number | null {
  const match = value?.trim().match(/^(?:C|≤|≥)?\s*(\d+(?:\.\d+)?)(?:\s*(?:%|mm|kg\/m[³3]))?$/i)
  return match ? Number(match[1]) : null
}

// 人工提高强度/保护层、降低水胶比等更严格取值保留；切换等级可替换旧自动值。
function choose(note: Note, id: string, required: string, minimum: boolean): string {
  const current = note.fieldValues[id]
  const previous = note.corrosionDesign?.autoValues[id]
  const reference = note.sourceReferenceValues?.[id]
  const candidates = [reference, current && current !== previous ? current : undefined].filter((value): value is string => Boolean(value))
  let selected = required
  for (const candidate of candidates) {
    const a = numeric(candidate), b = numeric(selected)
    if (a !== null && b !== null && (minimum ? a > b : a < b)) selected = candidate
    else if (candidate === current && a === null) selected = candidate
  }
  return selected
}

export function applyCorrosionScheme(note: Note, level: string): Note {
  if (note.discipline !== 'structural' || !note.structural) return note
  const rule = CORROSION_RULES[level]
  const fieldValues = { ...note.fieldValues }
  // 撤下旧自动方案，人工修改仍保留；微腐蚀不套弱腐蚀表。
  if (!rule || note.structural.designLifeYears !== 50) {
    for (const [id, value] of Object.entries(note.corrosionDesign?.autoValues ?? {})) if (fieldValues[id] === value) delete fieldValues[id]
    if (level === '微') for (const [id, value] of Object.entries(note.sourceReferenceValues ?? {})) if (isCorrosionField(id) && !fieldValues[id]?.trim()) fieldValues[id] = value
    return { ...note, structural: { ...note.structural, protectionScheme: level }, fieldValues, corrosionDesign: undefined, assemblyReviewConfirmed: false }
  }
  const autoValues: Record<string, string> = {}
  for (const [id, value] of Object.entries(note.sourceReferenceValues ?? {})) if (isCorrosionField(id) && !fieldValues[id]?.trim()) { fieldValues[id] = value; autoValues[id] = value }
  const set = (id: string, value: string, minimum = true) => { autoValues[id] = choose(note, id, value, minimum); fieldValues[id] = autoValues[id] }
  set('concrete_grade_main', `C${rule.grade}`)
  set('concrete_water_binder_ratio', rule.ratio.toFixed(2), false)
  set('concrete_chloride_max_content', `${rule.chloride.toFixed(2)}%`, false)
  set('concrete_max_alkali_content', `${rule.alkali.toFixed(1)}kg/m³`, false)
  set('concrete_min_binder_content', `${rule.binder}kg/m³`)
  set('corrosion_cover_flat', `${rule.flatCover}mm`)
  set('corrosion_cover_bar', `${rule.barCover}mm`)
  set('corrosion_cover_foundation', `${rule.foundationCover}mm`)
  for (const id of Object.keys(note.fieldDefinitions)) {
    if (/^cover_thickness_(?:column_2b|column_underground|frame_column|beam_2b|beam_underground|beam)$/.test(id)) set(id, `${rule.barCover}mm`)
    if (/^cover_thickness_(?:slab_2b|slab_underground|slab)$/.test(id)) set(id, `${rule.flatCover}mm`)
    if (/^cover_thickness_pool(?:_wall_floor)?$/.test(id)) set(id, '50mm')
  }
  const padCurrent = note.fieldValues.concrete_grade_pad
  autoValues.concrete_grade_pad = padCurrent && padCurrent !== note.corrosionDesign?.autoValues.concrete_grade_pad ? padCurrent : level === '弱' ? (note.sourceReferenceValues?.concrete_grade_pad ?? 'C20 混凝土') : 'C20 聚合物水泥混凝土（厚度100mm，满足相应介质的耐腐蚀性能要求）'
  fieldValues.concrete_grade_pad = autoValues.concrete_grade_pad
  const coatingCurrent = fieldValues.external_anticorrosion_coating
  autoValues.external_anticorrosion_coating = coatingCurrent && coatingCurrent !== note.corrosionDesign?.autoValues.external_anticorrosion_coating ? coatingCurrent : rule.coating
  fieldValues.external_anticorrosion_coating = autoValues.external_anticorrosion_coating
  const fieldDefinitions = { ...note.fieldDefinitions, ...CORROSION_FIELD_LABELS }
  return { ...note, structural: { ...note.structural, protectionScheme: level }, fieldDefinitions, fieldValues, corrosionDesign: { ruleVersion: CORROSION_RULE_VERSION, level, scopeConfirmed: false, autoValues }, assemblyReviewConfirmed: false,
    sections: note.sections.map(section => ({ ...section, modules: section.modules?.map(module => module.fieldIds.some(id => id in autoValues) ? { ...module, confirmedForNote: false } : module) })) }
}

export function corrosionIssues(note: Note): { field?: string; message: string }[] {
  const design = note.corrosionDesign
  if (!design) return []
  const rule = CORROSION_RULES[note.structural?.protectionScheme ?? '']
  if (!rule || note.structural?.designLifeYears !== 50 || design.level !== note.structural.protectionScheme || design.ruleVersion !== CORROSION_RULE_VERSION) return [{ message: '防腐方案条件已变化，请在 01 重新选择并核定方案。' }]
  const issues: { field?: string; message: string }[] = []
  if (!design.scopeConfirmed) issues.push({ message: '请在 01 核对防腐方案的受腐蚀部位、50年普通钢筋混凝土适用条件和防护做法。' })
  for (const [id, limit, minimum] of [
    ['concrete_grade_main', rule.grade, true], ['concrete_water_binder_ratio', rule.ratio, false],
    ['concrete_chloride_max_content', rule.chloride, false], ['concrete_max_alkali_content', rule.alkali, false],
    ['concrete_min_binder_content', rule.binder, true], ['corrosion_cover_flat', rule.flatCover, true],
    ['corrosion_cover_bar', rule.barCover, true], ['corrosion_cover_foundation', rule.foundationCover, true],
    ...Object.keys(design.autoValues).filter(id => /^cover_thickness_(?:column_2b|column_underground|frame_column|beam_2b|beam_underground|beam|slab_2b|slab_underground|slab|pool(?:_wall_floor)?)$/.test(id)).map(id => [id, /pool/.test(id) ? 50 : /slab/.test(id) ? rule.flatCover : rule.barCover, true]),
  ] as [string, number, boolean][]) {
    const value = numeric(note.fieldValues[id])
    if (value === null || (minimum ? value < limit : value > limit)) issues.push({ field: id, message: `防腐参数「${note.fieldDefinitions[id]?.label ?? id}」须${minimum ? '不小于' : '不大于'}${limit}，请核对取值。` })
  }
  const padGrade = note.fieldValues.concrete_grade_pad?.match(/C(\d+)/)?.[1]
  if (padGrade && Number(padGrade) < 20) issues.push({ field: 'concrete_grade_pad', message: '候选混凝土垫层强度不应低于C20；其他耐腐蚀材料须专项核定。' })
  if (!note.fieldValues.concrete_grade_pad?.trim() || !note.fieldValues.external_anticorrosion_coating?.trim()) issues.push({ message: '防腐方案缺少垫层或表面防护做法。' })
  return issues
}

export function corrosionSupplement(note: Note): string | null {
  if (!note.corrosionDesign) return null
  const values = note.fieldValues
  return `本方案用于已核定的${note.structural?.protectionScheme}腐蚀部位（设计使用年限50年、普通钢筋混凝土）：胶凝材料用量不小于${values.concrete_min_binder_content ?? '【待填写】'}；保护层最小厚度：板、墙${values.corrosion_cover_flat ?? '【待填写】'}，梁、柱${values.corrosion_cover_bar ?? '【待填写】'}，基础及直接接触腐蚀性介质的地下外墙、底板${values.corrosion_cover_foundation ?? '【待填写】'}。依据GB/T 50046-2018表4.2.3、4.2.5；基础垫层及外表面防护按表4.8.5-1核定。`
}
