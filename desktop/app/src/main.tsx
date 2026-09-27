import React, { useEffect, useMemo, useRef, useState } from 'react'
import { createRoot } from 'react-dom/client'
import { ArrowLeft, ArrowRight, BriefcaseBusiness, FileText, FolderOpen, MoreHorizontal, PieChart, Plus, Search, Sparkles, X } from 'lucide-react'
import productIcon from '../../../branding/product-icon.png'
import { renderModule, type ContentCatalog } from './shared/content'
import { assembleNote } from './shared/assembly'
import {
  buildDocument,
  createNote,
  customTemplateId,
  defaultStructuralParams,
  defaultTitle,
  effectiveFieldValues,
  disciplineLabel,
  DISCIPLINES,
  findSectionDefinition,
  findIssues,
  FOUNDATION_GRADES,
  isSectionEmpty,
  librarySections,
  newId,
  nowIso,
  PROTECTION_SCHEMES,
  resetSectionBody,
  setFieldValue,
  setModuleTemplate,
  SAFETY_LEVELS,
  SEISMIC_GRADES,
  SEISMIC_INTENSITIES,
  SEISMIC_INTENSITY_PENDING,
  sectionTitle,
  SITE_CATEGORIES,
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
  { route: 'step02b', label: '02 模板与章节' },
  { route: 'step03', label: '03 生成说明' },
]

