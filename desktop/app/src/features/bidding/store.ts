import { existsSync, mkdirSync, readFileSync, readdirSync, renameSync, writeFileSync, statSync } from 'node:fs'
import { join, extname, basename } from 'node:path'
import { createHash, randomUUID } from 'node:crypto'
import { parseBid, reviewBid, isoNow, type BidProject, type BidSource, type BidSummary, type BidSave } from './model.ts'

const hash = (bytes: Uint8Array) => createHash('sha256').update(bytes).digest('hex')
function atomic(path: string, content: string | Uint8Array) {
  const temporary = path + '.' + randomUUID() + '.tmp'
  writeFileSync(temporary, content, { flag: 'wx' })
  // 失败临时文件保留用于诊断，不把失败显示为已保存。
  renameSync(temporary, path)
}
export function createBidStore(dataRoot: string) {
  const root = join(dataRoot, 'bids'), assets = join(root, 'assets')
  mkdirSync(assets, { recursive: true })
  const pathFor = (id: string) => {
    if (typeof id !== 'string' || !/^[A-Za-z0-9_-]{1,80}$/.test(id)) throw new Error('投标编号无效。')
    return join(root, id + '.json')
  }
  const assetPath = (asset: string) => {
    if (!/^[a-f0-9]{64}\.(docx|pdf|png|jpg|jpeg|txt)$/.test(asset)) throw new Error('附件编号无效。')
    return join(assets, asset)
  }
  function assetBytes(source: BidSource) {
    const path = assetPath(source.asset)
    if (!existsSync(path)) throw new Error('附件缺失，请重新导入：' + source.name)
    if (statSync(path).size > 20_000_000) throw new Error('附件过大。')
    const bytes = readFileSync(path)
    if (hash(bytes) !== source.hash || bytes.length !== source.bytes) throw new Error('附件校验不一致：' + source.name)
    return bytes
  }
  function load(id: string): { bid: BidProject; recovered: boolean } {
    const path = pathFor(id)
    const read = (p: string) => { const parsed = parseBid(JSON.parse(readFileSync(p, 'utf8'))); if (parsed.id !== id) throw new Error('文件与编号不一致。'); return parsed }
    try { return { bid: read(path), recovered: false } }
    catch (error) {
      if (existsSync(path + '.previous')) return { bid: read(path + '.previous'), recovered: true }
      throw new Error('投标草稿无法读取，请从备份恢复。' + (error instanceof Error ? error.message : ''))
    }
  }
  function save(value: unknown): BidSave {
    try {
      const bid = parseBid(value), path = pathFor(bid.id)
      let previous: BidProject | null = null
      if (existsSync(path) || existsSync(path + '.previous')) previous = load(bid.id).bid
      if ((previous?.revision ?? 0) !== bid.revision) throw new Error('草稿已在其他操作中更新，请重新打开后继续，避免覆盖。')
      // 来源只能引用本模块导入过的内容，不能指向任意本机路径。
      for (const source of bid.sources) if (!existsSync(assetPath(source.asset))) throw new Error('附件缺失：' + source.name)
      bid.updatedAt = isoNow(); bid.revision++
      if (previous) atomic(path + '.previous', JSON.stringify(previous))
      atomic(path, JSON.stringify(bid, null, 2))
      return { ok: true, revision: bid.revision, savedAt: bid.updatedAt }
    } catch (error) { return { ok: false, error: error instanceof Error ? error.message : '保存失败。' } }
  }
  function list(): BidSummary[] {
    const ids = new Set(readdirSync(root).filter(name => name.endsWith('.json') || name.endsWith('.json.previous')).map(name => name.replace(/\.json(\.previous)?$/, '')))
    return [...ids].map(id => {
      try { const { bid, recovered } = load(id); return { id, title: bid.title, type: bid.type, updatedAt: bid.updatedAt, sections: bid.sections.length, issues: reviewBid(bid).length, damaged: recovered } }
      catch { return { id, title: '损坏草稿 · ' + id, type: 'design' as const, updatedAt: '', sections: 0, issues: 1, damaged: true } }
    }).sort((a,b) => b.updatedAt.localeCompare(a.updatedAt))
  }
  function importAsset(file: string): BidSource {
    const extension = extname(file).toLowerCase()
    if (!['.docx','.pdf','.png','.jpg','.jpeg','.txt'].includes(extension)) throw new Error('请选择 DOCX、PDF、TXT 或图片。')
    if (statSync(file).size > 20_000_000) throw new Error('单个附件上限为20MB。')
    const bytes = readFileSync(file), digest = hash(bytes), asset = digest + extension, path = assetPath(asset)
    if (!existsSync(path)) atomic(path, bytes)
    else if (hash(readFileSync(path)) !== digest) throw new Error('已存附件校验失败，请先恢复备份。')
    return { id: randomUUID(), name: basename(file), asset, hash: digest, bytes: bytes.length, importedAt: isoNow(), kind: '招标文件', version: '1', supersedes: '', blocks: [], warnings: [], confirmed: false }
  }
  function backup(id: string) {
    const bid = load(id).bid
    const files: Record<string,string> = {}
    let total = 0
    for (const source of bid.sources) {
      if (files[source.asset]) continue
      const bytes = assetBytes(source); total += bytes.length
      if (total > 80_000_000) throw new Error('备份附件总量超过80MB，请按标段拆分。')
      files[source.asset] = bytes.toString('base64')
    }
    return JSON.stringify({ kind: 'engispace-bid-backup', schemaVersion: 1, bid, files })
  }
  function restore(json: string) {
    if (json.length > 115_000_000) throw new Error('备份过大。')
    const value = JSON.parse(json)
    if (value.kind !== 'engispace-bid-backup' || value.schemaVersion !== 1 || !value.files || typeof value.files !== 'object') throw new Error('请选择投标模块导出的备份。')
    const bid = parseBid(value.bid), checked = new Map<string,Buffer>()
    let total = 0
    for (const source of bid.sources) {
      const encoded = value.files[source.asset]
      if (typeof encoded !== 'string' || encoded.length > 27_000_000) throw new Error('备份附件无效。')
      const bytes = Buffer.from(encoded, 'base64'); total += checked.has(source.asset) ? 0 : bytes.length
      if (total > 80_000_000 || hash(bytes) !== source.hash || bytes.length !== source.bytes) throw new Error('备份附件校验失败。')
      checked.set(source.asset, bytes)
    }
    for (const [asset,bytes] of checked) { const path = assetPath(asset); if (existsSync(path)) { if (hash(readFileSync(path)) !== hash(bytes)) throw new Error('本机同名附件异常，未覆盖。') } else atomic(path, bytes) }
    bid.id = randomUUID(); bid.revision = 0; bid.title = (bid.title.slice(0, 150) + '（恢复副本）'); bid.createdAt = isoNow(); bid.exports = []
    const result = save(bid); if (!result.ok) throw new Error(result.error)
    return load(bid.id).bid
  }
  function duplicate(id: string) {
    const bid = load(id).bid; bid.id = randomUUID(); bid.revision = 0; bid.title = bid.title.slice(0, 150) + '（副本）'; bid.createdAt = isoNow(); bid.exports = []
    const result = save(bid); if (!result.ok) throw new Error(result.error)
    return load(bid.id).bid
  }
  return { root, load, save, list, importAsset, assetPath, assetBytes, backup, restore, duplicate }
}
