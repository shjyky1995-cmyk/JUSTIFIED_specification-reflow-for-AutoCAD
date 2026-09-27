export type WorkResult = {
  success: boolean
  message: string
  path: string | null
  blocks: number
  headings: string[]
  diagnostics: { code: string; severity: string; message: string }[]
}

export type SaveResult = { ok: true; savedAt: string } | { ok: false; error: string }

declare global {
  interface Window {
    workbench: {
      chooseSave(): Promise<string | null>
      work(request: Record<string, unknown>): Promise<WorkResult>
      openDocx(path: string): Promise<void>
      notesList(): Promise<import('./shared/model').NoteSummary[]>
      noteLoad(id: string): Promise<import('./shared/model').Note>
      noteSave(note: import('./shared/model').Note): Promise<SaveResult>
      noteDelete(id: string): Promise<void>
      projectsList(): Promise<import('./shared/model').StoredProject[]>
      projectSave(project: import('./shared/model').StoredProject): Promise<import('./shared/model').StoredProject[]>
    }
  }
}
