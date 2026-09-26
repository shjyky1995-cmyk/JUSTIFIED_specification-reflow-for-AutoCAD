import React, { useEffect, useMemo, useRef, useState } from 'react'
import { createRoot } from 'react-dom/client'
import {
  buildDocument,
  createNote,
  customTemplateId,
  defaultStructuralParams,
  defaultTitle,
  disciplineLabel,
  DISCIPLINES,
  findIssues,
  FOUNDATION_GRADES,
  isSectionEmpty,
  librarySections,
  newId,
  nowIso,
  PROTECTION_SCHEMES,
  rebuildSections,
  resetSectionBody,
  SAFETY_LEVELS,
  SEISMIC_GRADES,
  SEISMIC_INTENSITY_PENDING,
  sectionTitle,
  SITE_CATEGORIES,
  STRUCTURAL_REQUIRED_FIELDS,
  templatesFor,
  toExportRequest,
  type DisciplineCode,
  type Note,
  type NoteIssue,
  type NoteSection,
  type Project,
  type StructuralParams,
} from './shared/model'
import type { NoteSummary, StoredProject } from './shared/model'
import type { WorkResult } from './global'
import './styles.css'

type Route = 'home' | 'step01' | 'step02a' | 'step02b' | 'step03'
type SaveStatus = 'idle' | 'dirty' | 'saving' | 'saved' | 'error'
type NoteMutation = (note: Note | null) => Note | null

type ConfirmState = { title: string; message: string; confirmLabel: string; danger: boolean; onConfirm: () => void } | null

const STEP_LABELS: { route: Route; label: string }[] = [
  { route: 'step01', label: '01 参数设置' },
  { route: 'step02b', label: '02 章节编制' },
  { route: 'step03', label: '03 生成导出' },
]

function formatTime(iso: string): string {
  if (!iso) return ''
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return ''
  const pad = (value: number) => String(value).padStart(2, '0')
  return `${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`
}

