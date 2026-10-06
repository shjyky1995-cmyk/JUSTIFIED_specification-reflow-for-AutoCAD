import React, { useEffect, useMemo, useRef, useState } from 'react'
import { createRoot } from 'react-dom/client'
import { ArrowLeft, ArrowRight, BriefcaseBusiness, Copy, FileText, FolderOpen, MoreHorizontal, PieChart, Plus, Search, Settings, Sparkles, X } from 'lucide-react'
import productIcon from '../../../branding/product-icon.png'
import { renderModule, renderTemplate, type ContentCatalog } from './shared/content'
import { assembleNote, templateReadiness } from './shared/assembly'
import { reviewQueue, referenceIndex, sourceIndex, extractReferences, type ReviewTarget } from './shared/review'
import { projectFields, projectFieldValues, fieldLabelsFor, isProjectField, CORE_FIELD_IDS } from './shared/project-fields'
import { applyCorrosionScheme, isCorrosionField, CORROSION_FIELD_LABELS } from './shared/corrosion'
import { CORROSION_SOURCES } from './shared/corrosion-rules'
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
  draftExportIssues,
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

const LINKED_FIELD_IDS = CORE_FIELD_IDS

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
  const [reviewFocus, setReviewFocus] = useState<(ReviewTarget & { serial: number }) | null>(null)
  const [sourceReviewOpen, setSourceReviewOpen] = useState(false)
  const [linkedProjectId, setLinkedProjectId] = useState<string | null>(null)
  const [exportState, setExportState] = useState<{ busy: boolean; result: WorkResult | null; error: string | null }>({ busy: false, result: null, error: null })
  const [catalog, setCatalog] = useState<ContentCatalog | null>(null)
  const [catalogError, setCatalogError] = useState('')
  const saveTimer = useRef<number | null>(null)
  const saveChain = useRef<Promise<boolean>>(Promise.resolve(true))
  const [settingsOpen, setSettingsOpen] = useState(false)
  const undoHistory = useRef<Note[]>([])
  const redoHistory = useRef<Note[]>([])

  function locateReview(target: ReviewTarget) {
    if (target.page === 'parameters') setRoute('step01')
    else if (target.page === 'editor') { setSelectedSectionId(target.sectionId ?? noteRef.current?.sections[0]?.id ?? null); setRoute('step02b') }
    else setRoute('step03')
    setReviewFocus({ ...target, serial: Date.now() })
  }

  function setWorkingNote(next: Note | null) {
    setReviewFocus(null)
    setSourceReviewOpen(false)
    if (saveTimer.current !== null) { window.clearTimeout(saveTimer.current); saveTimer.current = null }
    undoHistory.current = []
    redoHistory.current = []
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
    if (!window.workbench?.beforeClose) return
    return window.workbench.beforeClose(flushSave)
  }, [])

  useEffect(() => {
    const focusSearch = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') { event.preventDefault(); void commitSave(); return }
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

  function commitSave(): Promise<boolean> {
    if (saveTimer.current !== null) { window.clearTimeout(saveTimer.current); saveTimer.current = null }
    const current = noteRef.current
    if (!current) return Promise.resolve(true)
    if (!window.workbench) return Promise.resolve(false)
    const operation = saveChain.current.then(async () => {
      if (noteRef.current === current) setSave({ status: 'saving', message: '正在保存…' })
      try {
        const result = await window.workbench.noteSave(current)
        if (result.ok) {
          if (noteRef.current === current) setSave({ status: 'saved', message: '已保存 ' + formatTime(result.savedAt) })
          return true
        }
        if (noteRef.current === current) setSave({ status: 'error', message: result.error })
        return false
      } catch (error) {
        if (noteRef.current === current) setSave({ status: 'error', message: error instanceof Error ? error.message : '保存失败，请重试。' })
        return false
      }
    })
    saveChain.current = operation
    return operation
  }

  async function flushSave(): Promise<boolean> {
    // 等待保存期间仍可能收到最后一次输入；保存到引用稳定后才能离开。
    let snapshot: Note | null
    do {
      snapshot = noteRef.current
      if (!(await commitSave())) return false
    } while (noteRef.current !== snapshot)
    return true
  }

  function updateNote(mutate: NoteMutation) {
    const previous = noteRef.current
    const next = mutate(previous)
    if (!next) return
    if (previous) { undoHistory.current = [...undoHistory.current.slice(-29), previous]; redoHistory.current = [] }
    if (previous?.assemblyReviewConfirmed && next.assemblyReviewConfirmed) next.assemblyReviewConfirmed = false
    if (previous) {
      const before = effectiveFieldValues(previous), after = effectiveFieldValues(next)
      const changed = new Set([...Object.keys(before), ...Object.keys(after)].filter(id => before[id] !== after[id]))
      next.sections = next.sections.map(section => ({ ...section,
        modules: section.modules?.map(module => module.fieldIds.some(id => changed.has(id)) ? { ...module, confirmedForNote: false } : module),
        layoutBlocks: section.layoutBlocks?.map(block => block.kind === 'table' && [...block.rows.flat().join(' ').matchAll(/\{([a-z][a-z0-9_]*)\}/g)].some(match => changed.has(match[1])) ? { ...block, confirmedForNote: false } : block),
      }))
    }
    next.updatedAt = nowIso()
    noteRef.current = next
    setNote(next)
    scheduleSave()
  }

  function askConfirm(state: NonNullable<ConfirmState>) {
    setConfirm(state)
  }

  async function startNew() {
    if (!(await flushSave())) return
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
    if (!(await flushSave())) return
    try {
      const loaded = await window.workbench.noteLoad(id)
      setWorkingNote(loaded)
      setSave({ status: loaded.recoveryMessage ? 'error' : 'saved', message: loaded.recoveryMessage || '已恢复 ' + formatTime(loaded.updatedAt) })
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
      if (noteRef.current?.id === id) setWorkingNote(null)
      refreshHome()
    } catch (error) {
      setSave({ status: 'error', message: error instanceof Error ? error.message : '删除失败。' })
    }
  }

  const issues: NoteIssue[] = useMemo(() => (note ? findIssues(note) : []), [note])
  const blocking = issues.some(issue => issue.level === 'error')

  async function goHome() {
    if (!(await flushSave())) return
    setRoute('home')
    refreshHome()
  }

  function undo(redo = false) {
    const from = redo ? redoHistory : undoHistory
    const to = redo ? undoHistory : redoHistory
    const next = from.current.pop()
    if (!next || !noteRef.current) return
    to.current.push(noteRef.current)
    const restored = { ...next, updatedAt: nowIso() }
    noteRef.current = restored
    setNote(restored)
    scheduleSave()
  }

  async function duplicate(id: string) {
    if (!window.workbench || !(await commitSave())) return
    try {
      const copied = await window.workbench.duplicateNote(id)
      refreshHome()
      await openNote(copied.id)
    } catch (error) { setSave({ status: 'error', message: error instanceof Error ? error.message : '复制草稿失败。' }) }
  }

  return <div className="app-shell">
    <header className="global-header">
      <button className="global-brand" onClick={() => void goHome()} aria-label="返回首页"><img src={productIcon} alt="产品图标" /><span>EngiSpace</span></button>
      <div className="global-account">{note && route !== 'home' && <><button className="text-link" disabled={undoHistory.current.length === 0} onClick={() => undo()}>撤销</button><button className="text-link" disabled={redoHistory.current.length === 0} onClick={() => undo(true)}>重做</button><button className="text-link" onClick={() => void commitSave()}>保存</button></>}<span>离线工作台</span><button className="icon-button" aria-label="设置与备份" onClick={() => setSettingsOpen(true)}><Settings size={18} /></button></div>
    </header>
    {route !== 'home' && <StepNav route={route} note={note} onNavigate={target => {
      if (target === 'home') { void goHome(); return }
      if (!note) return
      setRoute(target)
    }} />}
    <main className={'main-area route-' + route}>
      {route === 'home' && <HomePage notes={notes} projects={projects} search={search} onSearch={setSearch} onNew={() => void startNew()} onContinue={id => void openNote(id)} onCopy={id => void duplicate(id)} onDelete={id => askConfirm({ title: '删除说明', message: '确定删除这份本机说明草稿？导出的 DOCX 文件不会被删除。', confirmLabel: '删除草稿', danger: true, onConfirm: () => { void deleteNote(id) } })} />}
      {route === 'step01' && <Step01 focus={reviewFocus} note={note} catalog={catalog} projects={projects} linkedProjectId={linkedProjectId} setLinkedProjectId={setLinkedProjectId} onUpdate={updateNote} onConfirm={askConfirm} onDone={async () => {
        if (!noteRef.current) setWorkingNote(createNote('structural', customTemplateId('structural'), { name: '', number: '', owner: '', location: '' }, defaultTitle('structural')))
        if (!(await commitSave())) return
        if (noteRef.current) await rememberProject(noteRef.current, projects, setProjects)
        setRoute(noteRef.current?.sections.length ? 'step02b' : 'step02a')
      }} />}
      {route === 'step02a' && note && <Step02A note={note} catalog={catalog} catalogError={catalogError} onBack={() => setRoute('step01')} onSelect={templateId => {
        const current = noteRef.current
        if (!current) return
        const hasEdits = Object.keys(current.fieldValues).length > 0 || current.sections.some(section => section.custom || (section.layoutBlocks?.length ?? 0) > 0 || section.modules?.some(module => module.edited) || section.body.trim() !== resetSectionBody(section).trim())
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
      {route === 'step02b' && note && <Step02B focus={reviewFocus} note={note} save={save} selectedSectionId={selectedSectionId} setSelectedSectionId={setSelectedSectionId} libraryOpen={libraryOpen} setLibraryOpen={setLibraryOpen} onUpdate={updateNote} onBack={() => setRoute('step02a')} onParameters={() => setRoute('step01')} onConfirm={askConfirm} onGenerate={async () => {
        if (!(await commitSave())) return
        setExportState({ busy: false, result: null, error: null })
        setRoute('step03')
      }} />}
      {route === 'step03' && note && <Step03 onLocate={locateReview} onSources={() => setSourceReviewOpen(true)} note={note} issues={issues} blocking={blocking} exportState={exportState} setExportState={setExportState} onUpdate={updateNote} onReconfigure={() => setRoute('step01')} onBackToEdit={() => setRoute('step02b')} onConfirm={askConfirm} onExported={() => refreshHome()} />}
    </main>
    {!canUseDesktop && route !== 'home' && <p className="browser-note">网页预览只展示界面；请从桌面程序中保存与导出文件。</p>}
    {save.status === 'error' && <div className="app-error" role="alert">{save.message}<button className="text-link" onClick={() => void commitSave()}>重试保存</button></div>}
    {settingsOpen && <SettingsDialog onClose={() => setSettingsOpen(false)} onSave={flushSave} onRefresh={() => { refreshHome() }} onCatalog={next => { setCatalog(next); setCatalogError('') }} />}
    {sourceReviewOpen && note && <SourceReviewDialog note={note} catalog={catalog} onClose={() => setSourceReviewOpen(false)} onLocate={target => { setSourceReviewOpen(false); locateReview(target) }} />}
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

function HomePage({ notes, projects, search, onSearch, onNew, onContinue, onCopy, onDelete }: {
  notes: NoteSummary[]
  projects: StoredProject[]
  search: string
  onSearch: (value: string) => void
  onNew: () => void
  onContinue: (id: string) => void
  onDelete: (id: string) => void
  onCopy: (id: string) => void
}) {
  const keyword = search.trim().toLowerCase()
  const recent = keyword.length === 0 ? notes : notes.filter(item => item.title.toLowerCase().includes(keyword) || disciplineLabel(item.discipline).includes(keyword))
  const visibleProjects = keyword.length === 0 ? projects : projects.filter(item => `${item.name} ${item.number} ${item.owner} ${item.location}`.toLowerCase().includes(keyword))
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
        <small>暂未开放</small>
      </div>)}
    </section>

    <section className="home-columns">
      <div className="home-panel">
        <div className="panel-head"><div><h2>当前项目</h2></div></div>
        {visibleProjects.length === 0 ? <p className="empty-hint">{keyword ? '没有匹配的项目。' : '暂无项目，新建说明时填写'}</p> : <ul className="recent-list">{visibleProjects.slice(0, 4).map(item => <li key={item.id}><span className="list-icon"><FolderOpen size={15} /></span><div className="recent-info"><strong>{item.name}</strong><small>{item.number || '未填写编号'} · {item.owner || '未填写建设单位'}</small></div></li>)}</ul>}
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
              <button className="icon-button" title="复制为新草稿" onClick={() => onCopy(item.id)}><Copy size={15} /></button>
              <button className="icon-button" title="删除" onClick={() => onDelete(item.id)}><X size={15} /></button>
            </div>
          </li>)}
        </ul>}
      </div>
    </section>
  </div>
}

