// 设计说明桌面端共享领域模型：专业、结构参数、章节库、模板、当前说明、校验与导出映射。
// 约束：仅使用可擦除语法（无枚举/命名空间/参数属性），供 Electron 主进程、渲染进程与 Node 测试共同引用。
import { renderModule, type SelectedModule } from './content.ts'

export const LIBRARY_VERSION = '1.0.1'

export type DisciplineCode = 'architecture' | 'structural' | 'plumbing' | 'electrical' | 'hvac' | 'other'

export type Discipline = { code: DisciplineCode; label: string }

export const DISCIPLINES: Discipline[] = [
  { code: 'architecture', label: '建筑' },
  { code: 'structural', label: '结构' },
  { code: 'plumbing', label: '给排水' },
  { code: 'electrical', label: '电气' },
  { code: 'hvac', label: '暖通' },
  { code: 'other', label: '其他' },
]

export const DISCIPLINE_CODES: DisciplineCode[] = DISCIPLINES.map(item => item.code)

export function disciplineLabel(code: string): string {
  return DISCIPLINES.find(item => item.code === code)?.label ?? code
}

export function isDisciplineCode(value: unknown): value is DisciplineCode {
  return typeof value === 'string' && DISCIPLINE_CODES.includes(value as DisciplineCode)
}

export type Project = { name: string; number: string; owner: string; location: string }

export function emptyProject(): Project {
  return { name: '', number: '', owner: '', location: '' }
}

export function projectFilled(project: Project): number {
  return [project.name, project.number, project.owner, project.location].filter(value => value.trim().length > 0).length
}

export const SEISMIC_INTENSITY_PENDING = '待核定'
export const SEISMIC_INTENSITIES = ['6度（0.05g）', '7度（0.10g）', '7度（0.15g）', '8度（0.20g）', '8度（0.30g）', '9度（0.40g）']

export type StructuralParams = {
  siteCategory: string
  seismicGrade: string
  safetyLevel: string
  foundationGrade: string
  designLifeYears: number
  corrosion: string
  protectionScheme: string
  protectionExtra: string
  seismicIntensity: string
}

export function defaultStructuralParams(): StructuralParams {
  return {
    siteCategory: '',
    seismicGrade: '',
    safetyLevel: '',
    foundationGrade: '',
    designLifeYears: 50,
    corrosion: '',
    protectionScheme: '',
    protectionExtra: '',
    seismicIntensity: '',
  }
}

export const SITE_CATEGORIES = ['I0', 'I1', 'II', 'III', 'IV']
export const SEISMIC_GRADES = ['甲', '乙', '丙', '丁']
export const SAFETY_LEVELS = ['一级', '二级', '三级']
export const FOUNDATION_GRADES = ['甲级', '乙级', '丙级']
export const PROTECTION_SCHEMES = ['弱', '中', '强']

export const STRUCTURAL_REQUIRED_FIELDS: { key: keyof StructuralParams; label: string }[] = [
  { key: 'siteCategory', label: '场地类别' },
  { key: 'seismicGrade', label: '抗震设防类别' },
  { key: 'safetyLevel', label: '结构安全等级' },
  { key: 'foundationGrade', label: '地基基础设计等级' },
  { key: 'protectionScheme', label: '材料/防腐方案' },
  { key: 'seismicIntensity', label: '抗震设防烈度' },
]

export type SectionDefinition = {
  id: string
  title: string
  body: string // 仅作为编辑提示；未核定的专业正文不预填到说明中。
  disciplines: DisciplineCode[]
  applicability: string
}

export type TemplateDefinition = {
  id: string
  name: string
  discipline: DisciplineCode
  custom: boolean
  sectionIds: string[]
}

const COMMON_SECTIONS: SectionDefinition[] = [
  { id: 'sec-overview', title: '工程概况', body: '概述工程用途、建设地点、规模与主要设计标准。', disciplines: DISCIPLINE_CODES, applicability: '所有专业' },
  { id: 'sec-basis', title: '设计依据', body: '列出本说明引用的设计依据，如设计任务书、勘察报告与主要标准规范。', disciplines: DISCIPLINE_CODES, applicability: '所有专业' },
  { id: 'sec-general-requirements', title: '通用设计要求', body: '说明本专业需要遵循的总体设计原则与主要技术要求。', disciplines: DISCIPLINE_CODES, applicability: '所有专业' },
  { id: 'sec-other', title: '其他说明', body: '补充上述章节未覆盖、但需要保留在说明中的内容。', disciplines: DISCIPLINE_CODES, applicability: '所有专业' },
]

