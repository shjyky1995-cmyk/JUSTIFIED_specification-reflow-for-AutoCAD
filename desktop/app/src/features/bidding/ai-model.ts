import { newRequirement, parseBid, invalidateResponses, type BidProject, type RequirementKind, REQUIREMENT_TYPES } from './model.ts'
export type AiKind = 'requirements' | 'outline' | 'chapter'
export type PdfPageRange = { from: number; to: number }
export type AiSelection = { bidId: string; kind: AiKind; sourceIds: string[]; sectionId: string; pageRanges?: Record<string, PdfPageRange> }
export function pdfPhysicalPage(location: string): number | null { const match = /^PDF物理页 (\d+) \/ 文字块 \d+$/.exec(location); return match ? Number(match[1]) : null }
export type AiInput = { type: string; project: Record<string,string>; section: { id: string; title: string; body: string } | null; sources: { id: string; name: string; hash: string; blocks: { index: number; location: string; text: string }[] }[] }
export type AiReference = { sourceId: string; index: number; quote: string }
export type AiSourceStamp = { id: string; name: string; hash: string; kind: string; version: string; supersedes: string }
export type AiResult = { requirements: { title: string; kind: RequirementKind; reference: AiReference }[]; outline: { title: string; reference: AiReference }[]; chapter: { body: string; references: AiReference[] } | null }
export type AiJob = { id: string; bidId: string; kind: AiKind; model: string; state: 'running'|'succeeded'|'failed'|'canceled'|'interrupted'; createdAt: string; finishedAt: string; error: string; sourceIds: string[]; sectionId: string; pageRanges?: Record<string, PdfPageRange>; inputHash: string; sourceHashes: Record<string,string>; contextHash: string; sourceContext?: AiSourceStamp[]; reviewIssue?: string; result: AiResult | null; usage: { input: number; output: number; total: number } | null; applied: number[] }
export type AiSettings = { model: string; hasKey: boolean; encryptionAvailable: boolean }
export type AiPrepared = { token: string; provider: string; model: string; preview: string; characters: number; expiresAt: string }
// 核对状态不参与快照；所选资料、关联原版/补遗及未限定影响范围的补遗参与。
export function aiSourceContext(bid: BidProject, sourceIds: string[]): AiSourceStamp[] {
  const related = new Set(sourceIds)
  let count: number
  do {
    count = related.size
    for (const source of bid.sources) {
      if (related.has(source.id) && source.supersedes) related.add(source.supersedes)
      if (source.kind === '补遗答疑' && (!source.supersedes || related.has(source.supersedes))) related.add(source.id)
    }
  } while (count !== related.size)
  return bid.sources.filter(s => related.has(s.id)).map(({id,name,hash,kind,version,supersedes}) => ({id,name,hash,kind,version,supersedes})).sort((a,b) => a.id.localeCompare(b.id))
}
export function aiSourceIssue(bid: BidProject, job: AiJob): string {
  if (!job.sourceContext) return '旧任务未记录资料版本，请重新生成候选；原结果仍保留供查看。'
  if (JSON.stringify(aiSourceContext(bid,job.sourceIds)) !== JSON.stringify(job.sourceContext)) return '资料版本、类型或相关补遗已变化，请重新生成候选；原正文保留。'
  return ''
}
export function buildAiInput(bid: BidProject, selection: AiSelection): AiInput {
  if (selection.bidId !== bid.id || !['requirements','outline','chapter'].includes(selection.kind)) throw new Error('AI任务与当前标段不一致。')
  if (!Array.isArray(selection.sourceIds) || !selection.sourceIds.length || selection.sourceIds.length>10 || new Set(selection.sourceIds).size!==selection.sourceIds.length) throw new Error('请选择1至10份有正文的资料。')
  if (selection.pageRanges && (typeof selection.pageRanges !== 'object' || Array.isArray(selection.pageRanges) || Object.keys(selection.pageRanges).some(id => !selection.sourceIds.includes(id)))) throw new Error('PDF页范围与所选资料不一致。')
  if (selection.pageRanges && Object.values(selection.pageRanges).some(r => !r || typeof r !== 'object')) throw new Error('PDF页范围格式无效。')
  const sources=selection.sourceIds.map(id=>{
    const source=bid.sources.find(s=>s.id===id)
    if(!source || !source.blocks.length) throw new Error('所选资料没有可解析正文，请先人工摘录或更换文件。')
    if(source.kind==='证明材料') throw new Error('本阶段AI只处理招标文件和补遗；证明原件仍由人工核定。')
    const range=selection.pageRanges?.[id]
    if (range && (!source.asset.endsWith('.pdf') || !Number.isInteger(range.from) || !Number.isInteger(range.to) || range.from<1 || range.to<range.from || range.to>500)) throw new Error('PDF物理页范围须为1至500页内的起止整数。')
    const blocks=source.blocks.map((b,index)=>({index,location:b.location,text:b.text})).filter(b => { if (!range) return true; const page=pdfPhysicalPage(b.location); return page !== null && page>=range.from && page<=range.to })
    if (!blocks.length) throw new Error('所选PDF页范围没有可提取文字；请核对原件、页序或更换范围。')
    return {id:source.id,name:source.name,hash:source.hash,blocks}
  })
  const section=selection.kind==='chapter'?bid.sections.find(s=>s.id===selection.sectionId):null
  if(selection.kind==='chapter'&&!section) throw new Error('请选择需要起草的章节。')
  const input:AiInput={type:bid.type,project:selection.kind==='requirements'?{}:{name:bid.project.name,location:bid.project.location,lot:bid.lot,period:bid.period},section:section?{id:section.id,title:section.title,body:section.body}:null,sources}
  if(JSON.stringify(input).length>40_000) throw new Error('所选文字超过本轮4万字符限额，请减少文件或拆分资料后重试；没有自动截断。')
  return input
}
export function validateAiResult(value: unknown, kind: AiKind, input: AiInput): AiResult {
  const v=value as Record<string,unknown>
  if(!v||typeof v!=='object') throw new Error('AI回复结构无效。')
  const string=(v:unknown,max:number)=>{if(typeof v!=='string'||!v.trim()||v.length>max)throw new Error('AI回复字段为空或过长。');return v}
  const reference=(v:unknown):AiReference=>{
    const r=v as AiReference
    if(!r||typeof r.sourceId!=='string'||!Number.isInteger(r.index))throw new Error('AI来源格式无效。')
    const block=input.sources.find(s=>s.id===r.sourceId)?.blocks.find(b=>b.index===r.index)
    const quote=string(r.quote,20_000)
    if(!block||!block.text.includes(quote)) throw new Error('AI引用不在本次发送原文中，结果未采纳。')
    return {sourceId:r.sourceId,index:r.index,quote}
  }
  const result:AiResult={requirements:[],outline:[],chapter:null}
  if(kind==='requirements'){
    if(!Array.isArray(v.requirements)||v.requirements.length>80)throw new Error('AI要求数量无效。')
    result.requirements=v.requirements.map(r=>{if(!r||!REQUIREMENT_TYPES.includes(r.kind))throw new Error('AI要求分类无效。');return {title:string(r.title,160),kind:r.kind,reference:reference(r.reference)}})
  }else if(kind==='outline'){
    if(!Array.isArray(v.outline)||v.outline.length>30)throw new Error('AI目录数量无效。')
    result.outline=v.outline.map(r=>({title:string(r?.title,140),reference:reference(r?.reference)}))
  }else{
    const c=v.chapter as {body:unknown;references:unknown[]}
    if(!c||!Array.isArray(c.references)||!c.references.length||c.references.length>50)throw new Error('AI章节必须提供可核对来源。')
    result.chapter={body:string(c.body,30_000),references:c.references.map(reference)}
  }
  return result
}
export function applyAiResult(bid: BidProject, job: AiJob, indices: number[]): BidProject {
  if(job.bidId!==bid.id||job.state!=='succeeded'||!job.result)throw new Error('AI任务不能应用到此投标。')
  const sourceIssue=aiSourceIssue(bid,job)
  if(sourceIssue)throw new Error(sourceIssue)
  const selection={bidId:bid.id,kind:job.kind,sectionId:job.sectionId,sourceIds:job.sourceIds,pageRanges:job.pageRanges}
  const input=buildAiInput(bid,selection),result=validateAiResult(job.result,job.kind,input)
  const count=job.kind==='requirements'?result.requirements.length:job.kind==='outline'?result.outline.length:1
  const selected=[...new Set(indices)]
  if(!selected.length||selected.some(i=>!Number.isInteger(i)||i<0||i>=count))throw new Error('请选择有效的候选项。')
  let next=structuredClone(bid)
  const sourceText=(ref:AiReference)=>{const src=input.sources.find(s=>s.id===ref.sourceId)!;return `${src.name} · ${src.blocks.find(b=>b.index===ref.index)!.location}\n${ref.quote}`}
  for(const i of selected){
    const id=`ai_${job.id}_${i}`
    if(job.kind==='requirements'){
      if(next.requirements.some(r=>r.id===id))continue
      const r=result.requirements[i],src=input.sources.find(s=>s.id===r.reference.sourceId)!,block=src.blocks.find(b=>b.index===r.reference.index)!
      if(next.requirements.some(e=>e.sourceId===src.id&&e.location===block.location&&e.excerpt===r.reference.quote))continue
      next.requirements.push({...newRequirement(),id,title:r.title,kind:r.kind,sourceId:src.id,location:block.location,excerpt:r.reference.quote,mandatory:false})
      next.requirementsReviewed=false
    }else{
      if(next.sections.some(s=>s.id===id))continue
      const r=job.kind==='outline'?result.outline[i]:null
      next.sections.push({id,title:r?r.title:(input.section!.title.slice(0,145)+'（AI候选）'),category:'AI候选',body:`【待核定：AI建议，须人工核对工程事实及适用性】\n${r?'':result.chapter!.body+'\n\n'}来源摘录：\n${(r?[r.reference]:result.chapter!.references).map(sourceText).join('\n\n')}`,tables:[],reviewed:false})
      next=invalidateResponses(next)
    }
  }
  return parseBid(next)
}
