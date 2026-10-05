import React, { useEffect, useRef, useState } from 'react'
import type { BidSource, SourceBlock } from './model'
import { pdfPhysicalPage } from './ai-model'

export function SourcePreview({ source, focus, onCreate }: { source: BidSource; focus: string; onCreate: (block: SourceBlock) => void }) {
  const [group, setGroup] = useState(0)
  const container = useRef<HTMLDivElement>(null)
  const groups = Math.max(1, Math.ceil(source.blocks.length / 20))
  useEffect(() => { const index=source.blocks.findIndex(b=>b.location===focus); setGroup(index<0?0:Math.floor(index/20)) }, [source.id,focus])
  useEffect(() => { container.current?.querySelector('.bid-source-focused')?.scrollIntoView({block:'center'}) }, [focus,group])
  return <div className="bid-source-blocks" ref={container}>
    <div className="bid-toolbar"><span>{source.blocks.length}块摘录 · 每页显示20块</span>{groups>1&&<><button className="bid-button" disabled={group===0} onClick={()=>setGroup(group-1)}>上一组摘录</button><label className="bid-field"><span>摘录分组</span><select value={group} onChange={e=>setGroup(Number(e.target.value))}>{Array.from({length:groups},(_,i)=><option key={i} value={i}>{i+1} / {groups} · {source.blocks[i*20]?.location}</option>)}</select></label><button className="bid-button" disabled={group+1>=groups} onClick={()=>setGroup(group+1)}>下一组摘录</button></>}</div>
    {source.blocks.slice(group*20,group*20+20).map((block,i)=><div key={group*20+i} className={block.location===focus?'bid-source-focused':''}>
      <small>{block.location}{pdfPhysicalPage(block.location)!==null?' · 按文件顺序计页':''}</small><p>{block.text}</p>
      <button className="bid-text-button" disabled={block.text.length>20_000} onClick={()=>onCreate(block)}>从此处建立要求</button>
      {block.text.length>20_000&&<small>摘录超过单项2万字符限额，请人工拆分；不会截断加入要求。</small>}
    </div>)}
    {!source.blocks.length&&<p className="bid-muted">未提取到文字，请打开原件核对；扫描件和图片尚未执行OCR。</p>}
  </div>
}
