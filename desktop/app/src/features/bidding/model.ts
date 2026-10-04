// 工程投标独立模型；不使用 CAD 文档协议，不包含 AI 生成的事实。
export const BID_TYPES = { survey: '勘察', design: '设计', combined: '勘察设计联合' } as const
export type BidType = keyof typeof BID_TYPES
export const REQUIREMENT_TYPES = ['资格', '评分', '实质要求', '成果', '格式', '提交事项'] as const
export type RequirementKind = typeof REQUIREMENT_TYPES[number]
export type SourceBlock = { location: string; text: string }
export type BidSource = {
  id: string; name: string; asset: string; hash: string; bytes: number; importedAt: string
  kind: '招标文件' | '补遗答疑' | '证明材料'; version: string; supersedes: string
  blocks: SourceBlock[]; warnings: string[]; confirmed: boolean
}
export type Requirement = {
  id: string; title: string; kind: RequirementKind; sourceId: string; location: string; excerpt: string
  score: string; mandatory: boolean; applicable: boolean; reason: string; confirmed: boolean
  sectionIds: string[]; evidenceIds: string[]; responseReviewed: boolean
}
export type Evidence = { id: string; title: string; kind: string; holder: string; validUntil: string; noExpiry: boolean; scope: string; sourceId: string; verified: boolean }
export type BidSection = { id: string; title: string; category: string; body: string; tables: { id: string; rows: string[][] }[]; reviewed: boolean }
export type BidProject = {
  schemaVersion: 1; id: string; revision: number; title: string; type: BidType; createdAt: string; updatedAt: string
  project: { name: string; number: string; owner: string; location: string; linkedId: string }
  lot: string; bidder: string; deadline: string; period: string; lead: string; consortium: boolean; members: string
  quote: string; quoteUnit: string; taxNote: string; requirementsReviewed: boolean
  sources: BidSource[]; requirements: Requirement[]; evidence: Evidence[]; sections: BidSection[]
  exports: { at: string; mode: 'draft' | 'reviewed'; fileName: string; revision: number }[]
}
export type BidSummary = { id: string; title: string; type: BidType; updatedAt: string; sections: number; issues: number; damaged?: boolean }
export type BidIssue = { page: 'project' | 'sources' | 'requirements' | 'evidence' | 'editor'; id?: string; message: string }
export type BidSave = { ok: true; revision: number; savedAt: string } | { ok: false; error: string }
export const bidId = () => globalThis.crypto.randomUUID()
export const isoNow = () => new Date().toISOString()

const surveyChapters = ['勘察范围与项目理解', '已有资料与现场条件', '勘察方法与工作安排', '勘察人员设备与质量安全', '勘察进度及成果提交', '勘察与设计配合']
const designChapters = ['设计范围与重点难点', '总体设计思路与方案比较', '专业设计方案与接口协调', '设计质量校审', '设计进度及成果清单', '投资控制与后续服务']
export function bidTemplate(type: BidType): BidSection[] {
  return [
    ...['投标函与授权', '企业资质与人员配置', '业绩与证明材料', '承诺与偏差说明'].map(title => ({ title, category: '商务资信' })),
    ...(type !== 'design' ? surveyChapters.map(title => ({ title, category: '勘察技术' })) : []),
    ...(type !== 'survey' ? designChapters.map(title => ({ title, category: '设计技术' })) : []),
    { title: '报价说明', category: '报价' },
  ].map(item => ({ ...item, id: bidId(), body: '', tables: [], reviewed: false }))
}
export function newBid(type: BidType, name = ''): BidProject {
  return { schemaVersion: 1, id: bidId(), revision: 0, title: name || `${BID_TYPES[type]}投标文件`, type, createdAt: isoNow(), updatedAt: isoNow(),
    project: { name: '', number: '', owner: '', location: '', linkedId: '' }, lot: '', bidder: '', deadline: '', period: '', lead: '', consortium: false, members: '',
    quote: '', quoteUnit: '元', taxNote: '', requirementsReviewed: false, sources: [], requirements: [], evidence: [], sections: bidTemplate(type), exports: [] }
}
export function newRequirement(): Requirement {
  return { id: bidId(), title: '', kind: '实质要求', sourceId: '', location: '', excerpt: '', score: '', mandatory: true, applicable: true, reason: '', confirmed: false, sectionIds: [], evidenceIds: [], responseReviewed: false }
}
export function newEvidence(): Evidence { return { id: bidId(), title: '', kind: '人员证书', holder: '', validUntil: '', noExpiry: false, scope: '', sourceId: '', verified: false } }

