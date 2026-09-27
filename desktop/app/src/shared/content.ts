// 私有资料库的纯数据模型；不在网页包中包含旧工程正文。
import type { DisciplineCode } from './model.ts'

export type ContentChapter = { id: string; title: string }
export type ContentField = { id: string; label: string; unit: string; scope: string; reviewStatus: string; aliases: string[] }
export type ContentSource = { sourceId: string; para: number; file?: string }
export type ContentClause = {
  id: string
  chapterId: string
  disciplines: string[]
  kind: string
  template: string
  fieldIds: string[]
  refs: string[]
  sources: ContentSource[]
  note: string
  reviewStatus: string
  flags: string[]
  usableAsText: boolean
}
export type ContentCatalog = {
  schemaVersion: 1
  packageId: string
  generatedAt: string
  title: string
  reviewStatus: string
  chapters: ContentChapter[]
  fields: ContentField[]
  clauses: ContentClause[]
  sourceDigest: { id: string; file: string; sha256: string | null; discipline: string; versionRelation: string; rightsStatus: string }[]
}

export type SelectedModule = {
  id: string
  clauseId: string
  packageId: string
  template: string
  baseTemplate: string
  edited: boolean
  fieldIds: string[]
  sourceRefs: ContentSource[]
  refs: string[]
  flags: string[]
  reviewStatus: string
  confirmedForNote: boolean
}

function object(value: unknown, label: string): Record<string, unknown> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error(label + '格式不正确。')
  return value as Record<string, unknown>
}

function string(value: unknown, label: string): string {
  if (typeof value !== 'string' || value.trim().length === 0) throw new Error(label + '不能为空。')
  return value
}

function strings(value: unknown, label: string): string[] {
  if (!Array.isArray(value) || !value.every(item => typeof item === 'string')) throw new Error(label + '格式不正确。')
  return value
}

