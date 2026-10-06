// 将本机候选资料按模板装配成可编辑初稿。来源关系和排除规则集中在此，避免界面逐条选取。
import { selectedModule, type ContentCatalog, type ContentClause } from './content.ts'
import { rebuildSections, type LiveLoadRow, type Note, type NoteSection } from './model.ts'
import { isProjectField } from './project-fields.ts'
import { applyCorrosionScheme, isCorrosionField } from './corrosion.ts'

type Profile = { sourceFile: string; extraFacts: string[] }

// 这些句子虽被旧交接包标作“通用”，实际预设了地区、工艺或工程做法。
// 待专业审查确认适用条件后，才可从排除名单移除。
const SPECIFIC_FACTS = new Set([
  'C0116', 'C0155', 'C0307', 'C0313', 'C0342', 'C0378', 'C0435', 'C0436', 'C0437',
  'C0664', 'C1171', 'C1264', 'C1265', 'C1385',
])
const REGIONAL_WORDING = /山东省|新疆|乌苏|济南|腊山河|尉犁/
const MISSING_ATTACHMENT = /详见下表|见下表|附表\d*|附图所示/

const PROFILES: Record<string, Profile> = {
  'tpl-struct-pool': { sourceFile: '结构设计说明（构筑物）.docx', extraFacts: ['C0001', 'C0004', 'C0005'] },
  'tpl-struct-frame': { sourceFile: '结构设计说明（框架）.docx', extraFacts: ['C0004', 'C0005'] },
  'tpl-struct-pool-frame': { sourceFile: '结构设计说明（构筑物加框架）.docx', extraFacts: ['C0001', 'C0004', 'C0005'] },
  'tpl-arch-standard': { sourceFile: '建筑设计说明厂房.docx', extraFacts: [] },
  'tpl-plumb-standard': { sourceFile: '工艺设计说明总图.docx', extraFacts: [] },
  'tpl-elec-standard': { sourceFile: '电气设计说明总图.docx', extraFacts: [] },
  'tpl-hvac-standard': { sourceFile: '暖通设计说明.docx', extraFacts: [] },
}
const CHAPTER_TITLES: Record<string, Record<string, string>> = {
  'tpl-struct-pool-frame': { CH22: '防雷接地与专业配合' },
  'tpl-elec-standard': { CH22: '电气系统设计' },
  'tpl-hvac-standard': { CH22: '暖通系统设计' },
}

function eligible(clause: ContentClause, profile: Profile): boolean {
  if (!clause.usableAsText) return false
  if (profile.sourceFile === '结构设计说明（构筑物）.docx' && clause.fieldIds.some(id => id.includes('frame'))) return false
  if (profile.sourceFile === '结构设计说明（框架）.docx' && clause.id === 'C0014') return false
  if (profile.sourceFile === '结构设计说明（构筑物）.docx' && /框架梁|框架柱|框架抗震|屋面板|挑檐|雨篷|楼梯/.test(clause.template)) return false
  if (profile.sourceFile === '结构设计说明（框架）.docx' && /水池|池体|池壁|池外壁|池顶|池底|注水/.test(clause.template)) return false
  if (SPECIFIC_FACTS.has(clause.id) || REGIONAL_WORDING.test(clause.template) || MISSING_ATTACHMENT.test(clause.template)) return false
  if (clause.flags.some(flag => ['condition', 'table', 'possible_project_literal', 'source_note', 'suspect_reference'].includes(flag))) return false
  if (clause.kind === '通用文字' || clause.kind === '待核定规范引用') return true
  return profile.extraFacts.includes(clause.id)
}

export type AssemblyResult = { note: Note; selectedClauses: number; excludedClauses: number; sourceFile: string | null }

export function templateReadiness(templateId: string, catalog: ContentCatalog | null) {
  const profile = PROFILES[templateId]
  const source = catalog?.sourceDigest.find(item => item.file === profile?.sourceFile)
  const layout = catalog?.layouts?.find(item => item.templateId === templateId && item.sourceId === source?.id)
  const clauses = source && catalog && profile ? catalog.clauses.filter(clause => clause.sources.some(ref => ref.sourceId === source.id) && eligible(clause, profile)) : []
  return {
    sourceFile: source?.file ?? null,
    version: source?.versionRelation ?? '',
    chapters: layout?.sections.length ?? new Set(clauses.map(clause => clause.chapterId)).size,
    paragraphs: layout?.sections.reduce((count, section) => count + section.blocks.filter(block => block.kind === 'paragraph').length, 0) ?? clauses.length,
    tables: layout?.sections.reduce((count, section) => count + section.blocks.filter(block => block.kind === 'table').length, 0) ?? 0,
    available: Boolean(layout || clauses.length),
  }
}

