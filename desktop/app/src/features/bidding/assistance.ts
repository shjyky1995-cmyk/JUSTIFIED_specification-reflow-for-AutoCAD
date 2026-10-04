import { newRequirement, parseBid, type BidProject, type Requirement, type RequirementKind } from './model.ts'

// 本地关键词辅助，只提出候选。原文中的例外、否定和条件不删改，不推定满足要求。
const categories: { kind: RequirementKind; words: string[] }[] = [
  { kind: '评分', words: ['评分', '得分', '加分', '分值', '满分', '评审因素'] },
  { kind: '资格', words: ['资格', '资质', '注册证', '职称', '业绩', '项目负责人', '营业执照', '联合体'] },
  { kind: '成果', words: ['成果', '勘察报告', '设计文件', '设计图纸', '提交图纸', '设计深度'] },
  { kind: '格式', words: ['暗标', '字体', '字号', '页边距', '页数', '编制格式', '装订', '匿名', '封面'] },
  { kind: '提交事项', words: ['截止', '递交', '上传', '投标保证金', '签章', '签字', '密封', '投标有效期'] },
  { kind: '实质要求', words: ['必须', '应当', '不得', '须提供', '应提供', '应满足', '不接受', '否决', '服务期限', '工期', '报价', '应包含', '应包括'] },
]
export type RequirementCandidate = { index: number; sourceId: string; sourceHash: string; location: string; excerpt: string; title: string; kind: RequirementKind; reasons: string[]; alreadyAdded: boolean }
export function requirementCandidates(bid: BidProject, sourceId: string) {
  const source = bid.sources.find(s => s.id === sourceId)
  if (!source) return { candidates: [], notices: ['请先选择资料。'], examined: 0 }
  if (source.kind === '证明材料') return { candidates: [], notices: ['证明材料用于佐证，不能自动当作招标要求；请从招标文件或补遗提取。'], examined: 0 }
  const candidates: RequirementCandidate[] = [], notices: string[] = []
  if (!source.blocks.length) notices.push('没有可供提取的文字；PDF/扫描件请人工摘录，或导入带正文的 DOCX/TXT。')
  if (source.warnings.length) notices.push('原资料有解析提示，请同时核对原件；候选清单可能不完整。')
  source.blocks.forEach((block, index) => {
    const hits = categories.map(c => ({ ...c, matched: c.words.filter(w => block.text.includes(w)) })).filter(c => c.matched.length)
    if (!hits.length) return
    if (block.text.length > 20_000) { notices.push(`${block.location}超过单条摘录限额，请人工拆分；本次未截断加入。`); return }
    candidates.push({ index, sourceId, sourceHash: source.hash, location: block.location, excerpt: block.text, title: block.text.replace(/\s+/g,' ').trim().slice(0, 80), kind: hits[0].kind, reasons: [...new Set(hits.flatMap(h => h.matched))], alreadyAdded: bid.requirements.some(r => r.sourceId === sourceId && r.location === block.location && r.excerpt === block.text) })
  })
  return { candidates, notices, examined: source.blocks.length }
}
export function adoptCandidates(bid: BidProject, sourceId: string, sourceHash: string, indices: number[]): BidProject {
  const source = bid.sources.find(s => s.id === sourceId)
  if (!source || source.hash !== sourceHash) throw new Error('资料已变化，请重新提取候选。')
  const report = requirementCandidates(bid, sourceId), chosen = [...new Set(indices)]
  if (!chosen.length) throw new Error('请先勾选要加入的候选。')
  const selected = chosen.map(index => {
    const candidate = report.candidates.find(c => c.index === index)
    if (!candidate) throw new Error('候选已失效，请重新提取。')
    return candidate
  }).filter(c => !c.alreadyAdded)
  if (selected.length + bid.requirements.length > 500) throw new Error('本标段要求最多500项，请先合并或拆分。')
  return parseBid({ ...bid, requirementsReviewed: selected.length ? false : bid.requirementsReviewed,
    requirements: [...bid.requirements, ...selected.map(c => ({ ...newRequirement(), title: c.title, kind: c.kind, sourceId: c.sourceId, location: c.location, excerpt: c.excerpt, mandatory: false }))] })
}
const responseTopics = [
  ['人员', '负责人', '资质', '资格', '证书'], ['业绩', '证明材料'], ['勘察', '方法', '设备'],
  ['设计', '方案', '专业', '协调'], ['成果', '提交', '进度', '期限'], ['质量', '校审', '安全'],
  ['报价', '投资', '费用'], ['授权', '投标函'], ['承诺', '偏差'],
]
export function suggestResponses(bid: BidProject, requirement: Requirement) {
  const text = requirement.title + ' ' + requirement.excerpt
  return bid.sections.map(section => {
    const matches = responseTopics.filter(topic => topic.some(w => text.includes(w)) && topic.some(w => section.title.includes(w)))
    const reasons = [...new Set(matches.flatMap(topic => topic.filter(w => text.includes(w))))]
    return { sectionId: section.id, title: section.title, reasons, score: matches.length }
  }).filter(s => s.score > 0 && !requirement.sectionIds.includes(s.sectionId)).sort((a,b) => b.score - a.score).slice(0, 4)
}
