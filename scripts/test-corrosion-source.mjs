// 本机私有来源的防腐集成检查；只输出统计，不将原文/工程取值提交到仓库。
import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { createStore } from '../desktop/app/src/shared/store.ts'
import { assembleNote } from '../desktop/app/src/shared/assembly.ts'
import { createNote, parseNote, findIssues, toExportRequest } from '../desktop/app/src/shared/model.ts'
import { FIXED_MATERIAL_FIELDS } from '../desktop/app/src/shared/project-fields.ts'
const path = process.env.DSS_CONTENT_LIBRARY_PATH
assert.ok(path, '请设置本机私有资料目录')
const catalog = createStore(join(dirname(path), 'test-corrosion-source'), path).loadCatalog()
let exports = 0
for (const layout of catalog.layouts.filter(layout => layout.templateId.startsWith('tpl-struct'))) for (const level of ['微', '弱', '中', '强']) {
  const base = createNote('structural', layout.templateId, { name: '脱敏试验工程', number: '', owner: '', location: '测试地点' })
  Object.assign(base.structural, { siteCategory: 'II', seismicGrade: '乙', safetyLevel: '二级', foundationGrade: '乙级', protectionScheme: level, seismicIntensity: '7度（0.10g）' })
  base.fieldValues.foundation_type = '测试基础形式'
  let note = assembleNote(base, layout.templateId, catalog).note
  assert.equal(note.fieldValues.foundation_type, '测试基础形式')
  for (const id of FIXED_MATERIAL_FIELDS) assert.ok(!note.sections.some(section => section.modules?.some(module => module.fieldIds.includes(id))), '钢筋/焊条等应保留为固定原文')
  for (const id of Object.keys(note.fieldDefinitions)) if (!note.fieldValues[id]?.trim()) note.fieldValues[id] = '测试值'
  for (const section of note.sections) {
    for (const module of section.modules ?? []) module.confirmedForNote = true
    for (const block of section.layoutBlocks ?? []) if (block.kind === 'table') block.confirmedForNote = true
  }
  if (note.corrosionDesign) note.corrosionDesign.scopeConfirmed = true
  note.assemblyReviewConfirmed = true
  note = parseNote(JSON.parse(JSON.stringify(note)))
  assert.deepEqual(findIssues(note).filter(issue => issue.level === 'error'), [])
  const request = toExportRequest(note, join(dirname(path), `corrosion-${layout.templateId}-${level}-${process.pid}.docx`))
  const body = JSON.stringify(request.document)
  assert.ok(!body.includes('【待填写'))
  if (level === '中') assert.match(body, /C35/)
  if (level === '强') assert.match(body, /C40/)
  assert.ok(!/0\.08%%|mm重|50mmmm/.test(body))
  const worker = fileURLToPath(new URL('../desktop/worker/bin/Debug/net10.0/DocxWorkbench.Worker.dll', import.meta.url))
  const result = spawnSync(process.env.DSS_DOTNET_EXE, [worker], { input: JSON.stringify(request), encoding: 'utf8', windowsHide: true })
  assert.equal(result.status, 0, result.stderr)
  assert.equal(JSON.parse(result.stdout).success, true)
  exports++
}
console.log(`CORROSION_SOURCE_OK templates=3 levels=4 docx=${exports}`)