const DISCIPLINE_SECTIONS: Record<Exclude<DisciplineCode, 'structural'>, SectionDefinition[]> = {
  architecture: [
    { id: 'sec-arch-overall', title: '建筑设计总体', body: '说明总体布局、功能分区、交通组织与竖向设计。', disciplines: ['architecture'], applicability: '建筑专业' },
    { id: 'sec-arch-fire', title: '防火与安全', body: '说明防火分区、疏散设计与建筑耐火等级。', disciplines: ['architecture'], applicability: '建筑专业' },
    { id: 'sec-arch-detail', title: '建筑构造', body: '说明墙体、屋面、门窗与主要部位构造做法。', disciplines: ['architecture'], applicability: '建筑专业' },
  ],
  plumbing: [
    { id: 'sec-plumb-supply', title: '给水设计', body: '说明给水系统形式、用水定额与管网布置。', disciplines: ['plumbing'], applicability: '给排水专业' },
    { id: 'sec-plumb-drainage', title: '排水设计', body: '说明排水体制、管网布置与污水处理途径。', disciplines: ['plumbing'], applicability: '给排水专业' },
    { id: 'sec-plumb-equipment', title: '设备与管材', body: '说明主要设备选型与管材选用。', disciplines: ['plumbing'], applicability: '给排水专业' },
  ],
  electrical: [
    { id: 'sec-elec-supply', title: '供配电', body: '说明负荷等级、供电电源与配电系统形式。', disciplines: ['electrical'], applicability: '电气专业' },
    { id: 'sec-elec-lighting', title: '照明', body: '说明照明标准、光源选择与应急照明。', disciplines: ['electrical'], applicability: '电气专业' },
    { id: 'sec-elec-protection', title: '防雷与接地', body: '说明防雷类别、接地系统与电气安全措施。', disciplines: ['electrical'], applicability: '电气专业' },
  ],
  hvac: [
    { id: 'sec-hvac-design', title: '暖通空调设计', body: '说明空调系统形式、冷热源与风系统布置。', disciplines: ['hvac'], applicability: '暖通专业' },
    { id: 'sec-hvac-ventilation', title: '通风与防排烟', body: '说明通风系统、防排烟设计与补风措施。', disciplines: ['hvac'], applicability: '暖通专业' },
    { id: 'sec-hvac-energy', title: '节能措施', body: '说明节能设计措施与能耗控制要求。', disciplines: ['hvac'], applicability: '暖通专业' },
  ],
  other: [],
}

const STRUCTURAL_SECTIONS: SectionDefinition[] = [
  { id: 'sec-struct-system', title: '结构体系与布置', body: '说明结构体系选型、主要构件布置与抗震缝、伸缩缝设置。', disciplines: ['structural'], applicability: '结构专业' },
  { id: 'sec-struct-foundation', title: '地基与基础', body: '说明地基处理方案、基础形式与主要设计参数。', disciplines: ['structural'], applicability: '结构专业' },
  { id: 'sec-struct-material', title: '主要结构材料', body: '说明混凝土、钢筋、钢材等主要材料的强度等级与性能要求。', disciplines: ['structural'], applicability: '结构专业' },
  { id: 'sec-struct-calculation', title: '结构计算', body: '说明主要计算内容、计算软件与荷载取值原则。', disciplines: ['structural'], applicability: '结构专业' },
  { id: 'sec-struct-detail', title: '构造要求', body: '说明构件最小尺寸、配筋构造与连接构造要求。', disciplines: ['structural'], applicability: '结构专业' },
  { id: 'sec-struct-pool', title: '池体结构', body: '说明水池结构形式、抗渗抗浮与变形缝构造。', disciplines: ['structural'], applicability: '含水池工程' },
  { id: 'sec-struct-frame', title: '框架结构', body: '说明框架结构布置、梁柱截面与节点构造。', disciplines: ['structural'], applicability: '含框架工程' },
  { id: 'sec-struct-steel', title: '钢结构设计', body: '说明钢结构形式、构件截面与连接节点设计。', disciplines: ['structural'], applicability: '钢结构工程' },
  { id: 'sec-struct-protection', title: '防腐与防火', body: '结合水土腐蚀性结论，说明材料选择与防腐、防火措施。', disciplines: ['structural'], applicability: '腐蚀环境或钢结构' },
]

export const SECTION_LIBRARY: SectionDefinition[] = [...COMMON_SECTIONS, ...DISCIPLINE_SECTIONS.architecture, ...DISCIPLINE_SECTIONS.plumbing, ...DISCIPLINE_SECTIONS.electrical, ...DISCIPLINE_SECTIONS.hvac, ...STRUCTURAL_SECTIONS]

