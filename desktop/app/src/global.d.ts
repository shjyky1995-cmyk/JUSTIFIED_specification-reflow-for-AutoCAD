export type WorkResult = {
  success: boolean
  message: string
  path: string | null
  blocks: number
  diagnostics: { code: string; severity: string; message: string }[]
}

declare global {
  interface Window {
    workbench: {
      chooseSave(): Promise<string | null>
      chooseOpen(): Promise<string | null>
      work(request: Record<string, unknown>): Promise<WorkResult>
      openDocx(path: string): Promise<void>
    }
  }
}
