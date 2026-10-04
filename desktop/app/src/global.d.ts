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
      bids: {
        list(): Promise<import('./features/bidding/model').BidSummary[]>
        load(id: string): Promise<{ bid: import('./features/bidding/model').BidProject; recovered: boolean }>
        save(bid: import('./features/bidding/model').BidProject): Promise<import('./features/bidding/model').BidSave>
        duplicate(id: string): Promise<import('./features/bidding/model').BidProject>
        importSource(): Promise<import('./features/bidding/model').BidSource | null>
        openSource(id: string, sourceId: string): Promise<void>
        exportDocx(id: string, mode: 'draft' | 'reviewed'): Promise<{ success: boolean; message: string; path: string } | null>
        backup(id: string): Promise<string | null>
        restore(): Promise<import('./features/bidding/model').BidProject | null>
      }
      chooseSave(name?: string): Promise<string | null>
      work(request: Record<string, unknown>): Promise<WorkResult>
      openDocx(path: string): Promise<void>
      notesList(): Promise<import('./shared/model').NoteSummary[]>
      noteLoad(id: string): Promise<import('./shared/model').Note>
      noteSave(note: import('./shared/model').Note): Promise<SaveResult>
      noteDelete(id: string): Promise<void>
      projectsList(): Promise<import('./shared/model').StoredProject[]>
      projectSave(project: import('./shared/model').StoredProject): Promise<import('./shared/model').StoredProject[]>
      catalogLoad(): Promise<import('./shared/content').ContentCatalog | null>
      catalogChoose(): Promise<import('./shared/content').ContentCatalog | null>
      appInfo(): Promise<{ version: string; dataRoot: string; catalogPath: string; packaged: boolean }>
      openDataFolder(): Promise<string>
      duplicateNote(id: string): Promise<import('./shared/model').Note>
      exportBackup(): Promise<string | null>
      importBackup(): Promise<number | null>
      beforeClose(callback: () => Promise<boolean>): () => void
    }
  }
}