export function librarySections(discipline: DisciplineCode): SectionDefinition[] {
  return SECTION_LIBRARY.filter(section => section.disciplines.includes(discipline))
}

export function findSectionDefinition(id: string): SectionDefinition | undefined {
  return SECTION_LIBRARY.find(section => section.id === id)
}

export function sectionTitle(id: string): string {
  return findSectionDefinition(id)?.title ?? id
}

export const TEMPLATES: TemplateDefinition[] = [
  { id: 'tpl-struct-frame', name: '框架结构', discipline: 'structural', custom: false, sectionIds: ['sec-overview', 'sec-basis', 'sec-struct-system', 'sec-struct-foundation', 'sec-struct-material', 'sec-struct-calculation', 'sec-struct-detail', 'sec-other'] },
  { id: 'tpl-struct-pool', name: '水池结构', discipline: 'structural', custom: false, sectionIds: ['sec-overview', 'sec-basis', 'sec-struct-system', 'sec-struct-foundation', 'sec-struct-material', 'sec-struct-pool', 'sec-struct-detail', 'sec-other'] },
  { id: 'tpl-struct-pool-frame', name: '水池＋框架', discipline: 'structural', custom: false, sectionIds: ['sec-overview', 'sec-basis', 'sec-struct-system', 'sec-struct-foundation', 'sec-struct-material', 'sec-struct-pool', 'sec-struct-frame', 'sec-struct-detail', 'sec-other'] },
  { id: 'tpl-struct-steel', name: '钢结构', discipline: 'structural', custom: false, sectionIds: ['sec-overview', 'sec-basis', 'sec-struct-system', 'sec-struct-material', 'sec-struct-steel', 'sec-struct-protection', 'sec-struct-calculation', 'sec-struct-detail', 'sec-other'] },
  { id: 'tpl-arch-standard', name: '厂房建筑（候选）', discipline: 'architecture', custom: false, sectionIds: ['sec-overview', 'sec-basis', 'sec-arch-overall', 'sec-arch-fire', 'sec-arch-detail', 'sec-general-requirements', 'sec-other'] },
  { id: 'tpl-plumb-standard', name: '水处理工艺总图（候选）', discipline: 'plumbing', custom: false, sectionIds: ['sec-overview', 'sec-basis', 'sec-plumb-supply', 'sec-plumb-drainage', 'sec-plumb-equipment', 'sec-general-requirements', 'sec-other'] },
  { id: 'tpl-elec-standard', name: '电气总图（候选）', discipline: 'electrical', custom: false, sectionIds: ['sec-overview', 'sec-basis', 'sec-elec-supply', 'sec-elec-lighting', 'sec-elec-protection', 'sec-general-requirements', 'sec-other'] },
  { id: 'tpl-hvac-standard', name: '暖通设计（候选）', discipline: 'hvac', custom: false, sectionIds: ['sec-overview', 'sec-basis', 'sec-hvac-design', 'sec-hvac-ventilation', 'sec-hvac-energy', 'sec-general-requirements', 'sec-other'] },
  { id: 'tpl-other-standard', name: '通用组合', discipline: 'other', custom: false, sectionIds: ['sec-overview', 'sec-basis', 'sec-general-requirements', 'sec-other'] },
]

export function templatesFor(discipline: DisciplineCode): TemplateDefinition[] {
  const presets = TEMPLATES.filter(template => template.discipline === discipline && !template.custom)
  const custom: TemplateDefinition = { id: `tpl-${discipline}-custom`, name: '自定义组合', discipline, custom: true, sectionIds: [] }
  return [...presets, custom]
}

export function customTemplateId(discipline: DisciplineCode): string {
  return `tpl-${discipline}-custom`
}

export type LayoutBlock = { kind: 'paragraph'; moduleId: string } | { kind: 'table'; rows: string[][]; sourcePara: number; reviewNote: string; confirmedForNote?: boolean }
export type NoteSection = { id: string; title: string; body: string; custom: boolean; modules?: SelectedModule[]; layoutBlocks?: LayoutBlock[] }

export type Note = {
  id: string
  title: string
  discipline: DisciplineCode
  project: Project
  structural: StructuralParams | null
  templateId: string
  templateVersion: string
  sections: NoteSection[]
  fieldValues: Record<string, string>
  fieldDefinitions: Record<string, { label: string; unit: string }>
  assemblyPackageId?: string
  assemblyReviewConfirmed?: boolean
  createdAt: string
  updatedAt: string
}

export type NoteSummary = {
  id: string
  title: string
  discipline: DisciplineCode
  templateId: string
  sectionCount: number
  filledCount: number
  createdAt: string
  updatedAt: string
}