export function parseContentCatalog(value: unknown): ContentCatalog {
  const raw = object(value, '资料库')
  if (raw.schemaVersion !== 1) throw new Error('资料库版本不受支持。')
  const chapters = Array.isArray(raw.chapters) ? raw.chapters.map(item => {
    const row = object(item, '章节')
    return { id: string(row.id, '章节编号'), title: string(row.title, '章节名称') }
  }) : []
  const fields = Array.isArray(raw.fields) ? raw.fields.map(item => {
    const row = object(item, '字段')
    return { id: string(row.id, '字段编号'), label: string(row.label, '字段名称'), unit: typeof row.unit === 'string' ? row.unit : '', scope: typeof row.scope === 'string' ? row.scope : '', reviewStatus: typeof row.reviewStatus === 'string' ? row.reviewStatus : 'pending', aliases: strings(row.aliases ?? [], '字段别名') }
  }) : []
  const sourceDigest = Array.isArray(raw.sourceDigest) ? raw.sourceDigest.map(item => {
    const row = object(item, '来源')
    return { id: string(row.id, '来源编号'), file: string(row.file, '来源文件'), sha256: typeof row.sha256 === 'string' ? row.sha256 : null, discipline: typeof row.discipline === 'string' ? row.discipline : '', versionRelation: typeof row.versionRelation === 'string' ? row.versionRelation : '', rightsStatus: typeof row.rightsStatus === 'string' ? row.rightsStatus : '待核定' }
  }) : []
  const clauses = Array.isArray(raw.clauses) ? raw.clauses.map(item => {
    const row = object(item, '条款')
    const sources = Array.isArray(row.sources) ? row.sources.map(value => {
      const source = object(value, '条款来源')
      if (!Number.isInteger(source.para) || (source.para as number) < 0) throw new Error('条款来源段落号无效。')
      return { sourceId: string(source.sourceId, '来源编号'), para: source.para as number }
    }) : []
    return {
      id: string(row.id, '条款编号'), chapterId: string(row.chapterId, '条款章节'),
      disciplines: strings(row.disciplines, '条款专业'), kind: string(row.kind, '条款类型'),
      template: string(row.template, '条款文字'), fieldIds: strings(row.fieldIds, '条款字段'),
      refs: strings(row.refs, '规范引用'), sources, note: typeof row.note === 'string' ? row.note : '',
      reviewStatus: string(row.reviewStatus, '审核状态'), flags: strings(row.flags, '风险标记'), usableAsText: row.usableAsText === true,
    }
  }) : []
  if (chapters.length === 0 || clauses.length === 0) throw new Error('资料库没有章节或条款。')
  const unique = (ids: string[], label: string) => { if (new Set(ids).size !== ids.length) throw new Error(label + '存在重复编号。') }
  unique(chapters.map(item => item.id), '章节')
  unique(fields.map(item => item.id), '字段')
  unique(clauses.map(item => item.id), '条款')
  unique(sourceDigest.map(item => item.id), '来源')
  const chapterIds = new Set(chapters.map(item => item.id))
  const fieldIds = new Set(fields.map(item => item.id))
  const sourceIds = new Set(sourceDigest.map(item => item.id))
  for (const clause of clauses) {
    if (!chapterIds.has(clause.chapterId)) throw new Error(`条款 ${clause.id} 的章节不存在。`)
    if (clause.fieldIds.some(id => !fieldIds.has(id))) throw new Error(`条款 ${clause.id} 的字段不存在。`)
    if (clause.sources.length === 0 || clause.sources.some(source => !sourceIds.has(source.sourceId))) throw new Error(`条款 ${clause.id} 的来源不存在。`)
    const inText = [...clause.template.matchAll(/\{([a-z][a-z0-9_]*)\}/g)].map(match => match[1])
    if (new Set(inText).size !== new Set(clause.fieldIds).size || inText.some(id => !clause.fieldIds.includes(id))) throw new Error(`条款 ${clause.id} 的占位符清单不一致。`)
  }
  return {
    schemaVersion: 1,
    packageId: string(raw.packageId, '资料库编号'), generatedAt: typeof raw.generatedAt === 'string' ? raw.generatedAt : '',
    title: string(raw.title, '资料库名称'), reviewStatus: typeof raw.reviewStatus === 'string' ? raw.reviewStatus : 'pending',
    chapters, fields, clauses, sourceDigest,
  }
}

export function clausesFor(catalog: ContentCatalog, discipline: DisciplineCode, chapterId: string): ContentClause[] {
  const matching = discipline === 'plumbing' ? ['process'] : [discipline]
  return catalog.clauses.filter(clause => clause.chapterId === chapterId && clause.disciplines.some(code => matching.includes(code)))
}

export function availableChapters(catalog: ContentCatalog, discipline: DisciplineCode): ContentChapter[] {
  return catalog.chapters.filter(chapter => clausesFor(catalog, discipline, chapter.id).length > 0)
}

export function selectedModule(clause: ContentClause, catalog: ContentCatalog): SelectedModule {
  if (!clause.usableAsText) throw new Error('此条款需要先人工核对，暂不能作为纯文本加入。')
  return {
    id: clause.id,
    clauseId: clause.id,
    packageId: catalog.packageId,
    template: clause.template,
    baseTemplate: clause.template,
    edited: false,
    fieldIds: [...clause.fieldIds],
    sourceRefs: clause.sources.map(source => ({ ...source, file: catalog.sourceDigest.find(item => item.id === source.sourceId)?.file })),
    refs: [...clause.refs],
    flags: [...clause.flags],
    reviewStatus: clause.reviewStatus,
    confirmedForNote: false,
  }
}

export function renderModule(module: SelectedModule, values: Record<string, string>, labels: Record<string, string> = {}): { text: string; missing: string[] } {
  const missing: string[] = []
  const text = module.template.replace(/\{([a-z][a-z0-9_]*)\}/g, (_match, id: string) => {
    const value = values[id]?.trim()
    if (value) return value
    missing.push(id)
    return `【待填写：${labels[id] ?? id}】`
  })
  return { text, missing: [...new Set(missing)] }
}