function useReviewFocus(focus: (ReviewTarget & { serial: number }) | null, page: ReviewTarget['page']) {
  useEffect(() => {
    if (!focus || focus.page !== page) return
    const frame = requestAnimationFrame(() => {
      const key = focus.field ? 'field' : focus.moduleId ? 'module' : focus.tablePara !== undefined ? 'table' : 'section'
      const value = focus.field ?? focus.moduleId ?? (focus.tablePara !== undefined ? String(focus.tablePara) : focus.sectionId ?? '')
      const target = document.querySelector<HTMLElement>(`[data-review-${key}="${CSS.escape(value)}"]`)
      if (!target) return
      target.scrollIntoView({ block: 'center', behavior: 'instant' })
      target.focus({ preventScroll: true })
      target.classList.add('review-highlight')
      target.addEventListener('blur', () => target.classList.remove('review-highlight'), { once: true })
    })
    return () => cancelAnimationFrame(frame)
  }, [focus, page])
}

function Step01({ focus, note, catalog, projects, linkedProjectId, setLinkedProjectId, onUpdate, onConfirm, onDone }: {
  focus: (ReviewTarget & { serial: number }) | null
  note: Note | null
  catalog: ContentCatalog | null
  projects: StoredProject[]
  linkedProjectId: string | null
  setLinkedProjectId: (id: string | null) => void
  onUpdate: (mutate: NoteMutation) => void
  onConfirm: (state: NonNullable<ConfirmState>) => void
  onDone: () => void
}) {
  useReviewFocus(focus, 'parameters')
  const discipline = note?.discipline ?? 'structural'
  const project = note?.project ?? { name: '', number: '', owner: '', location: '' }
  const title = note?.title ?? defaultTitle(discipline)
  const structural = note?.structural ?? null
  const baseForFields = note ?? createNote(discipline, customTemplateId(discipline), project, title)
  const parameterFields = projectFields(baseForFields)
  const parameterValues = projectFieldValues(baseForFields)
  const parameterGroups = [...new Set(parameterFields.map(field => field.group))]
  const materialFields = Object.entries(baseForFields.fieldDefinitions).filter(([id]) => isCorrosionField(id) || id === 'external_anticorrosion_coating')

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

  function changeParameter(id: string, value: string, label: string, unit: string, aliases: string[] = []) {
    onUpdate(current => {
      const base = current ?? createNote(discipline, customTemplateId(discipline), project, title)
      let next = setFieldValue({ ...base, fieldDefinitions: { ...base.fieldDefinitions, [id]: { label, unit } } }, id, value)
      for (const alias of aliases) next = setFieldValue(next, alias, value)
      return next
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
        <label className="field wide"><span>说明标题</span><input data-review-field="title" value={title} maxLength={120} onChange={event => onUpdate(current => {
          const base = current ?? createNote(discipline, customTemplateId(discipline), project, title)
          return { ...base, title: event.target.value }
        })} placeholder="如：结构设计说明" /></label>
        <label className="field wide"><span>关联项目</span>
          <select value={linkedProjectId ?? ''} onChange={event => linkProject(event.target.value)}>
            <option value="">不关联项目</option>
            {projects.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}
          </select>
        </label>
        <label className="field"><span>项目名称</span><input data-review-field="project.name" value={project.name} maxLength={120} onChange={event => changeProject({ name: event.target.value })} placeholder="如果不关联项目，请在此输入..." /></label>
        <label className="field"><span>工程编号</span><input data-review-field="project.number" value={project.number} maxLength={60} onChange={event => changeProject({ number: event.target.value })} placeholder="选填" /></label>
        <label className="field"><span>建设单位</span><input data-review-field="project.owner" value={project.owner} maxLength={120} onChange={event => changeProject({ owner: event.target.value })} placeholder="选填" /></label>
        <label className="field"><span>建设地点</span><input data-review-field="project.location" value={project.location} maxLength={120} onChange={event => changeProject({ location: event.target.value })} placeholder="选填" /></label>
      </div>
    </section>

    {discipline === 'structural' && <section className="panel structural-panel">
      <div className="panel-head"><div><h2>结构专业参数</h2></div></div>
      <p className="group-label">基本设计参数</p>
      <div className="form-grid structural-grid">
        <label className="field"><span>场地类别 <b>*</b></span>
          <select data-review-field="structural.siteCategory" value={structural?.siteCategory ?? ''} onChange={event => changeStructural({ siteCategory: event.target.value })}>
            <option value="">请选择</option>
            {SITE_CATEGORIES.map(item => <option key={item} value={item}>{item}</option>)}
          </select>
        </label>
        <label className="field"><span>抗震设防类别 <b>*</b></span>
          <select data-review-field="structural.seismicGrade" value={structural?.seismicGrade ?? ''} onChange={event => changeStructural({ seismicGrade: event.target.value })}>
            <option value="">请选择</option>
            {SEISMIC_GRADES.map(item => <option key={item} value={item}>{item}</option>)}
          </select>
        </label>
        <label className="field"><span>结构安全等级 <b>*</b></span>
          <select data-review-field="structural.safetyLevel" value={structural?.safetyLevel ?? ''} onChange={event => changeStructural({ safetyLevel: event.target.value })}>
            <option value="">请选择</option>
            {SAFETY_LEVELS.map(item => <option key={item} value={item}>{item}</option>)}
          </select>
        </label>
        <label className="field"><span>地基基础设计等级 <b>*</b></span>
          <select data-review-field="structural.foundationGrade" value={structural?.foundationGrade ?? ''} onChange={event => changeStructural({ foundationGrade: event.target.value })}>
            <option value="">请选择</option>
            {FOUNDATION_GRADES.map(item => <option key={item} value={item}>{item}</option>)}
          </select>
        </label>
        <label className="field"><span>抗震设防烈度</span><select data-review-field="structural.seismicIntensity" value={structural?.seismicIntensity === SEISMIC_INTENSITY_PENDING ? '' : structural?.seismicIntensity ?? ''} onChange={event => changeStructural({ seismicIntensity: event.target.value })}><option value="">请选择当地取值</option>{SEISMIC_INTENSITIES.map(item => <option key={item} value={item}>{item}</option>)}</select></label>
        <label className="field"><span>设计使用年限</span><input data-review-field="structural.designLifeYears" type="number" min={1} max={200} value={structural?.designLifeYears ?? 50} onChange={event => changeStructural({ designLifeYears: Number(event.target.value) })} /></label>
      </div>
      <div className="structural-subsection"><p className="group-label">地勘水土腐蚀性完整结论</p><textarea data-review-field="structural.corrosion" value={structural?.corrosion ?? ''} maxLength={4000} onChange={event => changeStructural({ corrosion: event.target.value })} placeholder="请粘贴地勘报告中的水土腐蚀性结论……" rows={3} /></div>
      <div className="structural-subsection"><p className="group-label">材料与防腐方案</p><div className="corrosion-options" tabIndex={-1} data-review-field="structural.protectionScheme">{PROTECTION_SCHEMES.map(item => <button type="button" key={item} className={'corrosion-option ' + (structural?.protectionScheme === item ? 'active' : '')} onClick={() => onUpdate(current => applyCorrosionScheme(current ?? createNote(discipline, customTemplateId(discipline), project, title), item))}><span className="radio-dot" />{item}腐蚀</button>)}</div><p className="content-guidance">微、弱、中、强按地勘及介质分别判定；此处选择受腐蚀部位的控制等级。弱/中/强自动生成50年普通钢筋混凝土候选要求，保留原稿更严格取值；微腐蚀需按环境类别另行核定。池内介质、抗渗、抗冻、抗硫酸盐及预应力、桩基另核定。</p>
      {(baseForFields.corrosionDesign || materialFields.length > 0) && <div className="corrosion-design">
        <p>当前方案：{structural?.protectionScheme}腐蚀。下列材料参数供核对和手改，后续正文自动引用。</p>
        <div className="content-field-grid">{[...new Map([...Object.entries(CORROSION_FIELD_LABELS), ...materialFields].map(([id, definition]) => [id, definition])).entries()].map(([id, definition]) => <label className="field" key={id}><span>{definition.label}{definition.unit ? `（${definition.unit}）` : ''}</span><input data-review-field={id} value={baseForFields.fieldValues[id] ?? ''} onChange={event => changeParameter(id, event.target.value, definition.label, definition.unit)} placeholder="未提供依据，请核定后填写" /></label>)}</div>
        <p className="content-guidance">来源：GB/T 50046-2018 表4.2.3、4.2.5、4.8.5-1；垫层为候选做法，需核定耐腐蚀材料与厚度，保护层按受腐蚀部位及构件类别取值。<a href={CORROSION_SOURCES.materials} target="_blank" rel="noreferrer">材料与保护层依据</a> · <a href={CORROSION_SOURCES.foundation} target="_blank" rel="noreferrer">基础与垫层依据</a></p>
        {baseForFields.corrosionDesign && <label className="content-confirm"><input type="checkbox" checked={baseForFields.corrosionDesign.scopeConfirmed} onChange={event => onUpdate(current => current?.corrosionDesign ? { ...current, corrosionDesign: { ...current.corrosionDesign, scopeConfirmed: event.target.checked }, assemblyReviewConfirmed: false } : current)} /> 已核对本方案用于50年普通钢筋混凝土的受腐蚀部位，材料、垫层及表面防护适用（非预应力、非桩基）</label>}
      </div>}
      <label className="field extra-field"><span>附加防腐措施 (可选)</span><input value={structural?.protectionExtra ?? ''} maxLength={120} onChange={event => changeStructural({ protectionExtra: event.target.value })} placeholder="按工程实际填写" /></label></div>
    </section>}

    {parameterGroups.map(group => <section className="panel project-parameter-panel" key={group}><div className="panel-head"><div><h2>{group} · 项目共用参数</h2><p>在这里手填一次，各章节、预览和导出自动引用；选模板后显示该模板的补充字段。未填也可导出草稿。</p></div></div><div className="form-grid">{parameterFields.filter(field => field.group === group).map(field => <label className="field" key={field.id}><span>{field.label}{field.unit ? `（${field.unit}）` : ''}</span><input data-review-field={field.id} value={parameterValues[field.id] ?? ''} maxLength={4000} onChange={event => changeParameter(field.id, event.target.value, field.label, field.unit, field.aliases)} placeholder="按本工程资料手填（可稍后补充）" /></label>)}</div></section>)}
    {discipline === 'structural' && <section className="panel"><label className="field"><span>设计基本地震加速度（与所选烈度对应）</span><input value={effectiveFieldValues(baseForFields).seismic_acceleration ?? ''} readOnly placeholder="请先选择设防烈度" /></label></section>}

    <div className="action-bar">
      <div className="action-buttons">
        <button className="primary-button" onClick={onDone}>{note?.sections.length ? '返回章节编制' : '下一步：选择模板'} <ArrowRight size={15} /></button>
      </div>
    </div>
  </div>
}

function Step02A({ note, catalog, catalogError, onBack, onSelect }: { note: Note; catalog: ContentCatalog | null; catalogError: string; onBack: () => void; onSelect: (templateId: string) => void }) {
  const templates = templatesFor(note.discipline)
  const catalogReady = Boolean(catalog)
  return <div className="workspace">
    <button className="back-link" onClick={onBack}><ArrowLeft size={14} /> 返回参数设置</button>
    <div className="page-heading"><div><h1>选择说明模板</h1><p>选定后自动装配对应来源的章节与正文，下一步直接核对并填写工程取值。</p>{!catalogReady && <p className="error-text">{catalogError || 'G 盘项目内未找到资料库；预设模板暂时只能生成空章节。'}</p>}</div></div>
    <div className="template-grid">
      {templates.map(template => {
        const readiness = templateReadiness(template.id, catalog)
        return <button key={template.id} disabled={!catalogReady && !template.custom && !['tpl-struct-steel', 'tpl-other-standard'].includes(template.id)} className={note.templateId === template.id && note.sections.length > 0 ? 'template-card active' : 'template-card'} onClick={() => onSelect(template.id)}>
        <span className="template-icon"><FileText size={18} /></span>
        <strong>{template.name}</strong>
        <small>{template.custom ? '从空白开始，自由组合标准章节。' : readiness.available ? `${readiness.chapters} 章 · ${readiness.paragraphs} 段 · ${readiness.tables} 表；候选正文待核定。` : '暂无可靠正文来源，先建立章节框架。'}</small>
        {readiness.sourceFile && <small className="template-source">来源：{readiness.sourceFile}{readiness.version ? ` · ${readiness.version}` : ''}</small>}
        {template.id === 'tpl-plumb-standard' && readiness.available && <small>现有来源为污水处理厂工艺总图：覆盖处理工艺、运行与施工说明；未覆盖给水系统与用水定额、排水体制与管网、消防给水、管材与防腐保温等通用给排水主题，需按本工程范围在 02B 补充。</small>}
      </button>})}
    </div>
  </div>
}

function Step02B({ focus, note, save, selectedSectionId, setSelectedSectionId, libraryOpen, setLibraryOpen, onUpdate, onBack, onParameters, onConfirm, onGenerate }: {
  focus: (ReviewTarget & { serial: number }) | null
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
  onParameters: () => void
}) {
  useReviewFocus(focus, 'editor')
  const fieldValues = effectiveFieldValues(note)
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
  const usedFields = [...new Set([...(selected?.modules ?? []).flatMap(module => module.fieldIds), ...(selected?.layoutBlocks ?? []).flatMap(block => block.kind === 'table' ? [...block.rows.flat().join(' ').matchAll(/\{([a-z][a-z0-9_]*)\}/g)].map(match => match[1]) : [])])]
  const orderedBlocks = selected?.layoutBlocks?.length ? selected.layoutBlocks : (selected?.modules ?? []).map(module => ({ kind: 'paragraph' as const, moduleId: module.id }))
  const fieldLabels = fieldLabelsFor(note)

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
          <div className="editor-scroll" key={selected.id} tabIndex={0} role="region" aria-label="章节正文，可向下滚动">
          <div className="editor-head">
            <div><input data-review-section={selected.id} className="chapter-title-input" aria-label="章节标题" value={selected.title} maxLength={120} placeholder="章节标题" onChange={event => onUpdate(current => current ? { ...current, sections: current.sections.map(section => section.id === selected.id ? { ...section, title: event.target.value } : section) } : current)} /><p>{selected.custom ? '当前说明专用章节，不写回标准库' : '标准章节 · 可改为本说明正文'}</p></div>
          </div>
          {(chapterId || (selected.modules?.length ?? 0) > 0 || orderedBlocks.length > 0) && <div className="content-modules">
            <p className="content-guidance">原说明正文和表格按原顺序完整展开。项目共用参数在 01 统一填写；本章只填写专属取值，再核对规范与适用性。固定文字可展开手改。</p>
            {usedFields.some(id => isProjectField(id, note.fieldDefinitions[id]?.label) || isCorrosionField(id)) && <button className="secondary-button" onClick={onParameters}>查看 / 修改 01 项目参数</button>}
            {usedFields.some(id => !isProjectField(id, note.fieldDefinitions[id]?.label) && !((isCorrosionField(id) || id === 'external_anticorrosion_coating'))) && <div className="content-field-grid">{usedFields.filter(id => !isProjectField(id, note.fieldDefinitions[id]?.label) && !((isCorrosionField(id) || id === 'external_anticorrosion_coating'))).map(fieldId => <label className="field" key={fieldId}><span>{note.fieldDefinitions[fieldId]?.label ?? fieldId}{note.fieldDefinitions[fieldId]?.unit ? `（${note.fieldDefinitions[fieldId].unit}）` : ''}</span><input data-review-field={fieldId} value={fieldValues[fieldId] ?? ''} readOnly={LINKED_FIELD_IDS.has(fieldId)} onChange={event => onUpdate(currentNote => currentNote ? setFieldValue(currentNote, fieldId, event.target.value) : currentNote)} placeholder={LINKED_FIELD_IDS.has(fieldId) ? '请在 01 参数设置中填写' : '填写本工程取值；请按上方句子核对单位'} /></label>)}</div>}
            {orderedBlocks.map(block => {
              if (block.kind === 'paragraph') {
                const module = selected.modules?.find(item => item.id === block.moduleId)
                if (!module) return <p key={block.moduleId}>段落缺失，请恢复草稿。</p>
                return <div key={module.id} data-review-module={module.id} tabIndex={-1} className="content-module">
              <div className="content-module-head"><strong>{module.clauseId}</strong><span>{module.edited ? '本份文字已改写' : extractReferences(module.template).length > 0 ? '规范引用待核对' : '来源待核定'}</span><button className="text-link" onClick={() => onUpdate(currentNote => currentNote ? { ...currentNote, sections: currentNote.sections.map(section => section.id === selected.id ? { ...section, modules: (section.modules ?? []).filter(item => item.id !== module.id), layoutBlocks: section.layoutBlocks?.filter(item => item.kind !== 'paragraph' || item.moduleId !== module.id) } : section) } : currentNote)}>移除</button></div>
              <p>{renderModule(module, fieldValues, fieldLabels).text}</p>
              <small>来源：{module.sourceRefs.map(source => `${source.file ?? source.sourceId} 第 ${source.para} 段`).join('；')}{module.refs.length > 0 ? ` · 来源关联引用 ${module.refs.join('、')}` : ''}</small>
              <details className="content-edit"><summary>修改这份说明中的条款文字</summary><textarea value={module.template} maxLength={10000} onChange={event => onUpdate(currentNote => currentNote ? setModuleTemplate(currentNote, selected.id, module.id, event.target.value) : currentNote)} /><small>修改后需重新确认；来源只用于追溯原候选，不代表改写文字已经全局核定。</small><button className="text-link" disabled={!module.edited} onClick={() => onUpdate(currentNote => currentNote ? setModuleTemplate(currentNote, selected.id, module.id, module.baseTemplate) : currentNote)}>恢复选入时文字</button></details>
              {(!note.assemblyPackageId || module.flags.includes('requires_applicability_review')) && <label className="content-confirm"><input type="checkbox" checked={module.confirmedForNote} onChange={event => onUpdate(currentNote => currentNote ? { ...currentNote, assemblyReviewConfirmed: false, sections: currentNote.sections.map(section => section.id === selected.id ? { ...section, modules: (section.modules ?? []).map(item => item.id === module.id ? { ...item, confirmedForNote: event.target.checked } : item) } : section) } : currentNote)} /> 已核对本条适用于当前工程</label>}
            </div>
              }
              return <div key={`table-${block.sourcePara}`} data-review-table={block.sourcePara} tabIndex={-1} className="content-module"><div className="content-module-head"><strong>原稿第 {block.sourcePara} 处表格</strong><span>{block.reviewNote}</span><button className="text-link" onClick={() => onUpdate(currentNote => currentNote ? { ...currentNote, assemblyReviewConfirmed: false, sections: currentNote.sections.map(section => section.id !== selected.id ? section : { ...section, layoutBlocks: section.layoutBlocks?.filter(item => item.kind !== 'table' || item.sourcePara !== block.sourcePara) }) } : currentNote)}>移除表格</button></div><div className="layout-table-wrap"><table className="layout-table">{block.columnWidths && <colgroup>{block.columnWidths.map((width, index) => <col key={index} style={{ width: `${width / block.columnWidths!.reduce((sum, value) => sum + value, 0) * 100}%` }} />)}</colgroup>}<tbody>{block.rows.map((row, rowIndex) => <tr key={rowIndex}>{row.map((cell, columnIndex) => <td key={columnIndex}><span>{renderTemplate(cell, fieldValues, fieldLabels).text}</span><details><summary>修改文字</summary><input aria-label={`表格 ${block.sourcePara} 第 ${rowIndex + 1} 行第 ${columnIndex + 1} 列`} value={cell} placeholder={renderTemplate(cell, fieldValues, fieldLabels).text} onChange={event => onUpdate(currentNote => currentNote ? { ...currentNote, assemblyReviewConfirmed: false, sections: currentNote.sections.map(section => section.id !== selected.id ? section : { ...section, layoutBlocks: section.layoutBlocks?.map(item => item.kind !== 'table' || item.sourcePara !== block.sourcePara ? item : { ...item, confirmedForNote: false, rows: item.rows.map((tableRow, ri) => tableRow.map((value, ci) => ri === rowIndex && ci === columnIndex ? event.target.value : value)) }) }) } : currentNote)} /></details></td>)}</tr>)}</tbody></table></div>{block.reviewNote.startsWith('条件') && <label className="content-confirm"><input type="checkbox" checked={block.confirmedForNote === true} onChange={event => onUpdate(currentNote => currentNote ? { ...currentNote, assemblyReviewConfirmed: false, sections: currentNote.sections.map(section => section.id !== selected.id ? section : { ...section, layoutBlocks: section.layoutBlocks?.map(item => item.kind === 'table' && item.sourcePara === block.sourcePara ? { ...item, confirmedForNote: event.target.checked } : item) }) } : currentNote)} /> 本工程包含该类构件，表格适用</label>}</div>})}

          </div>}
          <textarea className="section-editor" value={selected.body} maxLength={100000} onChange={event => onUpdate(currentNote => currentNote ? { ...currentNote, sections: currentNote.sections.map(section => section.id === selected.id ? { ...section, body: event.target.value } : section) } : currentNote)} placeholder={findSectionDefinition(selected.id)?.body ?? '可在这里补充或改写本章的纯文本正文。'} />
          <div className="editor-foot">
            <span className="char-count">{selected.body.length} 字</span>
          </div>
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

function Step03({ onLocate, onSources, note, issues, blocking, exportState, setExportState, onUpdate, onReconfigure, onBackToEdit, onConfirm, onExported }: {
  onLocate: (target: ReviewTarget) => void
  onSources: () => void
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
  const queue = useMemo(() => reviewQueue(note, issues), [note, issues])
  const errors = queue.filter(item => item.issue.level === 'error')
  const warnings = queue.filter(item => item.issue.level === 'warning')
  const draftBlocked = draftExportIssues(note).length > 0

  async function exportDocx(mode: 'draft' | 'reviewed' = 'draft') {
    if (!window.workbench) return
    setExportState({ busy: true, result: null, error: null })
    try {
      const path = await window.workbench.chooseSave(note.project.name.trim() ? `${note.project.name}-${note.title}` : note.title)
      if (!path) { setExportState({ busy: false, result: null, error: null }); return }
      const request = toExportRequest(note, path, mode)
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
                : block.kind === 'table'
                  ? <div key={index} className="layout-table-wrap"><table className="layout-table">{block.columnWidths && <colgroup>{block.columnWidths.map((width, index) => <col key={index} style={{ width: `${width / block.columnWidths!.reduce((sum, value) => sum + value, 0) * 100}%` }} />)}</colgroup>}<tbody>{block.rows.map((row, rowIndex) => <tr key={rowIndex}>{row.map((cell, columnIndex) => <td key={columnIndex}>{cell}</td>)}</tr>)}</tbody></table><small>{block.reviewNote}</small></div>
                : <p key={index} className="paper-paragraph">{block.text}</p>)}
        </div>
      </section>
      <aside className="preview-side">
        <button className="secondary-button wide" onClick={onSources}>查看正文来源与规范引用</button>
        <details open className={'panel side-block issue-disclosure ' + (errors.length > 0 ? 'has-errors' : '')}>
          <summary>{errors.length > 0 ? `待完善：${errors.length} 项（仍可导出草稿）` : warnings.length > 0 ? `导出前检查：${warnings.length} 项提示` : '导出前检查：已通过'}</summary>
          {errors.length === 0 && warnings.length === 0 && <p className="ok-hint">没有发现问题，可以导出。</p>}
          {errors.length > 0 && <ul className="issue-list errors">{errors.map((item, index) => <ReviewQueueRow key={index} item={item} note={note} onLocate={onLocate} />)}</ul>}
          {warnings.length > 0 && <ul className="issue-list warnings">{warnings.map((item, index) => <ReviewQueueRow key={index} item={item} note={note} onLocate={onLocate} />)}</ul>}
        </details>
        <section className="panel side-block">
          {note.assemblyPackageId && <label className="content-confirm"><input type="checkbox" checked={note.assemblyReviewConfirmed === true} onChange={event => onUpdate(current => current ? { ...current, assemblyReviewConfirmed: event.target.checked } : current)} /> 我已核对本工程的整篇说明、适用条件和规范引用</label>}
          <p className="empty-hint">参数可稍后补充。导出草稿保留当前文字、表格和待填写标记，可在 Word/WPS 继续修改。</p>
          <button className="primary-button wide" disabled={draftBlocked || exportState.busy} onClick={() => void exportDocx()}>{exportState.busy ? '正在导出…' : '导出 Word 草稿'} <span>↗</span></button>
          <button className="secondary-button wide" disabled={blocking || exportState.busy} onClick={() => void exportDocx('reviewed')}>导出已核对说明</button>
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

function ReviewQueueRow({ item, note, onLocate }: { item: ReturnType<typeof reviewQueue>[number]; note: Note; onLocate: (target: ReviewTarget) => void }) {
  return <li className="review-queue-row"><p>{item.issue.message}</p>
    {item.target.page !== 'preview' && <button className="text-link" onClick={() => onLocate(item.target)}>{item.target.page === 'parameters' ? '去填写参数' : '定位正文'}</button>}
    {item.occurrences.length > 1 && <details><summary>出现 {item.occurrences.length} 处</summary><ul>{item.occurrences.map((issue, index) => <li key={index}>
      {issue.sectionId ? <button className="text-link" onClick={() => onLocate({ page: 'editor', sectionId: issue.sectionId, moduleId: issue.moduleId, tablePara: issue.tablePara })}>{note.sections.find(section => section.id === issue.sectionId)?.title || '章节'}{issue.tablePara !== undefined ? ` · 第${issue.tablePara}处表格` : issue.moduleId ? ' · 正文段落' : ''}</button> : '项目参数'}
    </li>)}</ul></details>}
  </li>
}

function SourceReviewDialog({ note, catalog, onClose, onLocate }: { note: Note; catalog: ContentCatalog | null; onClose: () => void; onLocate: (target: ReviewTarget) => void }) {
  const [search, setSearch] = useState('')
  const refs = useMemo(() => referenceIndex(note), [note])
  const sources = useMemo(() => sourceIndex(note, catalog), [note, catalog])
  const query = search.trim().toLowerCase().replace(/\s+/g, '')
  const filtered = refs.filter(ref => ref.code.toLowerCase().includes(query) || ref.occurrences.some(item => item.sectionTitle.toLowerCase().includes(query)))
  return <div className="modal-mask" onClick={onClose}><section className="modal source-review-modal" role="dialog" aria-modal="true" aria-label="正文来源与规范引用" onClick={event => event.stopPropagation()}>
    <div className="modal-head"><strong>正文来源与规范引用</strong><button className="modal-close" onClick={onClose}>×</button></div>
    <div className="settings-body">
      <p>当前说明：{note.title || '未命名说明'} · {note.sections.length} 章 · {refs.length} 种已识别编号。编号来自当前正文，包括你修改的文字；尚需核对有效版本和本工程适用性。</p>
      <h3>正文来源</h3>
      {sources.length === 0 && <p>未记录可关联的段落来源；手写文字及表格来源仍需人工核对。</p>}
      {sources.map(source => <details className="source-entry" key={source.id}><summary>{source.file} · {source.paragraphs} 个来源段落</summary>
        <p>用于：{source.sections.join('、')}</p>
        {source.digest ? <><p>当前资料库登记的版本关系：{source.digest.versionRelation || '未记录'}；使用权限：{source.digest.rightsStatus || '待确认'}。</p><small className="local-path">来源校验值：{source.digest.sha256 || '未记录'}</small><p>校验值用于区分来源文件，不能证明工程结论正确。</p></> : <p>当前资料库与此草稿的来源版本未关联，保留草稿记载的来源；不要据此认定版本一致。</p>}
      </details>)}
      <h3>当前正文引用</h3>
      <label className="field"><span>查找编号或章节</span><input value={search} onChange={event => setSearch(event.target.value)} placeholder="如 GB50010，或章节名称" /></label>
      {filtered.length === 0 && <p>{refs.length ? '没有匹配的引用。' : '未识别到常见编号；名称、地方标准或其他格式仍需人工核对。'}</p>}
      <div className="reference-list">{filtered.map(ref => <details className="source-entry" key={ref.code}><summary>{ref.code} · {ref.occurrences.length} 处 · 待核定</summary>
        {ref.occurrences.map((item, index) => <div className="reference-occurrence" key={index}><button className="text-link" onClick={() => onLocate({ page: 'editor', sectionId: item.sectionId, moduleId: item.moduleId, tablePara: item.tablePara })}>{item.sectionTitle || '未命名章节'}{item.tablePara !== undefined ? ' · 表格' : ''} · 定位</button><small>{item.source}{item.edited ? ' · 本份文字可编辑，原来源不证明当前文字已核定' : ''}</small></div>)}
      </details>)}</div>
      <p>本工程确认只记录当前说明的核对状态，不会把旧资料升级为全局批准标准。确认后再修改参数或正文，需要重新核对。</p>
    </div>
  </section></div>
}

function SettingsDialog({ onClose, onSave, onRefresh, onCatalog }: { onClose: () => void; onSave: () => Promise<boolean>; onRefresh: () => void; onCatalog: (catalog: ContentCatalog) => void }) {
  const [info, setInfo] = useState<{ version: string; dataRoot: string; catalogPath: string; packaged: boolean } | null>(null)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')
  useEffect(() => { if (window.workbench) void window.workbench.appInfo().then(setInfo).catch(error => setMessage(String(error))) }, [])

  async function action(operation: 'backup' | 'restore' | 'catalog' | 'folder') {
    if (!window.workbench) return
    setBusy(true)
    setMessage('')
    try {
      if (operation === 'backup') {
        if (!(await onSave())) throw new Error('当前草稿保存失败，请先重试保存。')
        const path = await window.workbench.exportBackup()
        if (path) setMessage('备份已保存：' + path)
      } else if (operation === 'restore') {
        if (!(await onSave())) throw new Error('当前草稿保存失败，请先重试保存。')
        const count = await window.workbench.importBackup()
        if (count !== null) { setMessage(`已恢复 ${count} 份草稿副本，请在首页最近工作中打开。`); onRefresh() }
      } else if (operation === 'catalog') {
        const catalog = await window.workbench.catalogChoose()
        if (catalog) { onCatalog(catalog); setInfo(await window.workbench.appInfo()); setMessage('资料库已更新；已有草稿保留当前正文，新建说明使用新资料。') }
      } else {
        const error = await window.workbench.openDataFolder()
        if (error) throw new Error(error)
      }
    } catch (error) { setMessage(error instanceof Error ? error.message : '操作失败，请重试。') }
    finally { setBusy(false) }
  }

  return <div className="modal-mask" onClick={() => { if (!busy) onClose() }}><section className="modal settings-modal" role="dialog" aria-modal="true" aria-label="设置与备份" onClick={event => event.stopPropagation()}>
    <div className="modal-head"><strong>设置与备份</strong><button className="modal-close" disabled={busy} onClick={onClose}>×</button></div>
    <div className="settings-body">
      <p>EngiSpace · 设计说明 {info?.version ?? ''} · {info?.packaged ? '离线客户端' : '开发试用版'}</p>
      <h3>草稿与项目</h3><p>自动保存到本机，关闭窗口前保存最后修改。备份可转移到另一台电脑，恢复时新增副本。</p>
      <div className="settings-actions"><button className="secondary-button" disabled={busy} onClick={() => void action('backup')}>导出全部备份</button><button className="secondary-button" disabled={busy} onClick={() => void action('restore')}>从备份恢复</button><button className="text-link" disabled={busy} onClick={() => void action('folder')}>打开数据文件夹</button></div>
      {info && <small className="local-path">{info.dataRoot}</small>}
      <h3>正文资料库</h3><p>选择资料库目录中的 catalog.json。来源版式与原稿保存在同一目录；旧工程候选资料仍需逐项核定。</p>
      <button className="secondary-button" disabled={busy} onClick={() => void action('catalog')}>选择本机资料库</button><small className="local-path">{info?.catalogPath || '未配置；可使用自定义章节编制。'}</small>
      <h3>使用方法</h3><ol><li>新建设计说明，选专业并填写已知参数。</li><li>选模板，修改章节文字和表格；缺项可以后补充。</li><li>生成预览并导出 Word 草稿，在 Word/WPS 继续编辑。</li></ol><p>桌面端交付到 DOCX。需要落图时自行使用 CAD 插件 DSS 选择最终文件。</p>
      {busy && <p role="status">正在处理…</p>}{message && <p className="settings-message" role="status">{message}</p>}
    </div>
  </section></div>
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