export type NoteIssue = { level: 'error' | 'warning'; field?: string; sectionId?: string; message: string }

export function newId(): string {
  const cryptoRef = globalThis.crypto
  if (cryptoRef && typeof cryptoRef.randomUUID === 'function') return cryptoRef.randomUUID()
  return 'id-' + Date.now().toString(36) + '-' + Math.random().toString(36).slice(2, 10)
}

export function nowIso(): string {
  return new Date().toISOString()
}

export function defaultTitle(discipline: DisciplineCode): string {
  return disciplineLabel(discipline) + '设计说明'
}

export function createNote(discipline: DisciplineCode, templateId: string, project: Project, title?: string): Note {
  const template = TEMPLATES.find(item => item.id === templateId)
  const sectionIds = template && !template.custom ? template.sectionIds : []
  const stamp = nowIso()
  return {
    id: newId(),
    title: title && title.trim().length > 0 ? title.trim() : defaultTitle(discipline),
    discipline,
    project: { ...project },
    structural: discipline === 'structural' ? defaultStructuralParams() : null,
    templateId,
    templateVersion: LIBRARY_VERSION,
    sections: sectionIds.map(sectionId => {
      const definition = findSectionDefinition(sectionId)
      return { id: definition ? definition.id : sectionId, title: definition ? definition.title : sectionId, body: '', custom: false }
    }),
    fieldValues: {},
    fieldDefinitions: {},
    assemblyPackageId: '',
    assemblyReviewConfirmed: false,
    createdAt: stamp,
    updatedAt: stamp,
  }
}

export function rebuildSections(note: Note, templateId: string): NoteSection[] {
  const template = TEMPLATES.find(item => item.id === templateId)
  const sectionIds = template && !template.custom ? template.sectionIds : []
  return sectionIds.map(sectionId => {
    const definition = findSectionDefinition(sectionId)
    return { id: definition ? definition.id : sectionId, title: definition ? definition.title : sectionId, body: '', custom: false }
  })
}

export function resetSectionBody(section: NoteSection): string {
  return ''
}

export function setFieldValue(note: Note, fieldId: string, value: string): Note {
  return {
    ...note,
    fieldValues: { ...note.fieldValues, [fieldId]: value },
    assemblyReviewConfirmed: false,
    sections: note.sections.map(section => ({
      ...section,
      modules: section.modules?.map(module => module.fieldIds.includes(fieldId) ? { ...module, confirmedForNote: false } : module),
    })),
  }
}

export function effectiveFieldValues(note: Note): Record<string, string> {
  return {
    ...note.fieldValues,
    project_name: note.project.name,
    project_location: note.project.location,
    ...(note.structural ? {
      structural_safety_level: note.structural.safetyLevel,
      foundation_design_grade: note.structural.foundationGrade,
      seismic_intensity: note.structural.seismicIntensity,
      site_class: note.structural.siteCategory,
      design_life: `${note.structural.designLifeYears}年`,
      seismic_fortification_category: note.structural.seismicGrade,
    } : {}),
  }
}

export function setModuleTemplate(note: Note, sectionId: string, moduleId: string, template: string): Note {
  return {
    ...note,
    assemblyReviewConfirmed: false,
    sections: note.sections.map(section => section.id !== sectionId ? section : {
      ...section,
      modules: section.modules?.map(module => module.id !== moduleId ? module : {
        ...module,
        template,
        fieldIds: [...new Set([...template.matchAll(/\{([a-z][a-z0-9_]*)\}/g)].map(match => match[1]))],
        edited: template !== module.baseTemplate,
        confirmedForNote: false,
      }),
    }),
  }
}

export function isSectionEmpty(section: NoteSection): boolean {
  if ((section.layoutBlocks?.length ?? 0) > 0) return false
  if ((section.modules?.length ?? 0) > 0) return false
  const body = section.body.trim()
  // 旧版草稿把提示文案预填到了正文；精确匹配时按空章处理，避免误导出。
  return body.length === 0 || (!section.custom && body === findSectionDefinition(section.id)?.body.trim())
}

export function sectionFilledCount(note: Note): number {
  return note.sections.filter(section => !isSectionEmpty(section)).length
}

export function summarizeNote(note: Note): NoteSummary {
  return {
    id: note.id,
    title: note.title,
    discipline: note.discipline,
    templateId: note.templateId,
    sectionCount: note.sections.length,
    filledCount: sectionFilledCount(note),
    createdAt: note.createdAt,
    updatedAt: note.updatedAt,
  }
}

