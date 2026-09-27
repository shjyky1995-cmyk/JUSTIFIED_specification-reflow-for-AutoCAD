// 桌面端 A 阶段全流程自动化验证：逐专业走 新建→编辑→保存→关闭重开→预览→导出→解析器读回。
// 直接驱动共享模型与本机存储（与 Electron 主进程同一实现），DOCX 生成与读回由 .NET 工作进程完成。
import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { existsSync } from 'node:fs'
import { mkdtemp, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  buildDocument,
  createNote,
  customTemplateId,
  DISCIPLINES,
  emptyProject,
  findIssues,
  librarySections,
  parseNote,
  rebuildSections,
  resetSectionBody,
  SECTION_LIBRARY,
  templatesFor,
  toExportRequest,
} from '../desktop/app/src/shared/model.ts'
import { createStore } from '../desktop/app/src/shared/store.ts'

const root = resolve(fileURLToPath(new URL('..', import.meta.url)))
const dotnetCandidates = [
  process.env.DSS_DOTNET_EXE,
  join(root, 'artifacts', 'dotnet10', 'sdk', 'dotnet.exe'),
  resolve(root, '..', '..', 'artifacts', 'dotnet10', 'sdk', 'dotnet.exe'),
].filter(Boolean)
const dotnet = dotnetCandidates.find(candidate => existsSync(candidate)) ?? 'dotnet'
const worker = join(root, 'desktop', 'worker', 'bin', 'Debug', 'net10.0', 'DocxWorkbench.Worker.dll')

function call(request) {
  return new Promise((resolveResult, reject) => {
    const child = spawn(dotnet, [worker], { stdio: ['pipe', 'pipe', 'pipe'], windowsHide: true })
    let output = ''
    let error = ''
    child.stdout.setEncoding('utf8').on('data', chunk => { output += chunk })
    child.stderr.setEncoding('utf8').on('data', chunk => { error += chunk })
    child.on('error', reject)
    child.on('close', () => {
      try { resolveResult(JSON.parse(output)) }
      catch { reject(new Error(error || output || 'worker returned no JSON')) }
    })
    child.stdin.end(JSON.stringify(request))
  })
}

function deepEqualNote(left, right, label) {
  assert.equal(left.id, right.id, `${label} id`)
  assert.equal(left.title, right.title, `${label} title`)
  assert.equal(left.discipline, right.discipline, `${label} discipline`)
  assert.deepEqual(left.project, right.project, `${label} project`)
  assert.deepEqual(left.structural, right.structural, `${label} structural`)
  assert.equal(left.templateId, right.templateId, `${label} templateId`)
  assert.equal(left.sections.length, right.sections.length, `${label} sections length`)
  for (const [index, section] of left.sections.entries()) {
    assert.deepEqual(section, right.sections[index], `${label} section ${index}`)
  }
}

