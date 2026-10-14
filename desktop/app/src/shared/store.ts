// 本机 JSON 存储：项目资料与说明草稿。供 Electron 主进程与 Node 自动化测试共用。
import { existsSync, mkdirSync, readFileSync, readdirSync, renameSync, writeFileSync, unlinkSync } from 'node:fs'
import { randomUUID } from 'node:crypto'
import { join } from 'node:path'
import { parseContentCatalog, parseContentLayout, type ContentCatalog } from './content.ts'
import {
  deserializeNote,
  parseNote,
  parseProject,
  serializeNote,
  summarizeNote,
  type Note,
  type NoteSummary,
  type StoredProject,
  newId,
  nowIso,
} from './model.ts'

export type SaveResult = { ok: true; savedAt: string } | { ok: false; error: string }

export type DesktopStore = {
  root: string
  listNotes: () => NoteSummary[]
  loadNote: (id: string) => Note
  saveNote: (note: Note) => SaveResult
  deleteNote: (id: string) => void
  listProjects: () => StoredProject[]
  saveProject: (project: StoredProject) => StoredProject[]
  loadCatalog: () => ContentCatalog | null
  importCatalog: (path: string) => ContentCatalog
  backup: () => string
  restoreBackup: (json: string) => number
  duplicateNote: (id: string) => Note
}

function ensureDir(path: string): void {
  if (!existsSync(path)) mkdirSync(path, { recursive: true })
}

function writeJsonAtomic(path: string, json: string): void {
  const temporary = path + '.' + randomUUID() + '.tmp'
  try {
    writeFileSync(temporary, json, { encoding: 'utf8', flag: 'wx' })
    renameSync(temporary, path)
  } finally { if (existsSync(temporary)) unlinkSync(temporary) }
}

function readJsonFile(path: string): string {
  return readFileSync(path, 'utf8')
}