export function invalidateResponses(bid: BidProject, sectionId?: string): BidProject {
  return { ...bid, requirements: bid.requirements.map(r => !sectionId || r.sectionIds.includes(sectionId) ? { ...r, responseReviewed: false } : r) }
}
export function addSource(bid: BidProject, source: BidSource): BidProject {
  let next = { ...bid, sources: [...bid.sources, source], requirementsReviewed: false }
  if (source.kind === '补遗答疑') {
    next = invalidateResponses(next)
    next.sections = next.sections.map(section => ({ ...section, reviewed: false }))
    next.requirements = next.requirements.map(r => ({ ...r, confirmed: false }))
  }
  return next
}
export function reviewBid(bid: BidProject, today = new Date().toLocaleDateString('en-CA')): BidIssue[] {
  const issues: BidIssue[] = []
  const add = (page: BidIssue['page'], message: string, id?: string) => issues.push({ page, message, id })
  for (const [label, value] of [['标题', bid.title], ['项目名称', bid.project.name], ['招标人', bid.project.owner], ['投标单位', bid.bidder], ['标段（单标段也请注明）', bid.lot], ['服务期限', bid.period], ['项目负责人', bid.lead], ['截止时间', bid.deadline]]) if (!value.trim()) add('project', `待填写：${label}`)
  if (bid.consortium && !bid.members.trim()) add('project', '待填写联合体成员及分工')
  if (!bid.quote.trim() || !bid.quoteUnit.trim() || !bid.taxNote.trim()) add('project', '待填写报价、单位和含税口径；金额由人工核定')
  if (bid.quote && (!/^\d+(\.\d{1,4})?$/.test(bid.quote) || !Number.isFinite(Number(bid.quote)))) add('project', '报价须为非负有限数字，最多四位小数')
  if (!bid.sources.some(s => s.kind === '招标文件')) add('sources', '尚未导入招标文件')
  for (const source of bid.sources) if (!source.confirmed) add('sources', `资料尚待人工核对：${source.name}`, source.id)
  if (!bid.requirementsReviewed || !bid.requirements.length) add('requirements', '尚未确认本标段要求清单完整性')
  for (const r of bid.requirements) {
    if (!r.title.trim() || !r.sourceId || !r.location.trim() || !r.excerpt.trim() || !r.confirmed) add('requirements', `要求待核对依据：${r.title || '未命名要求'}`, r.id)
    if (r.score && (!/^\d+(\.\d+)?$/.test(r.score) || !Number.isFinite(Number(r.score)))) add('requirements', `分值无效：${r.title}`, r.id)
    if (!r.applicable) { if (!r.reason.trim()) add('requirements', `请说明不适用原因：${r.title}`, r.id); continue }
    if (!r.sectionIds.length || !r.responseReviewed) add('requirements', `尚未完成响应复核：${r.title}`, r.id)
    if (r.kind === '资格' && !r.evidenceIds.length) add('requirements', `资格要求缺少证明材料：${r.title}`, r.id)
  }
  for (const e of bid.evidence) {
    if (!e.title.trim() || !e.holder.trim() || !e.scope.trim() || !e.sourceId || !e.verified) add('evidence', `资料来源/适用范围待核定：${e.title || '未命名资料'}`, e.id)
    if (!e.noExpiry && !e.validUntil) add('evidence', `有效期不明：${e.title}`, e.id)
    const cutoff = bid.deadline ? bid.deadline.slice(0, 10) : today
    if (e.validUntil && (e.validUntil < today || e.validUntil < cutoff)) add('evidence', `已过期或投标截止前到期：${e.title}`, e.id)
  }
  if (!bid.sections.length) add('editor', '至少保留一个章节')
  for (const section of bid.sections) {
    if (!section.title.trim() || (!section.body.trim() && !section.tables.some(t => t.rows.some(row => row.some(cell => cell.trim()))))) add('editor', `章节待填写：${section.title || '未命名章节'}`, section.id)
    if (/【待(填写|核定)[^】]*】/.test(section.body + section.tables.flatMap(t => t.rows.flat()).join(' '))) add('editor', `章节仍有待填标记：${section.title}`, section.id)
    if (!section.reviewed) add('editor', `章节待复核：${section.title}`, section.id)
  }
  return issues
}

