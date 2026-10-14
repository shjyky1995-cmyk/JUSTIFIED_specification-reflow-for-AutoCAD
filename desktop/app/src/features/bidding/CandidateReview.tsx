import React, { useMemo, useState } from 'react'
import { requirementCandidates, adoptCandidates } from './assistance'
import type { BidProject } from './model'

export function CandidateReview({ bid, sourceId, onAdopt, onClose }: { bid: BidProject; sourceId: string; onAdopt: (mutate: (bid: BidProject) => BidProject) => void; onClose: () => void }) {
  const [selected, setSelected] = useState<number[]>([]), [page, setPage] = useState(0), [error, setError] = useState('')
  const report = useMemo(() => requirementCandidates(bid, sourceId), [bid, sourceId])
  const source = bid.sources.find(s => s.id === sourceId)
  const available = report.candidates.filter(c => !c.alreadyAdded), totalPages = Math.max(1, Math.ceil(report.candidates.length / 12))
  return <section className="bid-card bid-candidates" aria-label="本地要求候选">
    <div className="bid-toolbar"><h2>要求候选</h2><span>{source?.name} · 检查{report.examined}段 · {available.length}项可加入</span><button className="bid-button" onClick={onClose}>收起候选</button></div>
    <p className="bid-notice">本地关键词筛选，不调用 AI。分类只是建议，否定、例外和适用条件请对照整份原文核对。勾选加入后仍需逐条复核，不会自动填写分值或证明事实。</p>
    {report.notices.map((message,i) => <p className="bid-notice" key={i}>{message}</p>)}
    {!report.candidates.length && <p className="bid-muted">没有找到关键词候选；不代表招标文件没有要求，请继续人工核对。</p>}
    {report.candidates.slice(page*12, page*12+12).map(c => <label className="bid-candidate" key={c.index}>
      <input type="checkbox" disabled={c.alreadyAdded} checked={selected.includes(c.index) && !c.alreadyAdded} onChange={e => setSelected(e.target.checked ? [...selected,c.index] : selected.filter(i => i !== c.index))}/>
      <span><strong>{c.kind} · {c.location}{c.alreadyAdded ? ' · 已加入' : ''}</strong><small>命中：{c.reasons.join('、')}</small><span className="bid-candidate-text">{c.excerpt}</span></span>
    </label>)}
    <div className="bid-toolbar"><button className="bid-button" disabled={page===0} onClick={()=>setPage(page-1)}>上一页</button><span>{page+1} / {totalPages}</span><button className="bid-button" disabled={page+1>=totalPages} onClick={()=>setPage(page+1)}>下一页</button><button className="bid-button primary" disabled={!selected.some(i=>available.some(c=>c.index===i))} onClick={()=>{
      try { if(!source) throw new Error('资料不存在。'); const indices=selected.filter(i=>available.some(c=>c.index===i)); adoptCandidates(bid,sourceId,source.hash,indices); onAdopt(current=>adoptCandidates(current,sourceId,source.hash,indices)); setSelected([]); setError('') } catch(e) { setError(e instanceof Error?e.message:String(e)) }
    }}>加入所选要求（{selected.filter(i=>available.some(c=>c.index===i)).length}）</button></div>
    {error && <p className="bid-danger" role="alert">{error}</p>}
  </section>
}