const LINKED_FIELD_IDS = new Set(['project_name', 'project_location', 'structural_safety_level', 'foundation_design_grade', 'seismic_intensity', 'site_class', 'design_life', 'seismic_fortification_category'])

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
  const [catalog, setCatalog] = useState<ContentCatalog | null>(null)
  const [catalogError, setCatalogError] = useState('')
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

  useEffect(() => {
    refreshHome()
    if (window.workbench) void window.workbench.catalogLoad().then(setCatalog).catch(error => setCatalogError(error instanceof Error ? error.message : '资料库读取失败。'))
  }, [])

  useEffect(() => {
    const focusSearch = (event: KeyboardEvent) => {
      if (route === 'home' && (event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault()
        document.querySelector<HTMLInputElement>('.home-search')?.focus()
      }
    }
    window.addEventListener('keydown', focusSearch)
    return () => window.removeEventListener('keydown', focusSearch)
  }, [route])

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
    const previous = noteRef.current
    const next = mutate(previous)
    if (!next) return
    if (previous?.assemblyReviewConfirmed && next.assemblyReviewConfirmed) next.assemblyReviewConfirmed = false
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
    <header className="global-header">
      <button className="global-brand" onClick={() => { setRoute('home'); refreshHome() }} aria-label="返回首页"><img src={productIcon} alt="产品图标" /><span>EngiSpace</span></button>
      <div className="global-account"><span>离线工作台</span><span className="global-avatar">设</span></div>
    </header>
    {route !== 'home' && <StepNav route={route} note={note} onNavigate={target => {
      if (target === 'home') { setRoute('home'); refreshHome(); return }
      if (!note) return
      setRoute(target)
    }} />}
    <main className={'main-area route-' + route}>
      {route === 'home' && <HomePage notes={notes} projects={projects} search={search} onSearch={setSearch} onNew={startNew} onContinue={id => void openNote(id)} onDelete={id => askConfirm({ title: '删除说明', message: '确定删除这份本机说明草稿？导出的 DOCX 文件不会被删除。', confirmLabel: '删除草稿', danger: true, onConfirm: () => { void deleteNote(id) } })} />}
      {route === 'step01' && <Step01 note={note} projects={projects} linkedProjectId={linkedProjectId} setLinkedProjectId={setLinkedProjectId} onUpdate={updateNote} onConfirm={askConfirm} onDone={async () => {
        if (!noteRef.current) setWorkingNote(createNote('structural', customTemplateId('structural'), { name: '', number: '', owner: '', location: '' }, defaultTitle('structural')))
        if (!(await commitSave())) return
        if (noteRef.current) await rememberProject(noteRef.current, projects, setProjects)
        setRoute('step02a')
      }} />}
      {route === 'step02a' && note && <Step02A note={note} catalogReady={Boolean(catalog)} catalogError={catalogError} onBack={() => setRoute('step01')} onSelect={templateId => {
        const current = noteRef.current
        if (!current) return
        const hasEdits = Object.keys(current.fieldValues).length > 0 || current.sections.some(section => section.custom || section.modules?.some(module => module.edited) || section.body.trim() !== resetSectionBody(section).trim())
        const apply = () => {
          const assembled = assembleNote(current, templateId, catalog)
          if (catalog && !assembled.sourceFile && !templateId.endsWith('-custom') && !['tpl-struct-steel', 'tpl-other-standard'].includes(templateId)) {
            setCatalogError('资料库缺少此模板对应的来源文件，无法自动装配。')
            return
          }
          const next = assembled.note
          const sections = next.sections
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
      {route === 'step03' && note && <Step03 note={note} issues={issues} blocking={blocking} exportState={exportState} setExportState={setExportState} onUpdate={updateNote} onReconfigure={() => setRoute('step01')} onBackToEdit={() => setRoute('step02b')} onConfirm={askConfirm} onExported={() => refreshHome()} />}
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
      <button className="home-link" onClick={() => onNavigate('home')} aria-label="退出编制"><X size={16} /></button>
      <strong className="workflow-title">新建设计说明</strong>
    </div>
      <nav className="step-nav">
        {STEP_LABELS.map((step, index) => <React.Fragment key={step.route}><button className={route === step.route || (step.route === 'step02b' && route === 'step02a') ? 'active' : ''} disabled={!note} onClick={() => onNavigate(step.route)}><span className="step-number">0{index + 1}</span>{step.label.slice(3)}</button>{index < 2 && <span className="step-divider">—</span>}</React.Fragment>)}
      </nav>
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
  return <div className="home">
    <div className="home-search-wrap"><Search size={16} /><input className="home-search" value={search} onChange={event => onSearch(event.target.value)} placeholder="搜索项目、文档或功能..." /><kbd>Ctrl K</kbd></div>

    <section className="entry-grid">
      <button className="entry-card active" onClick={onNew}>
        <span className="entry-icon"><FileText size={18} /></span>
        <strong>设计说明</strong>
        <small>起草并管理技术设计说明</small>
      </button>
      {['可研报告', '投标文件', '项目管理', 'AI 工程助手'].map(name => <div className="entry-card" key={name}>
        <span className="entry-icon">{name === 'AI 工程助手' ? <Sparkles size={18} /> : name === '项目管理' ? <FolderOpen size={18} /> : name === '可研报告' ? <PieChart size={18} /> : <BriefcaseBusiness size={18} />}</span>
        <strong>{name}</strong>
        <small>{name === '可研报告' ? '独立起草可行性研究报告' : name === '投标文件' ? '准备和管理项目招投标文件' : name === '项目管理' ? '组织与管理全局项目信息' : '自动化文档校审与检查'}</small>
      </div>)}
    </section>

    <section className="home-columns">
      <div className="home-panel">
        <div className="panel-head"><div><h2>当前项目</h2></div></div>
        {projects.length === 0 ? <p className="empty-hint">暂无项目，新建说明时填写</p> : <ul className="recent-list">{projects.slice(0, 4).map(item => <li key={item.id}><span className="list-icon"><FolderOpen size={15} /></span><div className="recent-info"><strong>{item.name}</strong><small>{item.number || '未填写编号'} · {item.owner || '未填写建设单位'}</small></div></li>)}</ul>}
      </div>
      <div className="home-panel wide">
        <div className="panel-head"><div><h2>最近工作</h2></div><span className="panel-count">{recent.length} 份</span></div>
        {recent.length === 0 ? <p className="empty-hint">{notes.length === 0 ? '还没有说明。点击「设计说明」开始新建。' : '没有匹配的说明。'}</p> : <ul className="recent-list">
          {recent.map(item => <li key={item.id}>
            <span className="list-icon"><FileText size={15} /></span>
            <div className="recent-info">
              <strong>{item.title}</strong>
              <small>设计说明 · {disciplineLabel(item.discipline)} · {item.filledCount}/{item.sectionCount} 章已填写</small>
            </div>
            <span className="recent-time">{formatDateTime(item.updatedAt)}</span>
            <div className="recent-actions">
              <button className="icon-button" title="继续" onClick={() => onContinue(item.id)}><ArrowRight size={16} /></button>
              <button className="icon-button" title="删除" onClick={() => onDelete(item.id)}><X size={15} /></button>
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
        fieldValues: {},
        fieldDefinitions: {},
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

  return <div className="workspace narrow">
    <section className="panel discipline-panel">
      <div className="panel-head"><div><h2>专业</h2></div></div>
      <div className="discipline-grid">
        {DISCIPLINES.map(item => <button key={item.code} className={discipline === item.code ? 'discipline-card active' : 'discipline-card'} onClick={() => changeDiscipline(item.code)}>
          <strong>{item.label}</strong>
        </button>)}
      </div>
    </section>

    <section className="panel project-panel">
      <div className="panel-head"><div><h2>关联项目 <span className="optional">(可选)</span></h2></div></div>
      <div className="form-grid">
        <label className="field wide"><span>说明标题</span><input value={title} maxLength={120} onChange={event => onUpdate(current => {
          const base = current ?? createNote(discipline, customTemplateId(discipline), project, title)
          return { ...base, title: event.target.value }
        })} placeholder="如：结构设计说明" /></label>
        <label className="field wide"><span>关联项目</span>
          <select value={linkedProjectId ?? ''} onChange={event => linkProject(event.target.value)}>
            <option value="">不关联项目</option>
            {projects.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}
          </select>
        </label>
        <label className="field"><span>项目名称</span><input value={project.name} maxLength={120} onChange={event => changeProject({ name: event.target.value })} placeholder="如果不关联项目，请在此输入..." /></label>
        <label className="field"><span>工程编号</span><input value={project.number} maxLength={60} onChange={event => changeProject({ number: event.target.value })} placeholder="选填" /></label>
        <label className="field"><span>建设单位</span><input value={project.owner} maxLength={120} onChange={event => changeProject({ owner: event.target.value })} placeholder="选填" /></label>
        <label className="field"><span>建设地点</span><input value={project.location} maxLength={120} onChange={event => changeProject({ location: event.target.value })} placeholder="选填" /></label>
      </div>
    </section>

    {discipline === 'structural' && <section className="panel structural-panel">
      <div className="panel-head"><div><h2>结构专业参数</h2></div></div>
      <p className="group-label">基本设计参数</p>
      <div className="form-grid structural-grid">
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
        <label className="field"><span>抗震设防烈度</span><select value={structural?.seismicIntensity === SEISMIC_INTENSITY_PENDING ? '' : structural?.seismicIntensity ?? ''} onChange={event => changeStructural({ seismicIntensity: event.target.value })}><option value="">请选择当地取值</option>{SEISMIC_INTENSITIES.map(item => <option key={item} value={item}>{item}</option>)}</select></label>
        <label className="field"><span>设计使用年限</span><input value="50 年" readOnly /></label>
      </div>
      <div className="structural-subsection"><p className="group-label">水土腐蚀性</p><textarea value={structural?.corrosion ?? ''} maxLength={4000} onChange={event => changeStructural({ corrosion: event.target.value })} placeholder="请粘贴地勘报告中的水土腐蚀性结论……" rows={3} /></div>
      <div className="structural-subsection"><p className="group-label">材料与防腐方案</p><div className="corrosion-options">{PROTECTION_SCHEMES.map(item => <button type="button" key={item} className={'corrosion-option ' + (structural?.protectionScheme === item ? 'active' : '')} onClick={() => changeStructural({ protectionScheme: item })}><span className="radio-dot" />{item}腐蚀</button>)}</div><label className="field extra-field"><span>附加防腐措施 (可选)</span><input value={structural?.protectionExtra ?? ''} maxLength={120} onChange={event => changeStructural({ protectionExtra: event.target.value })} placeholder="按工程实际填写" /></label></div>
    </section>}

    <div className="action-bar">
      <div className="action-buttons">
        <button className="primary-button" onClick={onDone}>下一步：选择模板 <ArrowRight size={15} /></button>
      </div>
    </div>
  </div>
}

function Step02A({ note, catalogReady, catalogError, onBack, onSelect }: { note: Note; catalogReady: boolean; catalogError: string; onBack: () => void; onSelect: (templateId: string) => void }) {
  const templates = templatesFor(note.discipline)
  return <div className="workspace">
    <button className="back-link" onClick={onBack}><ArrowLeft size={14} /> 返回参数设置</button>
    <div className="page-heading"><div><h1>选择说明模板</h1><p>选定后自动装配对应来源的章节与正文，下一步直接核对并填写工程取值。</p>{!catalogReady && <p className="error-text">{catalogError || 'G 盘项目内未找到资料库；预设模板暂时只能生成空章节。'}</p>}</div></div>
    <div className="template-grid">
      {templates.map(template => <button key={template.id} disabled={!catalogReady && !template.custom && !['tpl-struct-steel', 'tpl-other-standard'].includes(template.id)} className={note.templateId === template.id && note.sections.length > 0 ? 'template-card active' : 'template-card'} onClick={() => onSelect(template.id)}>
        <span className="template-icon"><FileText size={18} /></span>
        <strong>{template.name}</strong>
        <small>{template.custom ? '从空白开始，自由组合标准章节。' : template.sectionIds.map(id => sectionTitle(id)).slice(0, 3).join('、') + '等章节。'}</small>
      </button>)}
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
    onUpdate(currentNote => currentNote ? { ...currentNote, sections: [...currentNote.sections, { id: definition.id, title: definition.title, body: '', custom: false }] } : currentNote)
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

  const chapterId = selected?.id.startsWith('lib-CH') ? selected.id.slice(4) : ''
  const usedFields = [...new Set((selected?.modules ?? []).flatMap(module => module.fieldIds))]

  return <div className="workspace editor-workspace">
    <div className="editor-shell">
    <div className="editor-toolbar"><button className="back-link" onClick={onBack}><ArrowLeft size={14} /> 模板选择</button><button className="primary-button" disabled={note.sections.length === 0} onClick={onGenerate}>生成说明 <ArrowRight size={14} /></button></div>
    <div className="editor-grid">
      <aside className="section-panel panel">
        <div className="panel-head"><div><h2>章节目录</h2><p>{note.sections.length} 章 · 已填 {filled} 章</p></div></div>
        <button className="secondary-button add-section" onClick={() => setLibraryOpen(true)}><Plus size={14} /> 添加章节</button>
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
      </aside>

      <section className="editor-panel panel">
        {selected ? <div className="editor-body">
          <div className="editor-utility"><span className={'save-state ' + save.status}>{saveStatusText(save)}</span>{!selected.custom && <button className="text-link" onClick={() => onUpdate(currentNote => currentNote ? { ...currentNote, sections: currentNote.sections.map(section => section.id === selected.id ? { ...section, body: resetSectionBody(section) } : section) } : currentNote)}>清空手写正文</button>}</div>
          <div className="editor-head">
            <div><h2>{selected.title}</h2><p>{selected.custom ? '当前说明专用章节，不写回标准库' : '标准章节 · 可改为本说明正文'}</p></div>
          </div>
          {chapterId && <div className="content-modules">
            <p className="content-guidance">本章已按模板自动装配。请核对文字、规范引用与适用性，并填写本工程取值；有疑问的条款可移除或修改。</p>
            {(selected.modules ?? []).map(module => <div key={module.id} className="content-module">
              <div className="content-module-head"><strong>{module.clauseId}</strong><span>{module.edited ? '本份文字已改写' : module.refs.length > 0 ? '规范引用待核对' : '来源待核定'}</span><button className="text-link" onClick={() => onUpdate(currentNote => currentNote ? { ...currentNote, sections: currentNote.sections.map(section => section.id === selected.id ? { ...section, modules: (section.modules ?? []).filter(item => item.id !== module.id) } : section) } : currentNote)}>移除</button></div>
              <p>{renderModule(module, effectiveFieldValues(note), Object.fromEntries(Object.entries(note.fieldDefinitions).map(([id, definition]) => [id, definition.label]))).text}</p>
              <small>来源：{module.sourceRefs.map(source => `${source.file ?? source.sourceId} 第 ${source.para} 段`).join('；')}{module.refs.length > 0 ? ` · 引用 ${module.refs.join('、')}` : ''}</small>
              <details className="content-edit"><summary>修改这份说明中的条款文字</summary><textarea value={module.template} maxLength={10000} onChange={event => onUpdate(currentNote => currentNote ? setModuleTemplate(currentNote, selected.id, module.id, event.target.value) : currentNote)} /><small>修改后需重新确认；来源只用于追溯原候选，不代表改写文字已经全局核定。</small><button className="text-link" disabled={!module.edited} onClick={() => onUpdate(currentNote => currentNote ? setModuleTemplate(currentNote, selected.id, module.id, module.baseTemplate) : currentNote)}>恢复选入时文字</button></details>
              {!note.assemblyPackageId && <label className="content-confirm"><input type="checkbox" checked={module.confirmedForNote} onChange={event => onUpdate(currentNote => currentNote ? { ...currentNote, sections: currentNote.sections.map(section => section.id === selected.id ? { ...section, modules: (section.modules ?? []).map(item => item.id === module.id ? { ...item, confirmedForNote: event.target.checked } : item) } : section) } : currentNote)} /> 已核对本条适用于当前工程</label>}
            </div>)}
            {usedFields.length > 0 && <div className="content-field-grid">{usedFields.map(fieldId => <label className="field" key={fieldId}><span>{note.fieldDefinitions[fieldId]?.label ?? fieldId}{note.fieldDefinitions[fieldId]?.unit ? `（${note.fieldDefinitions[fieldId].unit}）` : ''}</span><input value={effectiveFieldValues(note)[fieldId] ?? ''} readOnly={LINKED_FIELD_IDS.has(fieldId)} onChange={event => onUpdate(currentNote => currentNote ? setFieldValue(currentNote, fieldId, event.target.value) : currentNote)} placeholder={LINKED_FIELD_IDS.has(fieldId) ? '请在 01 参数设置中填写' : '填写本工程取值；请按上方句子核对单位'} /></label>)}</div>}
          </div>}
          <textarea className="section-editor" value={selected.body} maxLength={100000} onChange={event => onUpdate(currentNote => currentNote ? { ...currentNote, sections: currentNote.sections.map(section => section.id === selected.id ? { ...section, body: event.target.value } : section) } : currentNote)} placeholder={findSectionDefinition(selected.id)?.body ?? '可在这里补充或改写本章的纯文本正文。'} />
          <div className="editor-foot">
            <span className="char-count">{selected.body.length} 字</span>
          </div>
        </div> : <div className="editor-empty"><p>从左侧选择章节开始编辑，或添加新章节。</p></div>}
      </section>
    </div>
    </div>

    {libraryOpen && <AddSectionModal available={available} onAddStandard={addStandardSection} onAddCustom={addCustomSection} onClose={() => setLibraryOpen(false)} />}

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

function Step03({ note, issues, blocking, exportState, setExportState, onUpdate, onReconfigure, onBackToEdit, onConfirm, onExported }: {
  note: Note
  issues: NoteIssue[]
  blocking: boolean
  exportState: { busy: boolean; result: WorkResult | null; error: string | null }
  setExportState: (value: { busy: boolean; result: WorkResult | null; error: string | null }) => void
  onUpdate: (mutate: NoteMutation) => void
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
    <div className="preview-layout">
      <section className="paper-panel panel">
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
        <details className={'panel side-block issue-disclosure ' + (errors.length > 0 ? 'has-errors' : '')}>
          <summary>{errors.length > 0 ? `导出前检查：${errors.length} 项待补齐` : warnings.length > 0 ? `导出前检查：${warnings.length} 项提示` : '导出前检查：已通过'}</summary>
          {errors.length === 0 && warnings.length === 0 && <p className="ok-hint">没有发现问题，可以导出。</p>}
          {errors.length > 0 && <ul className="issue-list errors">{errors.map((issue, index) => <li key={index}>{issue.message}</li>)}</ul>}
          {warnings.length > 0 && <ul className="issue-list warnings">{warnings.map((issue, index) => <li key={index}>{issue.message}</li>)}</ul>}
        </details>
        <section className="panel side-block">
          {note.assemblyPackageId && <label className="content-confirm"><input type="checkbox" checked={note.assemblyReviewConfirmed === true} onChange={event => onUpdate(current => current ? { ...current, assemblyReviewConfirmed: event.target.checked } : current)} /> 我已核对本工程的整篇说明、适用条件和规范引用</label>}
          <button className="primary-button wide" disabled={blocking || exportState.busy} onClick={() => void exportDocx()}>{exportState.busy ? '正在导出…' : '导出 Word'} <span>↗</span></button>
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