export function createStore(root: string, projectCatalogPath?: string): DesktopStore {
  const notesDir = join(root, 'notes')
  const projectsFile = join(root, 'projects.json')
  const catalogDir = join(root, 'content-library')
  const catalogFile = join(catalogDir, 'catalog.json')

  function ensureRoot(): void {
    ensureDir(root)
    ensureDir(notesDir)
  }

  function notePath(id: string): string {
    if (!/^[A-Za-z0-9._-]+$/.test(id)) throw new Error('说明编号无效。')
    return join(notesDir, id + '.json')
  }

  return {
    root,

    listNotes() {
      if (!existsSync(notesDir)) return []
      const summaries: NoteSummary[] = []
      const candidates = [...new Set(readdirSync(notesDir).filter(name => name.endsWith('.json') || name.endsWith('.json.previous')).map(name => name.replace(/\.previous$/, '')))]
      for (const fileName of candidates) {
        try {
          const note = this.loadNote(fileName.replace(/\.json$/, ''))
          summaries.push({ ...summarizeNote(note), ...(note.recoveryMessage ? { title: (note.title || '未命名说明') + '（可恢复）' } : {}) })
        } catch {
          summaries.push({
            id: fileName.replace(/\.json$/, ''),
            title: '（损坏的说明文件）',
            discipline: 'other',
            templateId: '',
            sectionCount: 0,
            filledCount: 0,
            createdAt: '',
            updatedAt: '',
          })
        }
      }
      summaries.sort((left, right) => (right.updatedAt || '').localeCompare(left.updatedAt || ''))
      return summaries
    },

    loadNote(id) {
      const path = notePath(id)
      if (!existsSync(path) && !existsSync(path + '.previous')) throw new Error('找不到说明文件：' + id)
      try { return deserializeNote(readJsonFile(path)) }
      catch {
        if (!existsSync(path + '.previous')) throw new Error('说明文件损坏，且没有可恢复的上一版本。请导入备份。')
        const recovered = deserializeNote(readJsonFile(path + '.previous'))
        return { ...recovered, recoveryMessage: '文件损坏或缺失，已恢复上一次保存的内容；请核对并重新保存。' }
      }
    },

    saveNote(note) {
      try {
        const validated = parseNote(note)
        ensureRoot()
        const path = notePath(validated.id)
        if (existsSync(path)) {
          try {
            const previous = readJsonFile(path)
            deserializeNote(previous)
            writeJsonAtomic(path + '.previous', previous)
          } catch (error) {
            // 损坏正文允许由恢复版本修复；备份写入失败则必须报告。
            if (error instanceof Error && 'code' in error) throw error
          }
        }
        writeJsonAtomic(notePath(validated.id), serializeNote(validated))
        return { ok: true, savedAt: validated.updatedAt }
      } catch (error) {
        return { ok: false, error: error instanceof Error ? error.message : '保存失败，请重试。' }
      }
    },

    deleteNote(id) {
      const path = notePath(id)
      const suffix = '.' + randomUUID() + '.deleted'
      if (existsSync(path)) renameSync(path, path + suffix)
      if (existsSync(path + '.previous')) renameSync(path + '.previous', path + '.previous' + suffix)
    },

    listProjects() {
      if (!existsSync(projectsFile)) return []
      try {
        const value: unknown = JSON.parse(readJsonFile(projectsFile))
        if (!Array.isArray(value)) return []
        return value.map(item => parseProject(item)).filter(project => project.name.trim().length > 0)
      } catch {
        return []
      }
    },

    saveProject(project) {
      try {
        ensureRoot()
        const projects = this.listProjects()
        const index = projects.findIndex(item => item.id === project.id)
        const stamped: StoredProject = { ...parseProject(project), updatedAt: new Date().toISOString() }
        if (index >= 0) projects[index] = stamped
        else projects.unshift(stamped)
        writeJsonAtomic(projectsFile, JSON.stringify(projects, null, 2))
        return projects
      } catch (error) {
        throw error instanceof Error ? error : new Error('保存项目资料失败。')
      }
    },

    loadCatalog() {
      const path = projectCatalogPath === undefined ? catalogFile : projectCatalogPath
      if (!path || !existsSync(path)) return null
      const catalog = parseContentCatalog(JSON.parse(readJsonFile(path)))
      const directory = projectCatalogPath ? join(path, '..') : catalogDir
      const sourcesFile = join(directory, 'source-layouts.json')
      const legacyFile = join(directory, 'pool-layout.json')
      const layouts = existsSync(sourcesFile)
        ? (JSON.parse(readJsonFile(sourcesFile)) as unknown[]).map(parseContentLayout)
        : existsSync(legacyFile) ? [parseContentLayout(JSON.parse(readJsonFile(legacyFile)))] : catalog.layouts ?? []
      const fields = new Map(catalog.fields.map(field => [field.id, field]))
      for (const layout of layouts) {
        const source = catalog.sourceDigest.find(source => source.file === layout.sourceFile)
        if (source?.id !== layout.sourceId || (layout.sourceHash && source.sha256 !== layout.sourceHash)) throw new Error('原稿版式来源与资料包不一致。')
        for (const field of layout.fields ?? []) if (!fields.has(field.id)) fields.set(field.id, field)
      }
      return parseContentCatalog({ ...catalog, fields: [...fields.values()], layouts })
    },

    importCatalog(path) {
      const json = readJsonFile(path)
      if (json.length > 12_000_000) throw new Error('资料库文件过大。')
      const catalog = parseContentCatalog(JSON.parse(json))
      ensureDir(catalogDir)
      writeJsonAtomic(catalogFile, JSON.stringify(catalog))
      return catalog
    },

    duplicateNote(id) {
      const original = this.loadNote(id)
      const copy = parseNote({ ...original, id: newId(), title: original.title.slice(0, 110) + '（副本）', createdAt: nowIso(), updatedAt: nowIso() })
      const saved = this.saveNote(copy)
      if (!saved.ok) throw new Error(saved.error)
      return copy
    },

    backup() {
      const notes = this.listNotes().map(summary => this.loadNote(summary.id))
      return JSON.stringify({ kind: 'engispace-backup', schemaVersion: 1, createdAt: nowIso(), notes, projects: this.listProjects() }, null, 2)
    },

    restoreBackup(json) {
      if (json.length > 50_000_000) throw new Error('备份文件过大。')
      const value = JSON.parse(json) as { kind?: string; schemaVersion?: number; notes?: unknown[]; projects?: unknown[] }
      if (value.kind !== 'engispace-backup' || value.schemaVersion !== 1 || !Array.isArray(value.notes) || !Array.isArray(value.projects)) throw new Error('请选择本程序导出的备份文件。')
      if (value.notes.length > 1000 || value.projects.length > 1000) throw new Error('备份包含过多草稿或项目。')
      // 全部先校验，再写入；恢复生成新编号，不覆盖本机现有草稿。
      const notes = value.notes.map(raw => parseNote(raw)).map(note => ({ ...note, id: newId(), createdAt: nowIso(), updatedAt: nowIso() }))
      const projects = value.projects.map(raw => parseProject(raw)).map(project => ({ ...project, id: newId() }))
      const written: string[] = []
      ensureRoot()
      try {
        for (const note of notes) {
          const result = this.saveNote(note)
          if (!result.ok) throw new Error(result.error)
          written.push(notePath(note.id))
        }
        writeJsonAtomic(projectsFile, JSON.stringify([...this.listProjects(), ...projects], null, 2))
      } catch (error) {
        for (const path of written) if (existsSync(path)) unlinkSync(path)
        throw error
      }
      return notes.length
    },
  }
}