// 明确的 schema 校验；不能把 JSON 能读出来当作业务数据有效。
export function parseBid(value: unknown): BidProject {
  if (!value || typeof value !== 'object') throw new Error('投标数据无效。')
  const b = value as BidProject
  const text = (v: unknown, limit = 2000) => { if (typeof v !== 'string' || v.length > limit) throw new Error('投标字段类型或长度无效。') }
  const id = (v: unknown) => { text(v, 80); if (!/^[A-Za-z0-9_-]{1,80}$/.test(v as string)) throw new Error('投标编号无效。') }
  const bool = (v: unknown) => { if (typeof v !== 'boolean') throw new Error('投标状态无效。') }
  const list = (v: unknown, limit: number) => { if (!Array.isArray(v) || v.length > limit) throw new Error('投标列表数量无效。') }
  const date = (v: unknown, empty = false) => { text(v, 40); if (empty && !v) return; const s = v as string; if (!/^\d{4}-\d{2}-\d{2}(T.*)?$/.test(s) || !Number.isFinite(Date.parse(s)) || new Date(s.slice(0,10)).toISOString().slice(0,10) !== s.slice(0,10)) throw new Error('日期格式无效。') }
  if (b.schemaVersion !== 1 || !Object.hasOwn(BID_TYPES, b.type) || !Number.isSafeInteger(b.revision) || b.revision < 0) throw new Error('投标版本不受支持。')
  id(b.id); text(b.title, 160); date(b.createdAt); date(b.updatedAt)
  if (!b.project || typeof b.project !== 'object') throw new Error('项目资料无效。')
  for (const key of ['name', 'number', 'owner', 'location', 'linkedId'] as const) text(b.project[key], 300)
  for (const key of ['lot', 'bidder', 'period', 'lead', 'members', 'quote', 'quoteUnit', 'taxNote'] as const) text(b[key])
  date(b.deadline, true); bool(b.consortium); bool(b.requirementsReviewed)
  list(b.sources, 100); list(b.requirements, 500); list(b.evidence, 300); list(b.sections, 100); list(b.exports, 100)
  const unique = (items: { id: string }[]) => { const ids = new Set<string>(); for (const item of items) { id(item.id); if (ids.has(item.id)) throw new Error('编号重复。'); ids.add(item.id) }; return ids }
  const sources = unique(b.sources), sections = unique(b.sections), evidence = unique(b.evidence); unique(b.requirements)
  const ref = (v: string, ids: Set<string>) => { text(v, 80); if (v && !ids.has(v)) throw new Error('存在失效的资料/章节关联。') }
  for (const s of b.sources) {
    text(s.name, 260); text(s.asset, 80); text(s.hash, 64); text(s.version, 100); date(s.importedAt); bool(s.confirmed)
    if (!/^[a-f0-9]{64}\.(docx|pdf|png|jpg|jpeg|txt)$/.test(s.asset) || s.asset.split('.')[0] !== s.hash || !Number.isSafeInteger(s.bytes) || s.bytes < 0 || s.bytes > 20_000_000 || !['招标文件', '补遗答疑', '证明材料'].includes(s.kind)) throw new Error('资料文件信息无效。')
    ref(s.supersedes, sources); if (s.supersedes === s.id) throw new Error('补遗不能替代自身。')
    list(s.blocks, 3000); list(s.warnings, 100)
    for (const block of s.blocks) { text(block.location, 300); text(block.text, 50_000) }; for (const w of s.warnings) text(w, 1000)
  }
  for (const s of b.sections) {
    text(s.title, 160); text(s.category, 60); text(s.body, 100_000); bool(s.reviewed); list(s.tables, 30); unique(s.tables)
    for (const t of s.tables) { list(t.rows, 100); if (!t.rows.length) throw new Error('表格不能为空。'); const n = t.rows[0].length; if (n < 1 || n > 12) throw new Error('表格列数无效。'); for (const row of t.rows) { list(row, 12); if (row.length !== n) throw new Error('表格行列不一致。'); for (const cell of row) text(cell, 10_000) } }
  }
  for (const e of b.evidence) { for (const k of ['title','kind','holder','scope'] as const) text(e[k]); date(e.validUntil, true); bool(e.noExpiry); bool(e.verified); ref(e.sourceId, sources) }
  for (const r of b.requirements) {
    for (const k of ['title','location','excerpt','score','reason'] as const) text(r[k], k === 'excerpt' ? 20_000 : 2000)
    if (!(REQUIREMENT_TYPES as readonly string[]).includes(r.kind)) throw new Error('要求分类无效。')
    for (const k of ['mandatory','applicable','confirmed','responseReviewed'] as const) bool(r[k])
    ref(r.sourceId, sources); list(r.sectionIds, 100); list(r.evidenceIds, 300); r.sectionIds.forEach(v => { id(v); ref(v, sections) }); r.evidenceIds.forEach(v => { id(v); ref(v, evidence) })
  }
  for (const e of b.exports) { date(e.at); text(e.fileName, 300); if (!['draft','reviewed'].includes(e.mode) || !Number.isSafeInteger(e.revision)) throw new Error('导出记录无效。') }
  if (JSON.stringify(b).length > 2_000_000) throw new Error('单份投标资料文本超过限额，请拆分标段。')
  return structuredClone(b)
}

