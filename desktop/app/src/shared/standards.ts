// 规范库：从当前资料库识别的编号与名称汇总，供设计依据清单添加使用。识别不等于版本核验。
import { parseContentCatalog, type SelectedModule } from './content.ts'
import type { Note, NoteSection } from './model.ts'
import { newId } from './model.ts'

export type StandardEntry = { code: string; name: string }

function collect(text: string, entries: Map<string, string>) {
  const normalized = text.replace(/[（]/g, '(').replace(/[）]/g, ')')
  for (const match of normalized.matchAll(/《([^》]{2,80})》\s*([A-Za-z][A-Za-z0-9/\-.]*\d[A-Za-z0-9/\-.]*)/g)) {
    const code = match[2].replace(/\s+/g, '')
    if (!entries.has(code)) entries.set(code, match[1].trim())
  }
}

export function standardsLibrary(catalog: ReturnType<typeof parseContentCatalog> | null): StandardEntry[] {
  const entries = new Map<string, string>()
  if (!catalog) return []
  for (const layout of catalog.layouts ?? []) for (const section of layout.sections) for (const block of section.blocks) {
    if (block.kind === 'paragraph') collect(block.template, entries)
    else for (const cell of block.rows.flat()) collect(cell, entries)
  }
  for (const clause of catalog.clauses) collect(clause.template, entries)
  return [...entries].map(([code, name]) => ({ code, name })).sort((a, b) => a.code.localeCompare(b.code))
}

export function isBasisSection(section: NoteSection): boolean {
  return section.title.includes('设计依据') || section.title.includes('依据')
}

export function isStandardModule(module: SelectedModule): boolean {
  return /(?:GB|JGJ|CJJ|CJ|HJ|DL|NB|CECS|DB\d{1,2}|\b\d{2}[A-Z]{1,3}\d{2,4})/i.test(module.template)
}

export function addStandardToNote(note: Note, sectionId: string, code: string, name: string): Note {
  const label = name ? `《${name}》${code}` : code
  const module: SelectedModule = {
    id: 'std-' + newId(),
    clauseId: '工程选入标准',
    packageId: note.assemblyPackageId ?? '',
    template: label,
    baseTemplate: label,
    fieldIds: [],
    sourceRefs: [],
    refs: [code],
    flags: [],
    reviewStatus: 'pending',
    confirmedForNote: true,
    edited: false,
  }
  return {
    ...note,
    assemblyReviewConfirmed: false,
    sections: note.sections.map(section => section.id !== sectionId ? section : {
      ...section,
      modules: [...(section.modules ?? []), module],
      layoutBlocks: [...(section.layoutBlocks ?? []), { kind: 'paragraph', moduleId: module.id }],
    }),
  }
}