export function findIssues(note: Note): NoteIssue[] {
  const issues: NoteIssue[] = []
  const assembled = Boolean(note.assemblyPackageId)
  if (assembled && !note.assemblyReviewConfirmed) issues.push({ level: 'error', message: '请核对自动生成的整篇说明及规范引用，再确认适用于本工程。' })
  if (assembled) issues.push({ level: 'warning', message: '自动初稿来自旧工程候选资料；规范版本与适用条件尚未逐项核定。' })
  if (note.title.trim().length === 0) issues.push({ level: 'error', field: 'title', message: '请填写说明标题。' })
  if (note.project.name.trim().length === 0) issues.push({ level: 'error', field: 'project.name', message: '请填写工程名称。' })
  if (note.discipline === 'structural') {
    const structural = note.structural ?? defaultStructuralParams()
    for (const required of STRUCTURAL_REQUIRED_FIELDS) {
      if (String(structural[required.key] ?? '').trim().length === 0 || (required.key === 'seismicIntensity' && structural.seismicIntensity === SEISMIC_INTENSITY_PENDING)) {
        issues.push({ level: 'error', field: `structural.${required.key}`, message: `结构参数缺少「${required.label}」，请回到 01 步补齐。` })
      }
    }
  }
  if (note.sections.length === 0) {
    issues.push({ level: 'error', message: '还没有选择任何章节，请回到 02A 选择模板或自定义组合。' })
  }
  const filled = note.sections.filter(section => !isSectionEmpty(section))
  if (note.sections.length > 0 && filled.length === 0) {
    issues.push({ level: 'error', message: '所有章节都是空的，请至少填写一个章节正文。' })
  }
  for (const section of note.sections) {
    for (const block of section.layoutBlocks ?? []) {
      if (block.kind === 'table' && block.rows.some(row => row.some(cell => cell.includes('【待核定：本工程取值】')))) issues.push({ level: 'error', sectionId: section.id, message: `章节「${section.title}」表格仍有本工程取值待填写。` })
      if (block.kind === 'table' && block.reviewNote.startsWith('条件') && !block.confirmedForNote) issues.push({ level: 'error', sectionId: section.id, message: `章节「${section.title}」第 ${block.sourcePara} 处表格的适用条件尚未确认。` })
    }
    for (const module of section.modules ?? []) {
      if (module.flags.includes('requires_rewrite') && !module.edited) issues.push({ level: 'error', sectionId: section.id, message: `章节「${section.title}」第 ${module.sourceRefs[0]?.para ?? '?'} 段含旧工程事实，请改写。` })
      if (module.flags.includes('requires_applicability_review') && !module.confirmedForNote) issues.push({ level: 'error', sectionId: section.id, message: `章节「${section.title}」第 ${module.sourceRefs[0]?.para ?? '?'} 段的适用条件尚未确认。` })
      if (module.reviewStatus !== 'approved' && !assembled) issues.push({ level: 'warning', sectionId: section.id, message: `条款 ${module.clauseId} 来自旧资料候选，尚未批准为全局标准。` })
      if (module.template.trim().length === 0) issues.push({ level: 'error', sectionId: section.id, message: `条款 ${module.clauseId} 的文字为空。` })
      for (const fieldId of module.fieldIds) {
        if (!note.fieldDefinitions?.[fieldId]) issues.push({ level: 'error', sectionId: section.id, message: `条款 ${module.clauseId} 使用了未定义的占位符「${fieldId}」。` })
      }
      if (!module.confirmedForNote && !assembled) {
        issues.push({ level: 'error', sectionId: section.id, message: `章节「${section.title}」条款 ${module.clauseId} 尚未确认适用于本工程。` })
      }
      const labels = Object.fromEntries(Object.entries(note.fieldDefinitions ?? {}).map(([id, definition]) => [id, definition.label]))
      const rendered = renderModule(module, effectiveFieldValues(note), labels)
      for (const fieldId of rendered.missing) {
        issues.push({ level: 'error', sectionId: section.id, field: fieldId, message: `章节「${section.title}」缺少「${note.fieldDefinitions?.[fieldId]?.label ?? fieldId}」。` })
      }
    }
    if (isSectionEmpty(section)) {
      issues.push({ level: 'warning', sectionId: section.id, message: `章节「${section.title}」为空，导出时不会写入。` })
    }
  }
  return issues
}

export function hasBlockingIssue(note: Note): boolean {
  return findIssues(note).some(issue => issue.level === 'error')
}

export type DocumentBlock =
  | { kind: 'title'; text: string }
  | { kind: 'meta'; text: string }
  | { kind: 'heading'; text: string }
  | { kind: 'paragraph'; text: string }
  | { kind: 'table'; rows: string[][]; reviewNote: string }