export function bidDocument(bid: BidProject, mode: 'draft' | 'reviewed') {
  const issues = reviewBid(bid)
  if (mode === 'reviewed' && issues.length) throw new Error(`仍有 ${issues.length} 项待完善，先导出草稿或完成核对。`)
  const filled = (value: string, label: string) => value.trim() || `【待填写：${label}】`
  return { title: filled(bid.title, '标题'), mode,
    meta: [`类型：${BID_TYPES[bid.type]}`, `项目：${filled(bid.project.name,'项目名称')}`, `编号：${bid.project.number || '未填写'}`, `招标人：${filled(bid.project.owner,'招标人')}`, `地点：${bid.project.location || '未填写'}`, `标段：${filled(bid.lot,'标段')}`, `投标单位：${filled(bid.bidder,'投标单位')}`, `负责人：${filled(bid.lead,'负责人')}`, `服务期限：${filled(bid.period,'服务期限')}`, `截止时间：${filled(bid.deadline,'截止时间')}`, `报价：${filled(bid.quote,'报价')} ${bid.quoteUnit}；${filled(bid.taxNote,'含税口径')}`, `联合体：${bid.consortium ? filled(bid.members,'成员及分工') : '否'}`],
    notices: mode === 'draft' ? ['编制草稿：待填项与资料适用性须人工核定。', ...issues.map(i => i.message)] : ['已完成本程序列出的人工核对；签章、电子提交和专业审核另行完成。'],
    sections: bid.sections.map(s => ({ title: filled(s.title, '章节标题'), blocks: [
      ...(s.body.trim() ? s.body : s.tables.length ? '' : filled(s.body, s.title + '正文')).split('\n').filter(Boolean).map(text => ({ kind: 'paragraph', text })),
      ...s.tables.map(t => ({ kind: 'table', rows: t.rows })),
    ] })),
    appendix: [
      { title: '要求响应对照（编制核对用）', rows: [['要求', '原文位置', '响应章节', '证明材料', '状态'], ...bid.requirements.map(r => [r.title, `${bid.sources.find(s => s.id === r.sourceId)?.name || '待补'} ${r.location}`, r.sectionIds.map(id => bid.sections.find(s => s.id === id)?.title).join('、'), r.evidenceIds.map(id => bid.evidence.find(e => e.id === id)?.title).join('、'), !r.applicable ? `不适用：${r.reason}` : r.responseReviewed ? '已人工复核' : '待复核'])] },
      { title: '证明附件清单（附件未嵌入本文件）', rows: [['资料', '持有人/单位', '有效期', '来源', '状态'], ...bid.evidence.map(e => [e.title, e.holder, e.noExpiry ? '无固定有效期（人工确认）' : e.validUntil || '待核定', bid.sources.find(s => s.id === e.sourceId)?.name || '待补', e.verified ? '已核定' : '待核定'])] },
    ] }
}