const directory = await mkdtemp(join(tmpdir(), 'dss-flow-'))
try {
  const store = createStore(join(directory, 'data'))
  let notesChecked = 0

  for (const [index, discipline] of DISCIPLINES.entries()) {
    const label = discipline.label
    const template = templatesFor(discipline.code).find(item => !item.custom)
    assert.ok(template, `${label} 缺少预设模板`)

    // 新建：选择专业与模板，填写项目资料
    const note = createNote(discipline.code, template.id, { ...emptyProject(), name: `${label}测试工程`, number: `NO-${index}`, owner: '建设单位', location: '测试地点' }, `${label}设计说明`)
    assert.equal(note.sections.length, template.sectionIds.length, `${label} 章节数量`)
    assert.ok(note.sections.every(section => section.body === ''), `${label} 未核定的专业正文不得预填`)
    assert.equal(note.structural === null, discipline.code !== 'structural', `${label} 结构参数显隐`)

    // 编辑：逐章填写纯文本正文（含换行）；结构专业补齐必填参数
    const sections = note.sections.map((section, order) => ({ ...section, body: `${label}第${order}章正文第一行。\n第二行。` }))
    const structural = note.structural
      ? { ...note.structural, siteCategory: 'II', seismicGrade: '乙', safetyLevel: '二级', foundationGrade: '乙级', protectionScheme: '中', seismicIntensity: '7度（0.15g）', corrosion: '微腐蚀性。' }
      : null
    const edited = { ...note, sections, structural }
    const saved = store.saveNote(edited)
    assert.equal(saved.ok, true, `${label} 保存失败: ${JSON.stringify(saved)}`)

    // 关闭重开：从存储列表恢复
    const summaries = store.listNotes()
    assert.ok(summaries.some(item => item.id === note.id), `${label} 最近工作列表`)
    const reopened = store.loadNote(note.id)
    deepEqualNote(edited, reopened, label)

    // 预览：连续预览块与结构
    const document = buildDocument(reopened)
    assert.equal(document.blocks[0].kind, 'title', `${label} 预览首块`)
    assert.equal(document.blocks[0].text, `${label}设计说明`, `${label} 预览标题`)
    assert.ok(document.blocks.some(block => block.kind === 'meta' && block.text === '工程名称：' + `${label}测试工程`), `${label} 预览项目资料`)
    const headingBlocks = document.blocks.filter(block => block.kind === 'heading')
    const expectedHeadings = document.blocks.filter(block => block.kind === 'title' || block.kind === 'heading').map(block => block.text)
    assert.deepEqual(headingBlocks.map(block => block.text).filter(text => text !== '结构设计参数'), sections.map(section => section.title), `${label} 预览章节顺序`)
    if (discipline.code === 'structural') {
      assert.ok(headingBlocks.some(block => block.text === '结构设计参数'), `${label} 结构参数标题`)
      assert.ok(document.blocks.some(block => block.text.includes('设防烈度：7度（0.15g）')), `${label} 烈度人工选择`)
    } else {
      assert.ok(!headingBlocks.some(block => block.text === '结构设计参数'), `${label} 不得出现结构参数`)
      assert.equal(toExportRequest(reopened, 'x.docx').document.structural, null, `${label} 导出请求无结构参数`)
    }

    // 导出：生成 DOCX 并用现有解析器读回
    const path = join(directory, `${label}设计说明.docx`)
    const exported = await call(toExportRequest(reopened, path))
    assert.equal(exported.success, true, `${label} 导出失败: ${JSON.stringify(exported)}`)
    assert.equal(exported.blocks, document.blocks.length, `${label} 读回块数`)
    assert.deepEqual(exported.headings, expectedHeadings, `${label} 读回标题顺序`)
    const readback = await call({ operation: 'inspect', path })
    assert.equal(readback.success, true, `${label} 解析器读回失败`)
    assert.equal(readback.blocks, exported.blocks, `${label} 读回一致`)
    notesChecked += 1
  }

  // 自定义组合：结构专业自定义章节、排序、移除，且不写回标准库
  const structuralNote = createNote('structural', customTemplateId('structural'), { ...emptyProject(), name: '自定义工程' }, '自定义组合说明')
  assert.equal(structuralNote.sections.length, 0, '自定义组合初始无章节')
  const libraryBefore = SECTION_LIBRARY.length
  const added = librarySections('structural').slice(0, 2).map(section => ({ id: section.id, title: section.title, body: `已编辑：${section.title}。`, custom: false }))
  const custom = {
    ...structuralNote,
    structural: { ...structuralNote.structural, siteCategory: 'II', seismicGrade: '乙', safetyLevel: '二级', foundationGrade: '乙级', protectionScheme: '中', seismicIntensity: '7度（0.15g）' },
    sections: [...added, { id: 'custom-test', title: '深基坑支护', body: '基坑等级与支护形式。', custom: true }],
  }
  assert.equal(store.saveNote(custom).ok, true, '自定义组合保存')
  const customReopened = store.loadNote(custom.id)
  assert.equal(customReopened.sections.length, 3, '自定义章节持久化')
  assert.equal(customReopened.sections[2].custom, true, '自定义章节标记')
  assert.equal(SECTION_LIBRARY.length, libraryBefore, '标准库不得被写回')
  assert.equal(librarySections('structural').some(section => section.id === 'custom-test'), false, '自定义章节不得进入标准库')

  // 重新排序：交换前两章后保存并重开，顺序应保持
  const reordered = { ...customReopened, sections: [customReopened.sections[1], customReopened.sections[0], customReopened.sections[2]] }
  assert.equal(store.saveNote(reordered).ok, true, '排序后保存')
  const reorderedReopened = store.loadNote(custom.id)
  assert.deepEqual(reorderedReopened.sections.map(section => section.id), [added[1].id, added[0].id, 'custom-test'], '排序持久化')

  // 自定义组合导出
  const customDocument = buildDocument(reorderedReopened)
  const customPath = join(directory, '自定义组合说明.docx')
  const customExported = await call(toExportRequest(reorderedReopened, customPath))
  assert.equal(customExported.success, true, `自定义组合导出失败: ${JSON.stringify(customExported)}`)
  assert.deepEqual(customExported.headings.filter(text => text !== '结构设计参数'), [reorderedReopened.title, ...reorderedReopened.sections.map(section => section.title)], '自定义组合章节顺序')

  // 清空正文；旧草稿中的未改动提示文案按空章处理，不进入 DOCX
  const resetBody = resetSectionBody(reorderedReopened.sections[0])
  assert.equal(resetBody, '', '清空后不应恢复未核定的示例正文')
  const legacyDefinition = SECTION_LIBRARY.find(section => section.id === reorderedReopened.sections[0].id)
  const legacy = { ...reorderedReopened, sections: [{ ...reorderedReopened.sections[0], body: legacyDefinition.body }] }
  assert.ok(findIssues(legacy).some(issue => issue.level === 'error' && issue.message.includes('所有章节都是空的')), '旧版提示文案不算已填写正文')
  assert.ok(!buildDocument(legacy).blocks.some(block => block.text === legacyDefinition.body), '旧版提示文案不得进入预览或导出')
  assert.equal(parseNote(legacy).sections[0].body, '', '旧版草稿读取后应在编辑器中显示空正文')

  // 空章节告警不阻断，但缺必要参数/空正文必须报错
  const emptyBodies = { ...reorderedReopened, sections: reorderedReopened.sections.map(section => ({ ...section, body: '' })) }
  const emptyIssues = findIssues(emptyBodies)
  assert.ok(emptyIssues.some(issue => issue.level === 'error' && issue.message.includes('所有章节都是空的')), '空正文必须报错')
  const partialEmpty = { ...reorderedReopened, sections: [{ ...reorderedReopened.sections[0], body: '' }, reorderedReopened.sections[1], reorderedReopened.sections[2]] }
  assert.ok(findIssues(partialEmpty).some(issue => issue.level === 'warning'), '空章节应告警')

  // 保存失败必须返回失败状态，不能显示已保存
  const blockedRoot = join(directory, 'blocker')
  await writeFile(blockedRoot, 'not a directory', 'utf8')
  const blockedStore = createStore(join(blockedRoot, 'notes'))
  const blockedResult = blockedStore.saveNote(custom)
  assert.equal(blockedResult.ok, false, '存储不可用必须报告失败')
  assert.ok(blockedResult.error.length > 0, '失败必须有原因')

  // 删除：从最近工作移除
  store.deleteNote(custom.id)
  assert.equal(store.listNotes().some(item => item.id === custom.id), false, '删除后不再列出')

  // 重建模板：rebuildSections 按模板重置副本
  const rebuilt = rebuildSections(reorderedReopened, templatesFor('structural')[0].id)
  assert.deepEqual(rebuilt.map(section => section.id), templatesFor('structural')[0].sectionIds, '重建章节按模板')

  console.log(`DESKTOP_FLOW_OK disciplines=${DISCIPLINES.length} notes=${notesChecked} custom=1 reordered=1 resetDefault=1 saveFailure=1 deleted=1`)
} finally {
  await rm(directory, { recursive: true, force: true })
}
