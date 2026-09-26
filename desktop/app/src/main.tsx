import React, { useState } from 'react'
import { createRoot } from 'react-dom/client'
import type { WorkResult } from './global'
import './styles.css'

type Draft = {
  title: string
  projectName: string
  discipline: string
  overview: string
  basis: string
  requirements: string
  other: string
}

const initialDraft: Draft = {
  title: '设计说明',
  projectName: '',
  discipline: '结构',
  overview: '',
  basis: '',
  requirements: '',
  other: '',
}

const sections: { key: keyof Draft; title: string; hint: string }[] = [
  { key: 'overview', title: '工程概况', hint: '例如：工程用途、位置、结构概况' },
  { key: 'basis', title: '设计依据', hint: '例如：采用的资料与设计依据' },
  { key: 'requirements', title: '设计要求', hint: '例如：设计条件、材料和施工要求' },
  { key: 'other', title: '其他说明', hint: '其他需要保留在说明中的文字' },
]

function App() {
  const [draft, setDraft] = useState<Draft>(initialDraft)
  const [busy, setBusy] = useState(false)
  const [result, setResult] = useState<WorkResult | null>(null)
  const [notice, setNotice] = useState('填写固定模板，生成后可在 Word 或 WPS 中继续修改。')
  const [tab, setTab] = useState<'create' | 'check'>('create')
  const [checkedPath, setCheckedPath] = useState<string | null>(null)

  const hasContent = sections.some(section => draft[section.key].trim())
  const ready = Boolean(draft.title.trim() && draft.projectName.trim() && draft.discipline.trim() && hasContent)
  const filledSections = sections.filter(section => draft[section.key].trim()).length
  const canUseDesktop = Boolean(window.workbench)

  function update(key: keyof Draft, value: string) {
    setDraft(current => ({ ...current, [key]: value }))
    setResult(null)
  }

  async function generate() {
    if (!ready || !window.workbench) return
    setBusy(true)
    setNotice('正在生成并检查 DOCX…')
    try {
      const path = await window.workbench.chooseSave()
      if (!path) { setNotice('已取消保存，填写内容仍在。'); return }
      const response = await window.workbench.work({ operation: 'generate', path, ...draft })
      setResult(response)
      setNotice(response.message)
      if (response.success) setCheckedPath(path)
    } catch (error) {
      setResult(null)
      setNotice(error instanceof Error ? error.message : '生成失败，请重试。')
    } finally { setBusy(false) }
  }

  async function check() {
    if (!window.workbench) return
    setBusy(true)
    setNotice('正在读取 DOCX…')
    try {
      const path = await window.workbench.chooseOpen()
      if (!path) { setNotice('已取消选择。'); return }
      setCheckedPath(path)
      const response = await window.workbench.work({ operation: 'inspect', path })
      setResult(response)
      setNotice(response.message)
    } catch (error) {
      setResult(null)
      setNotice(error instanceof Error ? error.message : '检查失败，请重试。')
    } finally { setBusy(false) }
  }

  async function openFile() {
    const path = result?.path || checkedPath
    if (!path || !window.workbench) return
    try { await window.workbench.openDocx(path) }
    catch (error) { setNotice(error instanceof Error ? error.message : '无法打开 DOCX。') }
  }

  return <div className="app-shell">
    <aside className="sidebar">
      <div className="brand">
        <div className="brand-mark"><span></span><span></span><span></span></div>
        <div><strong>JUSTIFIED</strong><small>设计说明工作台</small></div>
      </div>
      <div className="sidebar-caption">工作空间 / WORKSPACE</div>
      <nav className="nav">
        <button className={tab === 'create' ? 'active' : ''} onClick={() => setTab('create')}><span className="nav-icon">✦</span><span>编制说明</span><span className="nav-arrow">›</span></button>
        <button className={tab === 'check' ? 'active' : ''} onClick={() => setTab('check')}><span className="nav-icon">⌁</span><span>检查 DOCX</span><span className="nav-arrow">›</span></button>
      </nav>
      <div className="sidebar-bottom">
        <div className="flow-line"><span className="flow-dot current"></span><span>填写固定模板</span></div>
        <div className="flow-line"><span className="flow-dot"></span><span>Word / WPS 修改</span></div>
        <div className="flow-line"><span className="flow-dot"></span><span>在 CAD 中选择导入</span></div>
        <p>桌面程序和 CAD 独立运行。<br/>一份 DOCX 串起你的工作。</p>
      </div>
    </aside>

    <main className="main-area">
      <header className="topbar"><span>设计说明 / {tab === 'create' ? '编制' : '检查'}</span><span className="topbar-tag"><span></span> 离线工作台</span></header>
      {tab === 'create' ? <div className="workspace">
        <div className="page-heading"><div><span className="eyebrow">CREATE DOCUMENT · 01</span><h1>把想法，写成一份好用的说明。</h1><p>从固定模板开始，生成标准 DOCX。正文随后在 Word 或 WPS 自由调整。</p></div><div className="heading-deco">01<span>/ 03</span></div></div>
        <div className="content-grid">
          <div className="form-column">
            <section className="panel identity-panel"><div className="panel-head"><span className="panel-index">01</span><div><h2>基本信息</h2><p>这些内容会写入 DOCX 的开头。</p></div></div>
              <div className="form-grid"><label className="field wide"><span>说明标题 <b>*</b></span><input value={draft.title} maxLength={120} onChange={e => update('title', e.target.value)} placeholder="如：结构设计说明" /></label><label className="field"><span>工程名称 <b>*</b></span><input value={draft.projectName} maxLength={120} onChange={e => update('projectName', e.target.value)} placeholder="填写工程名称" /></label><label className="field"><span>专业 <b>*</b></span><input value={draft.discipline} maxLength={60} onChange={e => update('discipline', e.target.value)} placeholder="如：结构" /></label></div>
            </section>
            <section className="panel sections-panel"><div className="panel-head"><span className="panel-index">02</span><div><h2>说明内容</h2><p>至少填写一栏。每次换行都会保留为独立段落。</p></div><span className="panel-count">已填写 {filledSections} / 4 栏</span></div>
              <div className="section-fields">{sections.map((section, index) => <label className="field section-field" key={section.key}><span><i>{String(index + 1).padStart(2, '0')}</i>{section.title}</span><textarea value={draft[section.key]} onChange={e => update(section.key, e.target.value)} placeholder={section.hint} rows={index === 0 ? 4 : 3} /></label>)}</div>
            </section>
          </div>
          <div className="side-column">
            <section className="preview-card"><div className="preview-head"><span>DOCUMENT PREVIEW</span><span className="preview-spark">✦</span></div><div className="paper-preview"><span className="paper-mini">DOCX / GENERAL NOTE</span><h3>{draft.title || '设计说明'}</h3><p>工程名称：{draft.projectName || '待填写'}</p><p>专业：{draft.discipline || '待填写'}</p><div className="paper-rule"></div>{sections.filter(section => draft[section.key].trim()).slice(0, 3).map(section => <div className="paper-section" key={section.key}><strong>{section.title}</strong><span>{draft[section.key].split('\n')[0].slice(0, 52)}</span></div>)}{!hasContent && <div className="paper-empty">填写内容后，这里会显示结构摘要。</div>}</div><div className="preview-foot">这是结构摘要；文字效果以 Word/WPS 打开后的文件为准。</div></section>
            <section className="tip-card"><span className="tip-icon">i</span><div><strong>生成后仍可自由修改</strong><p>已有文件不会被覆盖。保存后，在 CAD 中使用 DSS 重新选择最终 DOCX。</p></div></section>
          </div>
        </div>
        <div className="action-bar"><div className="status"><span className={'status-light ' + (result?.success ? 'good' : result && !result.success ? 'bad' : '')}></span><div><strong>{result?.success ? '文件已就绪' : result && !result.success ? '需要处理' : busy ? '正在处理' : '准备生成'}</strong><small>{notice}</small></div></div><div className="action-buttons">{result?.success && <button className="secondary-button" onClick={openFile}>打开 DOCX</button>}<button className="primary-button" disabled={!ready || busy || !canUseDesktop} onClick={generate}>生成 DOCX <span>↗</span></button></div></div>
        {!canUseDesktop && <p className="browser-note">网页预览只展示界面；请从桌面程序中生成文件。</p>}
      </div> : <div className="workspace check-workspace"><div className="page-heading"><div><span className="eyebrow">CHECK DOCUMENT · 02</span><h1>检查你最后保存的 DOCX。</h1><p>确认正文结构与不支持的内容，再到 CAD 中选择同一个文件。</p></div><div className="heading-deco">02<span>/ 03</span></div></div><section className="check-panel panel"><div className="check-symbol">⌁</div><h2>选择已经修改好的 Word 文件</h2><p>桌面端只检查内容和结构，不预测 CAD 图幅排版，也不会修改原文件。</p><button className="primary-button" disabled={busy || !canUseDesktop} onClick={check}>{busy ? '正在检查…' : '选择 DOCX 并检查'} <span>↗</span></button>{checkedPath && <div className="selected-path">{checkedPath}</div>}</section>{result && <section className={'check-result ' + (result.success ? 'passed' : 'failed')}><strong>{result.success ? '检查通过' : '需要修改'}</strong><p>{result.message} {result.success ? `识别到 ${result.blocks} 个内容块。` : ''}</p>{result.diagnostics.length > 0 && <ul>{result.diagnostics.map((item, index) => <li key={index}>{item.message}</li>)}</ul>}{result.success && <button className="secondary-button" onClick={openFile}>打开这份 DOCX</button>}</section>}<div className="action-bar"><div className="status"><span className={'status-light ' + (result?.success ? 'good' : '')}></span><div><strong>{result?.success ? '可在 CAD 导入' : '等待选择文件'}</strong><small>{notice}</small></div></div><span className="foot-hint">CAD 中仍需手动选择 DOCX</span></div></div>}
    </main>
  </div>
}

createRoot(document.getElementById('root')!).render(<React.StrictMode><App /></React.StrictMode>)