export type BuiltDocument = { blocks: DocumentBlock[]; notices: string[] }

export function buildDocument(note: Note): BuiltDocument {
  const blocks: DocumentBlock[] = []
  const notices: string[] = []
  const sourceLayout = note.sections.some(section => section.layoutBlocks?.length)
  blocks.push({ kind: 'title', text: note.title.trim() })
  if (sourceLayout) blocks.push({ kind: 'meta', text: '资料状态：旧工程候选文字，项目取值、规范版本与适用性须由设计人员核定。' })
  else if (note.assemblyPackageId) blocks.push({ kind: 'meta', text: '资料状态：旧工程候选初稿，规范版本与适用性待核定' })
  if (!sourceLayout) blocks.push({ kind: 'meta', text: '专业：' + disciplineLabel(note.discipline) })
  const project = note.project
  if (!sourceLayout && project.name.trim().length > 0) blocks.push({ kind: 'meta', text: '工程名称：' + project.name.trim() })
  if (!sourceLayout && project.number.trim().length > 0) blocks.push({ kind: 'meta', text: '工程编号：' + project.number.trim() })
  if (!sourceLayout && project.owner.trim().length > 0) blocks.push({ kind: 'meta', text: '建设单位：' + project.owner.trim() })
  if (!sourceLayout && project.location.trim().length > 0) blocks.push({ kind: 'meta', text: '建设地点：' + project.location.trim() })
  if (note.discipline === 'structural' && note.structural && !sourceLayout) {
    const structural = note.structural
    blocks.push({ kind: 'heading', text: '结构设计参数' })
    blocks.push({ kind: 'paragraph', text: '场地类别：' + structural.siteCategory.trim() })
    blocks.push({ kind: 'paragraph', text: '抗震设防类别：' + structural.seismicGrade.trim() + '（设防烈度：' + (structural.seismicIntensity.trim() || SEISMIC_INTENSITY_PENDING) + '）' })
    blocks.push({ kind: 'paragraph', text: '结构安全等级：' + structural.safetyLevel.trim() })
    blocks.push({ kind: 'paragraph', text: '地基基础设计等级：' + structural.foundationGrade.trim() })
    blocks.push({ kind: 'paragraph', text: '设计使用年限：' + String(structural.designLifeYears || 50) + ' 年' })
    if (structural.corrosion.trim().length > 0) blocks.push({ kind: 'paragraph', text: '水土腐蚀性：' + structural.corrosion.trim() })
    const scheme = structural.protectionScheme.trim()
    const extra = structural.protectionExtra.trim()
    blocks.push({ kind: 'paragraph', text: '材料与防腐方案：' + scheme + (extra.length > 0 ? '（附加措施：' + extra + '）' : '') })
  }
  for (const section of note.sections) {
    if (isSectionEmpty(section)) {
      notices.push(`章节「${section.title}」为空，未写入文档。`)
      continue
    }
    blocks.push({ kind: 'heading', text: section.title.trim() })
    const labels = Object.fromEntries(Object.entries(note.fieldDefinitions ?? {}).map(([id, definition]) => [id, definition.label]))
    if (section.layoutBlocks?.length) {
      const byId = new Map((section.modules ?? []).map(module => [module.id, module]))
      for (const block of section.layoutBlocks) {
        if (block.kind === 'table') blocks.push({ kind: 'table', rows: block.rows, reviewNote: block.reviewNote })
        else {
          const module = byId.get(block.moduleId)
          if (module) blocks.push({ kind: 'paragraph', text: renderModule(module, effectiveFieldValues(note), labels).text })
        }
      }
    } else for (const module of section.modules ?? []) {
      const rendered = renderModule(module, effectiveFieldValues(note), labels)
      blocks.push({ kind: 'paragraph', text: rendered.text })
    }
    const lines = section.body.replace(/\r\n/g, '\n').replace(/\r/g, '\n').split('\n').filter(line => line.trim().length > 0)
    for (const line of lines) blocks.push({ kind: 'paragraph', text: line })
  }
  return { blocks, notices }
}

export type ExportRequest = {
  operation: 'generate'
  path: string
  document: {
    title: string
    discipline: string
    project: { name: string; number: string; owner: string; location: string }
    structural: {
      siteCategory: string
      seismicGrade: string
      safetyLevel: string
      foundationGrade: string
      designLifeYears: number
      corrosion: string
      protectionScheme: string
      protectionExtra: string
      seismicIntensity: string
    } | null
    sections: { title: string; body: string; blocks?: ({ kind: 'paragraph'; text: string } | { kind: 'table'; rows: string[][] })[] }[]
    layoutMode?: 'source'
  }
}

