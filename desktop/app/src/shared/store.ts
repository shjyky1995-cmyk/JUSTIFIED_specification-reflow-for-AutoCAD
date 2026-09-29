// 本机 JSON 存储：项目资料与说明草稿。供 Electron 主进程与 Node 自动化测试共用。
import { existsSync, mkdirSync, readFileSync, readdirSync, renameSync, writeFileSync } from 'node:fs'
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
}

function ensureDir(path: string): void {
  if (!existsSync(path)) mkdirSync(path, { recursive: true })
}

function writeJsonAtomic(path: string, json: string): void {
  const temporary = path + '.' + Date.now().toString(36) + '.tmp'
  writeFileSync(temporary, json, 'utf8')
  renameSync(temporary, path)
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
      for (const fileName of readdirSync(notesDir)) {
        if (!fileName.endsWith('.json')) continue
        try {
          const note = deserializeNote(readJsonFile(join(notesDir, fileName)))
          summaries.push(summarizeNote(note))
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
      if (!existsSync(path)) throw new Error('找不到说明文件：' + id)
      return deserializeNote(readJsonFile(path))
    },

    saveNote(note) {
      try {
        const validated = parseNote(note)
        ensureRoot()
        writeJsonAtomic(notePath(validated.id), serializeNote(validated))
        return { ok: true, savedAt: validated.updatedAt }
      } catch (error) {
        return { ok: false, error: error instanceof Error ? error.message : '保存失败，请重试。' }
      }
    },

    deleteNote(id) {
      const path = notePath(id)
      if (existsSync(path)) renameSync(path, path + '.' + Date.now().toString(36) + '.deleted')
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
  }
}
