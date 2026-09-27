import { existsSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'

export function findProjectCatalogPath(startDirectory: string, configured?: string): string {
  if (configured) return resolve(configured)
  let directory = resolve(startDirectory)
  while (true) {
    const candidate = join(directory, 'content-library', 'private', 'catalog.json')
    if (existsSync(candidate)) return candidate
    const parent = dirname(directory)
    if (parent === directory) return ''
    directory = parent
  }
}