export function toExportRequest(note: Note, path: string): ExportRequest {
  if (note.sections.some(section => (section.modules?.length ?? 0) > 0)) {
    const blocking = findIssues(note).filter(issue => issue.level === 'error')
    if (blocking.length > 0) throw new Error(blocking[0].message)
  }
  const document = buildDocument(note)
  const sections: { title: string; body: string; blocks?: ({ kind: 'paragraph'; text: string } | { kind: 'table'; rows: string[][] })[] }[] = []
  for (const block of document.blocks) {
    if (block.kind !== 'heading') continue
    if (block.text === '结构设计参数') continue
    const index = document.blocks.indexOf(block)
    const body: string[] = []
    const sectionBlocks: ({ kind: 'paragraph'; text: string } | { kind: 'table'; rows: string[][] })[] = []
    for (let cursor = index + 1; cursor < document.blocks.length; cursor += 1) {
      const next = document.blocks[cursor]
      if (next.kind === 'heading' || next.kind === 'title') break
      if (next.kind === 'paragraph') { body.push(next.text); sectionBlocks.push({ kind: 'paragraph', text: next.text }) }
      if (next.kind === 'table') sectionBlocks.push({ kind: 'table', rows: next.rows })
    }
    sections.push({ title: block.text, body: body.join('\n'), blocks: sectionBlocks })
  }
  return {
    operation: 'generate',
    path,
    document: {
      title: note.title.trim(),
      discipline: note.discipline,
      project: {
        name: note.project.name.trim(),
        number: note.project.number.trim(),
        owner: note.project.owner.trim(),
        location: note.project.location.trim(),
      },
      structural: note.discipline === 'structural' && note.structural
        ? {
            siteCategory: note.structural.siteCategory.trim(),
            seismicGrade: note.structural.seismicGrade.trim(),
            safetyLevel: note.structural.safetyLevel.trim(),
            foundationGrade: note.structural.foundationGrade.trim(),
            designLifeYears: note.structural.designLifeYears || 50,
            corrosion: note.structural.corrosion.trim(),
            protectionScheme: note.structural.protectionScheme.trim(),
            protectionExtra: note.structural.protectionExtra.trim(),
            seismicIntensity: note.structural.seismicIntensity.trim() || SEISMIC_INTENSITY_PENDING,
          }
        : null,
      sections,
      ...(note.sections.some(section => section.layoutBlocks?.length) ? { layoutMode: 'source' as const } : {}),
    },
  }
}

function asString(value: unknown, fallback: string): string {
  return typeof value === 'string' ? value : fallback
}

function asNumber(value: unknown, fallback: number): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback
}