function formatDateTime(iso: string): string {
  if (!iso) return ''
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return ''
  const pad = (value: number) => String(value).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`
}

function App() {
  const canUseDesktop = Boolean(window.workbench)
  const [route, setRoute] = useState<Route>('home')
  const [note, setNote] = useState<Note | null>(null)
  const noteRef = useRef<Note | null>(null)
  const [notes, setNotes] = useState<NoteSummary[]>([])
  const [projects, setProjects] = useState<StoredProject[]>([])
  const [search, setSearch] = useState('')
  const [save, setSave] = useState<{ status: SaveStatus; message: string }>({ status: 'idle', message: '' })
  const [confirm, setConfirm] = useState<ConfirmState>(null)
  const [selectedSectionId, setSelectedSectionId] = useState<string | null>(null)
  const [libraryOpen, setLibraryOpen] = useState(false)
  const [linkedProjectId, setLinkedProjectId] = useState<string | null>(null)
  const [exportState, setExportState] = useState<{ busy: boolean; result: WorkResult | null; error: string | null }>({ busy: false, result: null, error: null })
  const saveTimer = useRef<number | null>(null)

  function setWorkingNote(next: Note | null) {
    noteRef.current = next
    setNote(next)
  }

  function refreshHome() {
    if (!window.workbench) return
    void window.workbench.notesList().then(setNotes).catch(() => setNotes([]))
    void window.workbench.projectsList().then(setProjects).catch(() => setProjects([]))
  }

  useEffect(() => { refreshHome() }, [])

  function scheduleSave() {
    setSave({ status: 'dirty', message: '有未保存更改' })
    if (saveTimer.current !== null) window.clearTimeout(saveTimer.current)
    saveTimer.current = window.setTimeout(() => { void commitSave() }, 800)
  }

  async function commitSave(): Promise<boolean> {
    const current = noteRef.current
    if (!current || !window.workbench) return false
    setSave({ status: 'saving', message: '正在保存…' })
    try {
      const result = await window.workbench.noteSave(current)
      if (result.ok) {
        setSave({ status: 'saved', message: '已保存 ' + formatTime(result.savedAt) })
        return true
      }
      setSave({ status: 'error', message: result.error })
      return false
    } catch (error) {
      setSave({ status: 'error', message: error instanceof Error ? error.message : '保存失败，请重试。' })
      return false
    }
  }

  function updateNote(mutate: NoteMutation) {
    const next = mutate(noteRef.current)
    if (!next) return
    next.updatedAt = nowIso()
    noteRef.current = next
    setNote(next)
    scheduleSave()
  }

  function askConfirm(state: NonNullable<ConfirmState>) {
    setConfirm(state)
  }

  function startNew() {
    setWorkingNote(null)
    setSave({ status: 'idle', message: '' })
    setSelectedSectionId(null)
    setLibraryOpen(false)
    setLinkedProjectId(null)
    setExportState({ busy: false, result: null, error: null })
    setRoute('step01')
  }

  async function openNote(id: string) {
    if (!window.workbench) return
    try {
      const loaded = await window.workbench.noteLoad(id)
      setWorkingNote(loaded)
      setSave({ status: 'saved', message: '已恢复 ' + formatTime(loaded.updatedAt) })
      setSelectedSectionId(loaded.sections[0]?.id ?? null)
      setExportState({ busy: false, result: null, error: null })
      setRoute(loaded.sections.length > 0 ? 'step02b' : 'step01')
    } catch (error) {
      setSave({ status: 'error', message: error instanceof Error ? error.message : '打开说明失败。' })
    }
  }

  async function deleteNote(id: string) {
    if (!window.workbench) return
    try {
      await window.workbench.noteDelete(id)
      refreshHome()
    } catch (error) {
      setSave({ status: 'error', message: error instanceof Error ? error.message : '删除失败。' })
    }
  }

  const issues: NoteIssue[] = useMemo(() => (note ? findIssues(note) : []), [note])
  const blocking = issues.some(issue => issue.level === 'error')

  return <div className="app-shell">
    {route !== 'home' && <StepNav route={route} note={note} onNavigate={target => {
      if (target === 'home') { setRoute('home'); refreshHome(); return }
      if (!note) return
      setRoute(target)
    }} />}
    <main className={'main-area route-' + route}>
      {route === 'home' && <HomePage notes={notes} projects={projects} search={search} onSearch={setSearch} onNew={startNew} onContinue={id => void openNote(id)} onDelete={id => void deleteNote(id)} />}
      {route === 'step01' && <Step01 note={note} projects={projects} linkedProjectId={linkedProjectId} setLinkedProjectId={setLinkedProjectId} onUpdate={updateNote} onConfirm={askConfirm} onDone={async () => {
        if (!noteRef.current) return
        if (!(await commitSave())) return
        await rememberProject(noteRef.current, projects, setProjects)
        setRoute('step02a')
      }} />}
      {route === 'step02a' && note && <Step02A note={note} onBack={() => setRoute('step01')} onSelect={templateId => {
        const current = noteRef.current
        if (!current) return
        const hasEdits = current.sections.some(section => section.custom || section.body.trim() !== resetSectionBody(section).trim())
        const apply = () => {
          const sections = rebuildSections(current, templateId)
          const next: Note = { ...current, templateId, sections }
          noteRef.current = next
          setNote(next)
          setSelectedSectionId(sections[0]?.id ?? null)
          setLibraryOpen(templatesFor(current.discipline).find(item => item.id === templateId)?.custom === true)
          scheduleSave()
          setRoute('step02b')
        }
        if (hasEdits) {
          askConfirm({
            title: '重新选择模板',
            message: '重新选择模板将丢弃当前章节副本与正文修改，按新模板重建章节。确定继续？',
            confirmLabel: '重建章节',
            danger: true,
            onConfirm: apply,
          })
          return
        }
        apply()
      }} />}
      {route === 'step02b' && note && <Step02B note={note} save={save} selectedSectionId={selectedSectionId} setSelectedSectionId={setSelectedSectionId} libraryOpen={libraryOpen} setLibraryOpen={setLibraryOpen} onUpdate={updateNote} onBack={() => setRoute('step02a')} onConfirm={askConfirm} onGenerate={async () => {
        if (!(await commitSave())) return
        setExportState({ busy: false, result: null, error: null })
        setRoute('step03')
      }} />}
      {route === 'step03' && note && <Step03 note={note} issues={issues} blocking={blocking} exportState={exportState} setExportState={setExportState} onReconfigure={() => setRoute('step01')} onBackToEdit={() => setRoute('step02b')} onConfirm={askConfirm} onExported={() => refreshHome()} />}
    </main>
    {!canUseDesktop && route !== 'home' && <p className="browser-note">网页预览只展示界面；请从桌面程序中保存与导出文件。</p>}
    {confirm && <ConfirmDialog state={confirm} onCancel={() => setConfirm(null)} />}
  </div>
}

async function rememberProject(note: Note, projects: StoredProject[], setProjects: (value: StoredProject[]) => void) {
  if (!window.workbench || note.project.name.trim().length === 0) return
  const existing = projects.find(item => item.name.trim() === note.project.name.trim())
  const project: StoredProject = existing
    ? { ...existing, number: note.project.number, owner: note.project.owner, location: note.project.location }
    : { id: newId(), ...note.project, updatedAt: nowIso() }
  try {
    setProjects(await window.workbench.projectSave(project))
  } catch {
    // 项目资料保存失败不阻断编制流程，下次仍可手填。
  }
}

function StepNav({ route, note, onNavigate }: { route: Route; note: Note | null; onNavigate: (target: Route) => void }) {
  return <header className="topbar">
    <div className="topbar-left">
      <button className="home-link" onClick={() => onNavigate('home')}>‹ 首页</button>
      <nav className="step-nav">
        {STEP_LABELS.map(step => <button key={step.route} className={route === step.route || (step.route === 'step02b' && route === 'step02a') ? 'active' : ''} disabled={!note} onClick={() => onNavigate(step.route)}>{step.label}</button>)}
      </nav>
    </div>
    <div className="topbar-right">
      <span className="topbar-tag"><span></span> 离线工作台 · 导出 DOCX</span>
    </div>
  </header>
}

function HomePage({ notes, projects, search, onSearch, onNew, onContinue, onDelete }: {
  notes: NoteSummary[]
  projects: StoredProject[]
  search: string
  onSearch: (value: string) => void
  onNew: () => void
  onContinue: (id: string) => void
  onDelete: (id: string) => void
}) {
  const keyword = search.trim().toLowerCase()
  const recent = keyword.length === 0 ? notes : notes.filter(item => item.title.toLowerCase().includes(keyword) || disciplineLabel(item.discipline).includes(keyword))
  const currentProject = projects[0]?.name ?? ''

  return <div className="home">
    <header className="home-header">
      <div className="brand">
        <div className="brand-mark"><span></span><span></span><span></span></div>
        <div><strong>设计说明工作台</strong><small>各专业设计说明编制 · 导出可编辑 DOCX</small></div>
      </div>
      <input className="home-search" value={search} onChange={event => onSearch(event.target.value)} placeholder="搜索最近说明（按标题或专业）" />
    </header>

    <section className="entry-grid">
      <button className="entry-card active" onClick={onNew}>
        <span className="entry-icon">✦</span>
        <strong>设计说明</strong>
        <small>六专业共用流程，编制并导出 DOCX</small>
        <span className="entry-action">新建 / 继续 ›</span>
      </button>
      {['可研报告', '招标文件', '项目管理', 'AI 工程助手'].map(name => <div className="entry-card" key={name}>
        <span className="entry-icon">{name === 'AI 工程助手' ? '✧' : name === '项目管理' ? '▦' : '▤'}</span>
        <strong>{name}</strong>
        <small>后续阶段提供</small>
        <span className="entry-badge">未开放</span>
      </div>)}
    </section>

    <section className="home-columns">
      <div className="home-panel">
        <div className="panel-head"><span className="panel-index">◇</span><div><h2>当前项目</h2><p>最近一次编制关联的项目资料</p></div></div>
        <p className="current-project">{currentProject || '暂无项目，新建说明时填写'}</p>
      </div>
      <div className="home-panel wide">
        <div className="panel-head"><span className="panel-index">☰</span><div><h2>最近工作</h2><p>关闭后重新打开可恢复最后一次保存的内容</p></div><span className="panel-count">{recent.length} 份</span></div>
        {recent.length === 0 ? <p className="empty-hint">{notes.length === 0 ? '还没有说明。点击「设计说明」开始新建。' : '没有匹配的说明。'}</p> : <ul className="recent-list">
          {recent.map(item => <li key={item.id}>
            <div className="recent-info">
              <strong>{item.title}</strong>
              <small>{disciplineLabel(item.discipline)} · {item.filledCount}/{item.sectionCount} 章已填写 · {formatDateTime(item.updatedAt)}</small>
            </div>
            <div className="recent-actions">
              <button className="secondary-button" onClick={() => onContinue(item.id)}>继续</button>
              <button className="text-danger" onClick={() => onDelete(item.id)}>删除</button>
            </div>
          </li>)}
        </ul>}
      </div>
    </section>
  </div>
}

function Step01({ note, projects, linkedProjectId, setLinkedProjectId, onUpdate, onConfirm, onDone }: {
  note: Note | null
  projects: StoredProject[]
  linkedProjectId: string | null
  setLinkedProjectId: (id: string | null) => void
  onUpdate: (mutate: NoteMutation) => void
  onConfirm: (state: NonNullable<ConfirmState>) => void
  onDone: () => void
}) {
  const discipline = note?.discipline ?? 'structural'
  const project = note?.project ?? { name: '', number: '', owner: '', location: '' }
  const title = note?.title ?? defaultTitle(discipline)
  const structural = note?.structural ?? null

  function ensureBase(code: DisciplineCode): Note {
    return note ?? createNote(code, customTemplateId(code), project, title)
  }

  function changeDiscipline(code: DisciplineCode) {
    if (note && note.discipline === code) return
    const apply = () => {
      const base = ensureBase(code)
      const next: Note = {
        ...base,
        discipline: code,
        title: note ? note.title : defaultTitle(code),
        structural: code === 'structural' ? defaultStructuralParams() : null,
        sections: [],
        templateId: customTemplateId(code),
      }
      onUpdate(() => next)
      setLinkedProjectId(null)
    }
    if (note && note.sections.length > 0) {
      onConfirm({
        title: '切换专业',
        message: '切换专业将重建当前章节副本与正文修改，结构参数也会重置。确定切换？',
        confirmLabel: '切换并重建',
        danger: true,
        onConfirm: apply,
      })
      return
    }
    apply()
  }

  function changeProject(patch: Partial<Project>) {
    onUpdate(current => {
      const base = current ?? createNote(discipline, customTemplateId(discipline), project, title)
      return { ...base, project: { ...base.project, ...patch } }
    })
  }

  function changeStructural(patch: Partial<StructuralParams>) {
    onUpdate(current => {
      const base = current ?? createNote(discipline, customTemplateId(discipline), project, title)
      return { ...base, structural: { ...(base.structural ?? defaultStructuralParams()), ...patch } }
    })
  }

  function linkProject(id: string) {
    if (id === '') { setLinkedProjectId(null); return }
    const target = projects.find(item => item.id === id)
    if (!target) return
    const filled = [project.name, project.number, project.owner, project.location].some(value => value.trim().length > 0)
    const differs = project.name.trim() !== target.name.trim() || project.number.trim() !== target.number.trim() || project.owner.trim() !== target.owner.trim() || project.location.trim() !== target.location.trim()
    const applyLink = () => {
      setLinkedProjectId(id)
      onUpdate(current => {
        const base = current ?? createNote(discipline, customTemplateId(discipline), project, title)
        return { ...base, project: { name: target.name, number: target.number, owner: target.owner, location: target.location } }
      })
    }
    if (filled && differs) {
      onConfirm({
        title: '覆盖项目资料',
        message: `用项目「${target.name}」的资料覆盖已填写的内容？取消则保持已填内容。`,
        confirmLabel: '覆盖',
        danger: true,
        onConfirm: applyLink,
      })
      return
    }
    applyLink()
  }

  const structuralReady = discipline !== 'structural' || STRUCTURAL_REQUIRED_FIELDS.every(field => String(structural?.[field.key] ?? '').trim().length > 0)
  const ready = title.trim().length > 0 && project.name.trim().length > 0 && structuralReady

  return <div className="workspace narrow">
    <div className="page-heading"><div><span className="eyebrow">STEP 01 · 参数设置</span><h1>选择专业，填写项目资料。</h1><p>非结构专业不显示结构参数；结构专业的抗震设防烈度在获得经核验数据前显示「待核定」。</p></div><div className="heading-deco">01<span>/ 03</span></div></div>

    <section className="panel">
      <div className="panel-head"><span className="panel-index">01</span><div><h2>专业</h2><p>六个专业都可完成编制与导出</p></div></div>
      <div className="discipline-grid">
        {DISCIPLINES.map(item => <button key={item.code} className={discipline === item.code ? 'discipline-card active' : 'discipline-card'} onClick={() => changeDiscipline(item.code)}>
          <strong>{item.label}</strong>
          <small>{item.code === 'structural' ? '含结构专属参数' : item.code === 'other' ? '通用章节组合' : '专业章节组合'}</small>
        </button>)}
      </div>
    </section>

    <section className="panel">
      <div className="panel-head"><span className="panel-index">02</span><div><h2>说明与项目资料</h2><p>可关联已有项目带出资料，也可以直接手填</p></div></div>
      <div className="form-grid">
        <label className="field wide"><span>说明标题 <b>*</b></span><input value={title} maxLength={120} onChange={event => onUpdate(current => {
          const base = current ?? createNote(discipline, customTemplateId(discipline), project, title)
          return { ...base, title: event.target.value }
        })} placeholder="如：结构设计说明" /></label>
        <label className="field wide"><span>关联项目</span>
          <select value={linkedProjectId ?? ''} onChange={event => linkProject(event.target.value)}>
            <option value="">不关联项目</option>
            {projects.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}
          </select>
        </label>
        <label className="field"><span>工程名称 <b>*</b></span><input value={project.name} maxLength={120} onChange={event => changeProject({ name: event.target.value })} placeholder="填写工程名称" /></label>
        <label className="field"><span>工程编号</span><input value={project.number} maxLength={60} onChange={event => changeProject({ number: event.target.value })} placeholder="选填" /></label>
        <label className="field"><span>建设单位</span><input value={project.owner} maxLength={120} onChange={event => changeProject({ owner: event.target.value })} placeholder="选填" /></label>
        <label className="field"><span>建设地点</span><input value={project.location} maxLength={120} onChange={event => changeProject({ location: event.target.value })} placeholder="选填" /></label>
      </div>
    </section>

    {discipline === 'structural' && <section className="panel">
      <div className="panel-head"><span className="panel-index">03</span><div><h2>结构参数</h2><p>仅结构专业显示；设计使用年限默认 50 年</p></div></div>
      <div className="form-grid">
        <label className="field"><span>场地类别 <b>*</b></span>
          <select value={structural?.siteCategory ?? ''} onChange={event => changeStructural({ siteCategory: event.target.value })}>
            <option value="">请选择</option>
            {SITE_CATEGORIES.map(item => <option key={item} value={item}>{item}</option>)}
          </select>
        </label>
        <label className="field"><span>抗震设防类别 <b>*</b></span>
          <select value={structural?.seismicGrade ?? ''} onChange={event => changeStructural({ seismicGrade: event.target.value })}>
            <option value="">请选择</option>
            {SEISMIC_GRADES.map(item => <option key={item} value={item}>{item}</option>)}
          </select>
        </label>
        <label className="field"><span>结构安全等级 <b>*</b></span>
          <select value={structural?.safetyLevel ?? ''} onChange={event => changeStructural({ safetyLevel: event.target.value })}>
            <option value="">请选择</option>
            {SAFETY_LEVELS.map(item => <option key={item} value={item}>{item}</option>)}
          </select>
        </label>
        <label className="field"><span>地基基础设计等级 <b>*</b></span>
          <select value={structural?.foundationGrade ?? ''} onChange={event => changeStructural({ foundationGrade: event.target.value })}>
            <option value="">请选择</option>
            {FOUNDATION_GRADES.map(item => <option key={item} value={item}>{item}</option>)}
          </select>
        </label>
        <label className="field"><span>设计使用年限</span><input value="50 年" readOnly /></label>
        <label className="field"><span>抗震设防烈度</span><input value={SEISMIC_INTENSITY_PENDING} readOnly /><small className="field-hint">尚无经核验的地点映射数据，不猜值</small></label>
        <label className="field"><span>材料/防腐方案 <b>*</b></span>
          <select value={structural?.protectionScheme ?? ''} onChange={event => changeStructural({ protectionScheme: event.target.value })}>
            <option value="">请选择</option>
            {PROTECTION_SCHEMES.map(item => <option key={item} value={item}>{item}</option>)}
          </select>
        </label>
        <label className="field"><span>附加措施</span><input value={structural?.protectionExtra ?? ''} maxLength={120} onChange={event => changeStructural({ protectionExtra: event.target.value })} placeholder="选填" /></label>
        <label className="field wide"><span>水土腐蚀性</span><textarea value={structural?.corrosion ?? ''} maxLength={4000} onChange={event => changeStructural({ corrosion: event.target.value })} placeholder="可粘贴勘察报告中的腐蚀性结论（长文本）" rows={3} /></label>
      </div>
    </section>}

    <div className="action-bar">
      <div className="status"><span className="status-light"></span><div><strong>{discipline === 'structural' ? '结构参数已展开' : '非结构专业，无结构参数'}</strong><small>下一步选择章节模板与自定义组合</small></div></div>
      <div className="action-buttons">
        <button className="primary-button" disabled={!ready} onClick={onDone}>下一步：章节模板 <span>›</span></button>
      </div>
    </div>
    {!ready && <p className="form-hint">请填写说明标题、工程名称{discipline === 'structural' ? '与全部结构参数' : ''}后继续。</p>}
  </div>
}

function Step02A({ note, onBack, onSelect }: { note: Note; onBack: () => void; onSelect: (templateId: string) => void }) {
  const templates = templatesFor(note.discipline)
  return <div className="workspace">
    <div className="page-heading"><div><span className="eyebrow">STEP 02A · 模板选择</span><h1>选择章节组合。</h1><p>预设模板只是默认组合；之后仍可添加、移除、排序，或从可用章节库自定义。</p></div><div className="heading-deco">02<span>/ 03</span></div></div>
    <div className="template-grid">
      {templates.map(template => <button key={template.id} className={note.templateId === template.id && note.sections.length > 0 ? 'template-card active' : 'template-card'} onClick={() => onSelect(template.id)}>
        <strong>{template.name}</strong>
        <small>{template.custom ? '从可用章节库逐章选取' : template.sectionIds.length + ' 个章节'}</small>
        {!template.custom && <span className="template-preview">{template.sectionIds.map(id => sectionTitle(id)).join(' · ')}</span>}
        <span className="entry-action">{template.custom ? '开始自定义 ›' : '选择 ›'}</span>
      </button>)}
    </div>
    <div className="action-bar">
      <div className="status"><span className="status-light"></span><div><strong>{disciplineLabel(note.discipline)}专业</strong><small>当前标题：{note.title}</small></div></div>
      <div className="action-buttons"><button className="secondary-button" onClick={onBack}>‹ 上一步</button></div>
    </div>
  </div>
}

function Step02B({ note, save, selectedSectionId, setSelectedSectionId, libraryOpen, setLibraryOpen, onUpdate, onBack, onConfirm, onGenerate }: {
  note: Note
  save: { status: SaveStatus; message: string }
  selectedSectionId: string | null
  setSelectedSectionId: (id: string | null) => void
  libraryOpen: boolean
  setLibraryOpen: (open: boolean) => void
  onUpdate: (mutate: NoteMutation) => void
  onBack: () => void
  onConfirm: (state: NonNullable<ConfirmState>) => void
  onGenerate: () => void
}) {
  const selected = note.sections.find(section => section.id === selectedSectionId) ?? null
  const available = librarySections(note.discipline).filter(section => !note.sections.some(item => item.id === section.id))
  const filled = note.sections.filter(section => !isSectionEmpty(section)).length

  function moveSection(from: number, to: number) {
    if (to < 0 || to >= note.sections.length || from === to) return
    onUpdate(currentNote => {
      if (!currentNote) return currentNote
      const sections = [...currentNote.sections]
      const [moved] = sections.splice(from, 1)
      sections.splice(to, 0, moved)
      return { ...currentNote, sections }
    })
  }

  function removeSection(id: string) {
    onConfirm({
      title: '移除章节',
      message: '移除后该章节及其正文将从本说明中删除（不影响标准章节库）。确定移除？',
      confirmLabel: '移除',
      danger: true,
      onConfirm: () => {
        onUpdate(currentNote => currentNote ? { ...currentNote, sections: currentNote.sections.filter(section => section.id !== id) } : currentNote)
        if (selectedSectionId === id) setSelectedSectionId(null)
      },
    })
  }

  function addStandardSection(id: string) {
    const definition = librarySections(note.discipline).find(section => section.id === id)
    if (!definition) return
    onUpdate(currentNote => currentNote ? { ...currentNote, sections: [...currentNote.sections, { id: definition.id, title: definition.title, body: definition.body, custom: false }] } : currentNote)
    setSelectedSectionId(definition.id)
  }

  function addCustomSection(title: string, body: string) {
    const trimmed = title.trim()
    if (trimmed.length === 0) return
    const section: NoteSection = { id: 'custom-' + newId(), title: trimmed, body, custom: true }
    onUpdate(currentNote => currentNote ? { ...currentNote, sections: [...currentNote.sections, section] } : currentNote)
    setSelectedSectionId(section.id)
    setLibraryOpen(false)
  }

  return <div className="workspace editor-workspace">
    <div className="page-heading"><div><span className="eyebrow">STEP 02B · 章节工作区</span><h1>调整章节，逐章改写正文。</h1><p>正文为纯文本，自动保存；「恢复默认」只影响本说明的当前章节。</p></div><div className="heading-deco">02<span>/ 03</span></div></div>
    <div className="editor-grid">
      <aside className="section-panel panel">
        <div className="panel-head"><span className="panel-index">☰</span><div><h2>章节目录</h2><p>{note.sections.length} 章 · 已填 {filled} 章</p></div></div>
        <ul className="section-list">
          {note.sections.map((section, index) => <li key={section.id} className={selected && selected.id === section.id ? 'active' : ''} draggable onDragStart={event => { event.dataTransfer.setData('text/plain', String(index)) }} onDragOver={event => event.preventDefault()} onDrop={event => { event.preventDefault(); moveSection(Number(event.dataTransfer.getData('text/plain')), index) }} onClick={() => setSelectedSectionId(section.id)}>
            <span className="drag-handle">⠿</span>
            <span className="section-index">{String(index + 1).padStart(2, '0')}</span>
            <span className="section-title">{section.title}{section.custom && <em className="custom-tag">自定义</em>}{isSectionEmpty(section) && <em className="empty-tag">空</em>}</span>
            <span className="section-tools" onClick={event => event.stopPropagation()}>
              <button title="上移" disabled={index === 0} onClick={() => moveSection(index, index - 1)}>↑</button>
              <button title="下移" disabled={index === note.sections.length - 1} onClick={() => moveSection(index, index + 1)}>↓</button>
              <button title="移除" className="text-danger" onClick={() => removeSection(section.id)}>×</button>
            </span>
          </li>)}
        </ul>
        {note.sections.length === 0 && <p className="empty-hint">还没有章节。点击下方「添加章节」从标准章节库选取，或新建当前说明专用章节。</p>}
        <button className="secondary-button add-section" onClick={() => setLibraryOpen(true)}>＋ 添加章节</button>
      </aside>

      <section className="editor-panel panel">
        {selected ? <div className="editor-body">
          <div className="editor-head">
            <div><h2>{selected.title}</h2><p>{selected.custom ? '当前说明专用章节，不写回标准库' : '标准章节 · 可改为本说明正文'}</p></div>
            {!selected.custom && <button className="secondary-button" onClick={() => onUpdate(currentNote => currentNote ? { ...currentNote, sections: currentNote.sections.map(section => section.id === selected.id ? { ...section, body: resetSectionBody(section) } : section) } : currentNote)}>恢复默认</button>}
          </div>
          <textarea className="section-editor" value={selected.body} maxLength={100000} onChange={event => onUpdate(currentNote => currentNote ? { ...currentNote, sections: currentNote.sections.map(section => section.id === selected.id ? { ...section, body: event.target.value } : section) } : currentNote)} placeholder="逐章填写纯文本正文；换行会保留为独立段落。" />
          <div className="editor-foot">
            <span className={'save-state ' + save.status}>{saveStatusText(save)}</span>
            <span className="char-count">{selected.body.length} 字</span>
          </div>
        </div> : <div className="editor-empty"><p>从左侧选择章节开始编辑，或添加新章节。</p></div>}
      </section>
    </div>

    {libraryOpen && <AddSectionModal available={available} onAddStandard={addStandardSection} onAddCustom={addCustomSection} onClose={() => setLibraryOpen(false)} />}

    <div className="action-bar">
      <div className="status"><span className="status-light"></span><div><strong>{save.status === 'error' ? '保存失败' : '自动保存已开启'}</strong><small>{save.message || '停止输入约 1 秒后自动保存到本机'}</small></div></div>
      <div className="action-buttons">
        <button className="secondary-button" onClick={onBack}>‹ 模板选择</button>
        <button className="primary-button" disabled={note.sections.length === 0} onClick={onGenerate}>生成说明 <span>›</span></button>
      </div>
    </div>
  </div>
}

function saveStatusText(save: { status: SaveStatus; message: string }): string {
  switch (save.status) {
    case 'saving': return '保存中…'
    case 'saved': return save.message || '已保存'
    case 'error': return '保存失败：' + save.message
    case 'dirty': return '有未保存更改'
    default: return '自动保存已开启'
  }
}

function AddSectionModal({ available, onAddStandard, onAddCustom, onClose }: {
  available: { id: string; title: string; body: string; applicability: string }[]
  onAddStandard: (id: string) => void
  onAddCustom: (title: string, body: string) => void
  onClose: () => void
}) {
  const [tab, setTab] = useState<'standard' | 'custom'>('standard')
  const [customTitle, setCustomTitle] = useState('')
  const [customBody, setCustomBody] = useState('')
  return <div className="modal-mask" onClick={onClose}>
    <div className="modal" onClick={event => event.stopPropagation()}>
      <div className="modal-head">
        <div className="modal-tabs">
          <button className={tab === 'standard' ? 'active' : ''} onClick={() => setTab('standard')}>标准章节</button>
          <button className={tab === 'custom' ? 'active' : ''} onClick={() => setTab('custom')}>自定义章节</button>
        </div>
        <button className="modal-close" onClick={onClose}>×</button>
      </div>
      {tab === 'standard' ? <ul className="library-list">
        {available.length === 0 && <li className="empty-hint">可用章节都已加入。</li>}
        {available.map(section => <li key={section.id}>
          <div className="library-info"><strong>{section.title}</strong><small>{section.applicability} · {section.body}</small></div>
          <button className="secondary-button" onClick={() => onAddStandard(section.id)}>添加</button>
        </li>)}
      </ul> : <div className="custom-form">
        <label className="field"><span>章节标题 <b>*</b></span><input value={customTitle} maxLength={60} onChange={event => setCustomTitle(event.target.value)} placeholder="如：深基坑支护" /></label>
        <label className="field"><span>正文（选填）</span><textarea value={customBody} maxLength={100000} rows={5} onChange={event => setCustomBody(event.target.value)} placeholder="可随后在编辑区继续修改" /></label>
        <button className="primary-button" disabled={customTitle.trim().length === 0} onClick={() => { onAddCustom(customTitle, customBody); setCustomTitle(''); setCustomBody('') }}>添加章节</button>
      </div>}
    </div>
  </div>
}

function Step03({ note, issues, blocking, exportState, setExportState, onReconfigure, onBackToEdit, onConfirm, onExported }: {
  note: Note
  issues: NoteIssue[]
  blocking: boolean
  exportState: { busy: boolean; result: WorkResult | null; error: string | null }
  setExportState: (value: { busy: boolean; result: WorkResult | null; error: string | null }) => void
  onReconfigure: () => void
  onBackToEdit: () => void
  onConfirm: (state: NonNullable<ConfirmState>) => void
  onExported: () => void
}) {
  const document = useMemo(() => buildDocument(note), [note])
  const errors = issues.filter(issue => issue.level === 'error')
  const warnings = issues.filter(issue => issue.level === 'warning')

  async function exportDocx() {
    if (!window.workbench) return
    setExportState({ busy: true, result: null, error: null })
    try {
      const path = await window.workbench.chooseSave()
      if (!path) { setExportState({ busy: false, result: null, error: null }); return }
      const request = toExportRequest(note, path)
      const result = await window.workbench.work(request as unknown as Record<string, unknown>)
      setExportState({ busy: false, result, error: null })
      if (result.success) onExported()
    } catch (error) {
      setExportState({ busy: false, result: null, error: error instanceof Error ? error.message : '导出失败，请重试。' })
    }
  }

  return <div className="workspace preview-workspace">
    <div className="page-heading"><div><span className="eyebrow">STEP 03 · 生成说明</span><h1>连续预览并导出 DOCX。</h1><p>预览顺序与导出一致；导出的文件可用 Word/WPS 编辑，再到 CAD 中选择导入。</p></div><div className="heading-deco">03<span>/ 03</span></div></div>
    <div className="preview-layout">
      <section className="paper-panel panel">
        <div className="paper-head">连续预览 · 只读</div>
        <div className="paper">
          {document.blocks.map((block, index) => block.kind === 'title'
            ? <h1 key={index} className="paper-title">{block.text}</h1>
            : block.kind === 'meta'
              ? <p key={index} className="paper-meta">{block.text}</p>
              : block.kind === 'heading'
                ? <h2 key={index} className="paper-heading">{block.text}</h2>
                : <p key={index} className="paper-paragraph">{block.text}</p>)}
        </div>
      </section>
      <aside className="preview-side">
        <section className="panel side-block">
          <div className="panel-head"><span className="panel-index">✓</span><div><h2>导出前检查</h2><p>问题会明确定位，不会静默导出</p></div></div>
          {errors.length === 0 && warnings.length === 0 && <p className="ok-hint">没有发现问题，可以导出。</p>}
          {errors.length > 0 && <ul className="issue-list errors">{errors.map((issue, index) => <li key={index}>{issue.message}</li>)}</ul>}
          {warnings.length > 0 && <ul className="issue-list warnings">{warnings.map((issue, index) => <li key={index}>{issue.message}</li>)}</ul>}
        </section>
        <section className="panel side-block">
          <div className="panel-head"><span className="panel-index">↗</span><div><h2>导出 Word</h2><p>生成可编辑 DOCX，不覆盖已有文件</p></div></div>
          <button className="primary-button wide" disabled={blocking || exportState.busy} onClick={() => void exportDocx()}>{exportState.busy ? '正在导出…' : '导出 DOCX'} <span>↗</span></button>
          {exportState.error && <p className="error-hint">{exportState.error}</p>}
          {exportState.result && !exportState.result.success && <div className="export-result failed"><strong>导出未完成</strong><p>{exportState.result.message}</p>{exportState.result.diagnostics.length > 0 && <ul>{exportState.result.diagnostics.map((item, index) => <li key={index}>{item.message}</li>)}</ul>}</div>}
          {exportState.result?.success && <div className="export-result passed"><strong>导出成功</strong><p>{exportState.result.message} 读回检查识别到 {exportState.result.blocks} 个内容块。</p><button className="secondary-button" onClick={() => { if (exportState.result?.path) void window.workbench.openDocx(exportState.result.path) }}>打开这份 DOCX</button></div>}
          <div className="side-actions">
            <button className="secondary-button" onClick={onBackToEdit}>‹ 返回修改正文</button>
            <button className="secondary-button" onClick={() => onConfirm({ title: '重新配置', message: '返回 01 步修改专业或项目资料；当前章节与正文会保留，可在 02A 重新选择模板。', confirmLabel: '返回 01', danger: false, onConfirm: onReconfigure })}>重新配置</button>
          </div>
        </section>
      </aside>
    </div>
  </div>
}

function ConfirmDialog({ state, onCancel }: { state: NonNullable<ConfirmState>; onCancel: () => void }) {
  return <div className="modal-mask" onClick={onCancel}>
    <div className="modal confirm-modal" onClick={event => event.stopPropagation()}>
      <div className="modal-head"><strong>{state.title}</strong><button className="modal-close" onClick={onCancel}>×</button></div>
      <p className="confirm-message">{state.message}</p>
      <div className="confirm-actions">
        <button className="secondary-button" onClick={onCancel}>取消</button>
        <button className={state.danger ? 'danger-button' : 'primary-button'} onClick={() => { state.onConfirm(); onCancel() }}>{state.confirmLabel}</button>
      </div>
    </div>
  </div>
}

createRoot(document.getElementById('root')!).render(<React.StrictMode><App /></React.StrictMode>)
