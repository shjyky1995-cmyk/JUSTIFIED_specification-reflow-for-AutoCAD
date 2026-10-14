// 当前说明的核对清单和来源索引；只计算，不改变草稿/导出协议。
import { effectiveFieldValues, type Note, type NoteIssue } from './model.ts'
import { projectFields, isProjectField } from './project-fields.ts'
import { isCorrosionField } from './corrosion.ts'
import { renderTemplate, type ContentCatalog } from './content.ts'

export type ReviewTarget = { page: 'parameters' | 'editor' | 'preview'; field?: string; sectionId?: string; moduleId?: string; tablePara?: number }
const CORE_TARGETS: Record<string, string> = {
  project_name: 'project.name', project_number: 'project.number', project_owner: 'project.owner', project_location: 'project.location',
  structural_safety_level: 'structural.safetyLevel', foundation_design_grade: 'structural.foundationGrade',
  seismic_intensity: 'structural.seismicIntensity', site_class: 'structural.siteCategory', design_life: 'structural.designLifeYears',
  seismic_fortification_category: 'structural.seismicGrade', seismic_acceleration: 'structural.seismicIntensity',
}

export function reviewTarget(note: Note, issue: NoteIssue): ReviewTarget {
  const field = issue.field
  if (field) {
    if (CORE_TARGETS[field]) return { page: 'parameters', field: CORE_TARGETS[field] }
    if (field === 'title' || field.startsWith('project.') || field.startsWith('structural.')) return { page: 'parameters', field }
    if (isCorrosionField(field) || field === 'external_anticorrosion_coating') return { page: 'parameters', field: note.corrosionDesign ? field : 'structural.protectionScheme' }
    if (isProjectField(field, note.fieldDefinitions[field]?.label)) {
      const parameter = projectFields(note).find(item => item.id === field || item.aliases?.includes(field))
      return { page: 'parameters', field: parameter?.id ?? field }
    }
  }
  if (issue.sectionId) return { page: 'editor', sectionId: issue.sectionId, field, moduleId: issue.moduleId, tablePara: issue.tablePara }
  if (issue.message.includes('防腐') || issue.message.includes('腐蚀')) return { page: 'parameters', field: 'structural.protectionScheme' }
  if (issue.message.includes('整篇') || issue.level === 'warning') return { page: 'preview' }
  return { page: 'editor', sectionId: note.sections[0]?.id }
}

export type ReviewItem = { issue: NoteIssue; target: ReviewTarget; occurrences: NoteIssue[] }
export function reviewQueue(note: Note, issues: NoteIssue[]): ReviewItem[] {
  const items = new Map<string, ReviewItem>()
  for (const issue of issues) {
    const target = reviewTarget(note, issue)
    // 同一个输入被多个段落/表格引用时只列一次；不同正文条件分别保留。
    const key = issue.level + ':' + (issue.field ? JSON.stringify({ page: target.page, field: target.field, sectionId: target.page === 'editor' ? target.sectionId : undefined }) : JSON.stringify(issue))
    const existing = items.get(key)
    if (existing) existing.occurrences.push(issue)
    else items.set(key, { issue, target, occurrences: [issue] })
  }
  return [...items.values()]
}

// 识别正文里常见的规范/行业标准/图集编号；编号识别不等于版本核验。
export function extractReferences(text: string): string[] {
  const normalized = text.toUpperCase().replace(/[－—–]/g, '-').replace(/／/g, '/').replace(/[（）]/g, char => char === '（' ? '(' : ')')
  const matches = normalized.match(/(?:GB|JGJ|CJJ|CJ|HJ|DL|NB|CECS|T\/CECS|DB\d{1,2})(?:\s*\/?\s*T)?\s*\d{1,6}(?:\.\d+)?(?:\s*[-:]\s*\d{2,4})?|\b\d{2}[A-Z]{1,3}\d{2,4}(?:-\d+)?\b/g) ?? []
  return [...new Set(matches.map(ref => ref.replace(/\s+/g, '')))]
}

export type ReferenceOccurrence = { sectionId: string; sectionTitle: string; moduleId?: string; tablePara?: number; source: string; edited: boolean }
export function referenceIndex(note: Note): { code: string; occurrences: ReferenceOccurrence[] }[] {
  const index = new Map<string, ReferenceOccurrence[]>()
  const values = effectiveFieldValues(note)
  function add(text: string, occurrence: ReferenceOccurrence) {
    for (const code of extractReferences(renderTemplate(text, values).text)) {
      const existing = index.get(code) ?? []
      existing.push(occurrence)
      index.set(code, existing)
    }
  }
  for (const section of note.sections) {
    add(section.body, { sectionId: section.id, sectionTitle: section.title, source: '本份手写正文', edited: true })
    for (const module of section.modules ?? []) add(module.template, { sectionId: section.id, sectionTitle: section.title, moduleId: module.id, source: module.sourceRefs.map(source => `${source.file ?? source.sourceId} 第${source.para}段`).join('；') || '未记录来源', edited: module.edited })
    for (const block of section.layoutBlocks ?? []) if (block.kind === 'table') add(block.rows.flat().join('\n'), { sectionId: section.id, sectionTitle: section.title, tablePara: block.sourcePara, source: `原稿第${block.sourcePara}处表格`, edited: true })
  }
  return [...index].map(([code, occurrences]) => ({ code, occurrences })).sort((a, b) => a.code.localeCompare(b.code))
}

export function sourceIndex(note: Note, catalog: ContentCatalog | null) {
  const sources = new Map<string, { id: string; file: string; paragraphs: number; sections: Set<string>; packageIds: Set<string> }>()
  for (const section of note.sections) for (const module of section.modules ?? []) for (const source of module.sourceRefs) {
    const entry = sources.get(source.sourceId) ?? { id: source.sourceId, file: source.file ?? source.sourceId, paragraphs: 0, sections: new Set<string>(), packageIds: new Set<string>() }
    entry.paragraphs++
    entry.sections.add(section.title)
    entry.packageIds.add(module.packageId)
    sources.set(source.sourceId, entry)
  }
  return [...sources.values()].map(entry => ({ ...entry, sections: [...entry.sections], digest: entry.packageIds.size === 1 && entry.packageIds.has(catalog?.packageId ?? '') ? catalog?.sourceDigest.find(source => source.id === entry.id) : undefined }))
}