export function parseNote(value: unknown): Note {
  if (!value || typeof value !== 'object') throw new Error('说明数据为空或格式不正确。')
  const raw = value as Record<string, unknown>
  const discipline = raw.discipline
  if (!isDisciplineCode(discipline)) throw new Error('说明数据的专业无效。')
  const projectRaw = (raw.project ?? {}) as Record<string, unknown>
  const sectionsRaw = Array.isArray(raw.sections) ? raw.sections : []
  const sections: NoteSection[] = sectionsRaw.map(item => {
    const section = (item ?? {}) as Record<string, unknown>
    const parsed: NoteSection = {
      id: asString(section.id, newId()),
      title: asString(section.title, '未命名章节'),
      body: asString(section.body, ''),
      custom: section.custom === true,
      ...(Array.isArray(section.modules) ? { modules: section.modules.map(value => {
        const module = (value ?? {}) as Record<string, unknown>
        return {
          id: asString(module.id, newId()),
          clauseId: asString(module.clauseId, ''),
          packageId: asString(module.packageId, ''),
          template: asString(module.template, ''),
          baseTemplate: asString(module.baseTemplate, asString(module.template, '')),
          edited: module.edited === true,
          fieldIds: Array.isArray(module.fieldIds) ? module.fieldIds.filter((id): id is string => typeof id === 'string') : [],
          sourceRefs: Array.isArray(module.sourceRefs) ? module.sourceRefs.filter((source): source is { sourceId: string; para: number; file?: string } => !!source && typeof source === 'object' && typeof source.sourceId === 'string' && Number.isInteger(source.para)).map(source => ({ sourceId: source.sourceId, para: source.para, file: typeof source.file === 'string' ? source.file : undefined })) : [],
          refs: Array.isArray(module.refs) ? module.refs.filter((ref): ref is string => typeof ref === 'string') : [],
          flags: Array.isArray(module.flags) ? module.flags.filter((flag): flag is string => typeof flag === 'string') : [],
          reviewStatus: asString(module.reviewStatus, 'pending'),
          confirmedForNote: module.confirmedForNote === true,
        }
      }) } : {}),
      ...(Array.isArray(section.layoutBlocks) ? { layoutBlocks: section.layoutBlocks.flatMap<LayoutBlock>(value => {
        const block = (value ?? {}) as Record<string, unknown>
        if (block.kind === 'paragraph' && typeof block.moduleId === 'string') return [{ kind: 'paragraph' as const, moduleId: block.moduleId }]
        if (block.kind === 'table' && Array.isArray(block.rows) && block.rows.every(row => Array.isArray(row) && row.every(cell => typeof cell === 'string'))) return [{ kind: 'table' as const, rows: block.rows as string[][], sourcePara: asNumber(block.sourcePara, 0), reviewNote: asString(block.reviewNote, '待核定'), confirmedForNote: block.confirmedForNote === true }]
        return []
      }) } : {}),
    }
    if (isSectionEmpty(parsed)) parsed.body = ''
    return parsed
  })
  let structural: StructuralParams | null = null
  if (discipline === 'structural') {
    const base = defaultStructuralParams()
    const rawStructural = (raw.structural ?? {}) as Record<string, unknown>
    structural = {
      siteCategory: asString(rawStructural.siteCategory, base.siteCategory),
      seismicGrade: asString(rawStructural.seismicGrade, base.seismicGrade),
      safetyLevel: asString(rawStructural.safetyLevel, base.safetyLevel),
      foundationGrade: asString(rawStructural.foundationGrade, base.foundationGrade),
      designLifeYears: asNumber(rawStructural.designLifeYears, base.designLifeYears),
      corrosion: asString(rawStructural.corrosion, base.corrosion),
      protectionScheme: asString(rawStructural.protectionScheme, base.protectionScheme),
      protectionExtra: asString(rawStructural.protectionExtra, base.protectionExtra),
      seismicIntensity: asString(rawStructural.seismicIntensity, base.seismicIntensity),
    }
  }
  return {
    id: asString(raw.id, newId()),
    title: asString(raw.title, ''),
    discipline,
    project: {
      name: asString(projectRaw.name, ''),
      number: asString(projectRaw.number, ''),
      owner: asString(projectRaw.owner, ''),
      location: asString(projectRaw.location, ''),
    },
    structural,
    templateId: asString(raw.templateId, ''),
    templateVersion: asString(raw.templateVersion, LIBRARY_VERSION),
    sections,
    fieldValues: stringRecord(raw.fieldValues),
    fieldDefinitions: definitionRecord(raw.fieldDefinitions),
    assemblyPackageId: asString(raw.assemblyPackageId, ''),
    assemblyReviewConfirmed: raw.assemblyReviewConfirmed === true,
    createdAt: asString(raw.createdAt, nowIso()),
    updatedAt: asString(raw.updatedAt, nowIso()),
  }
}

function stringRecord(value: unknown): Record<string, string> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return {}
  return Object.fromEntries(Object.entries(value).filter(([key, item]) => /^[a-z][a-z0-9_]*$/.test(key) && typeof item === 'string')) as Record<string, string>
}

function definitionRecord(value: unknown): Record<string, { label: string; unit: string }> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return {}
  const result: Record<string, { label: string; unit: string }> = {}
  for (const [key, item] of Object.entries(value)) {
    if (!/^[a-z][a-z0-9_]*$/.test(key) || !item || typeof item !== 'object') continue
    const definition = item as Record<string, unknown>
    result[key] = { label: asString(definition.label, key), unit: asString(definition.unit, '') }
  }
  return result
}

export function serializeNote(note: Note): string {
  return JSON.stringify(note, null, 2)
}

export function deserializeNote(json: string): Note {
  let value: unknown
  try {
    value = JSON.parse(json)
  } catch {
    throw new Error('说明文件不是有效的 JSON。')
  }
  return parseNote(value)
}

export type StoredProject = Project & { id: string; updatedAt: string }

export function parseProject(value: unknown): StoredProject {
  if (!value || typeof value !== 'object') throw new Error('项目数据为空或格式不正确。')
  const raw = value as Record<string, unknown>
  return {
    id: asString(raw.id, newId()),
    name: asString(raw.name, ''),
    number: asString(raw.number, ''),
    owner: asString(raw.owner, ''),
    location: asString(raw.location, ''),
    updatedAt: asString(raw.updatedAt, nowIso()),
  }
}