export function assembleNote(note: Note, templateId: string, catalog: ContentCatalog | null): AssemblyResult {
  const profile = PROFILES[templateId]
  const empty = { ...note, templateId, sections: rebuildSections(note, templateId), fieldDefinitions: {}, fieldValues: Object.fromEntries(Object.entries(note.fieldValues).filter(([id]) => isProjectField(id, note.fieldDefinitions[id]?.label) || isCorrosionField(id) || id === 'external_anticorrosion_coating')), corrosionDesign: note.corrosionDesign, sourceReferenceValues: {}, assemblyReviewConfirmed: false, assemblyPackageId: '' }
  if (!profile || !catalog) return { note: empty, selectedClauses: 0, excludedClauses: 0, sourceFile: null }
  const source = catalog.sourceDigest.find(item => item.file === profile.sourceFile)
  if (!source) return { note: empty, selectedClauses: 0, excludedClauses: 0, sourceFile: null }
  const related = catalog.clauses.filter(clause => clause.sources.some(ref => ref.sourceId === source.id))
  const layout = catalog.layouts?.find(item => item.templateId === templateId && item.sourceId === source.id)
  if (layout) {
    const clausesById = new Map(catalog.clauses.map(clause => [clause.id, clause]))
    const sections: NoteSection[] = layout.sections.map(section => {
      const modules: NonNullable<NoteSection['modules']> = []
      const layoutBlocks: NonNullable<NoteSection['layoutBlocks']> = []
      for (const block of section.blocks) {
        if (block.kind === 'table') {
          layoutBlocks.push({ kind: 'table', rows: block.rows.map(row => [...row]), columnWidths: block.columnWidths, sourcePara: block.sourcePara, reviewNote: block.reviewNote, confirmedForNote: false })
          continue
        }
        const id = `${layout.sourceId}-p${block.sourcePara}`
        modules.push({ id, clauseId: block.clauseIds.join('+') || `原稿段落${block.sourcePara}`, packageId: catalog.packageId, template: block.template, baseTemplate: block.template, edited: false, fieldIds: block.fieldIds, sourceRefs: [{ sourceId: source.id, para: block.sourcePara, file: source.file }], refs: [...new Set(block.clauseIds.flatMap(clauseId => clausesById.get(clauseId)?.refs ?? []))], flags: block.reviewNote.startsWith('阻断') ? ['requires_rewrite'] : block.reviewNote.startsWith('条件') ? ['requires_applicability_review'] : [], reviewStatus: 'pending', confirmedForNote: false })
        layoutBlocks.push({ kind: 'paragraph', moduleId: id })
      }
      return { id: `lib-${section.id}`, title: section.title, body: '', custom: false, modules, layoutBlocks }
    })
    const usedFields = new Set(sections.flatMap(section => section.modules?.flatMap(module => module.fieldIds) ?? []))
    for (const section of sections) for (const block of section.layoutBlocks ?? []) if (block.kind === 'table') for (const cell of block.rows.flat()) for (const match of cell.matchAll(/\{([a-z][a-z0-9_]*)\}/g)) usedFields.add(match[1])
    const definitions: Note['fieldDefinitions'] = {}
    for (const field of [...catalog.fields, ...(layout.fields ?? [])]) if (usedFields.has(field.id)) definitions[field.id] = { label: field.label, unit: field.unit }
    // 活荷载参数表：原稿表格中带“活荷载”表头的取值行改为 01 项目参数表管理，默认值取原稿登记值。
    const liveLoads: LiveLoadRow[] = []
    for (const section of sections) for (const block of section.layoutBlocks ?? []) {
      if (block.kind !== 'table' || !block.rows[0]?.some(cell => cell.includes('活荷载'))) continue
      block.liveLoad = true
      for (const row of block.rows.slice(1)) {
        const fieldId = row.find(cell => /\{[a-z][a-z0-9_]*\}/.test(cell))?.match(/\{([a-z][a-z0-9_]*)\}/)?.[1]
        if (!fieldId) continue
        liveLoads.push({ item: row[0]?.trim() ?? '', value: layout.referenceValues?.[fieldId]?.trim() ?? '', fieldId })
      }
    }
    const liveLoadFieldIds = new Set(liveLoads.map(row => row.fieldId))
    // 材料、环境、防水等稳定参数按原稿登记值作默认值，不再逐项填写；01 项目参数与腐蚀方案字段保持原流程。
    const defaults: Record<string, string> = {}
    for (const id of usedFields) {
      if (liveLoadFieldIds.has(id) || isProjectField(id, catalog.fields.find(field => field.id === id)?.label ?? '') || isCorrosionField(id) || id === 'external_anticorrosion_coating') continue
      const value = layout.referenceValues?.[id]?.trim()
      if (value) { defaults[id] = value; definitions[id] = { ...definitions[id], defaultValue: value } }
    }
    const populated: Note = { ...empty, title: layout.sourceTitle || source.file.replace(/\.docx$/i, ''), sections, fieldDefinitions: definitions, fieldValues: { ...empty.fieldValues, ...defaults }, sourceReferenceValues: layout.referenceValues, liveLoads, assemblyPackageId: catalog.packageId }
    const result = note.structural?.protectionScheme ? applyCorrosionScheme(populated, note.structural?.protectionScheme ?? '') : populated
    return { note: result, selectedClauses: sections.reduce((count, section) => count + (section.modules?.length ?? 0), 0), excludedClauses: Math.max(0, related.length - sections.reduce((count, section) => count + (section.modules?.length ?? 0), 0)), sourceFile: source.file }
  }
  const chosen = related.filter(clause => eligible(clause, profile))
  const sections: NoteSection[] = catalog.chapters.flatMap(chapter => {
    const chapterClauses = chosen.filter(clause => clause.chapterId === chapter.id)
    return chapterClauses.length > 0 ? [{ id: `lib-${chapter.id}`, title: CHAPTER_TITLES[templateId]?.[chapter.id] ?? chapter.title, body: '', custom: false, modules: chapterClauses.map(clause => selectedModule(clause, catalog)) }] : []
  })
  const definitions: Note['fieldDefinitions'] = {}
  const usedFields = new Set(chosen.flatMap(clause => clause.fieldIds))
  for (const field of catalog.fields) if (usedFields.has(field.id)) definitions[field.id] = { label: field.label, unit: field.unit }
  return {
    note: { ...empty, sections, fieldDefinitions: definitions, assemblyPackageId: catalog.packageId },
    selectedClauses: chosen.length,
    excludedClauses: related.length - chosen.length,
    sourceFile: profile.sourceFile,
  }
}
