// 隐藏的独立 Electron 测试窗口，不读取或驱动用户正在使用的窗口。
const { app, BrowserWindow, ipcMain } = require('electron')
const assert = require('node:assert/strict')
const { join, resolve } = require('node:path')
const root = resolve(__dirname, '..')
app.setPath('userData', join(app.getPath('temp'), `dss-scroll-${process.pid}`))
const pause = ms => new Promise(resolve => setTimeout(resolve, ms))

app.whenReady().then(async () => {
  const { createNote, summarizeNote } = await import('../desktop/app/dist-electron/src/shared/model.js')
  let note = createNote('structural', 'tpl-struct-frame', { name: '脱敏滚动测试', number: '', owner: '', location: '' })
  const definitions = { foundation_type: { label: '基础形式', unit: '' }, geotechnical_survey_no: { label: '勘察报告工程编号', unit: '' } }
  note.fieldDefinitions = definitions
  note.fieldValues = { foundation_type: '筏板基础', geotechnical_survey_no: 'TEST-01' }
  note.sections = Array.from({ length: 7 }, (_, chapter) => {
    const modules = Array.from({ length: 35 }, (_, index) => ({ id: `m-${chapter}-${index}`, clauseId: `测试条款${index}`, packageId: 'test', template: `第${index}段，用于核对长章节滚动。采用{foundation_type}，报告{geotechnical_survey_no}。`, baseTemplate: '', edited: false, fieldIds: ['foundation_type', 'geotechnical_survey_no'], sourceRefs: [], refs: [], flags: [], reviewStatus: 'pending', confirmedForNote: false }))
    return { id: `chapter-${chapter}`, title: `第${chapter + 1}章测试`, body: '章节最后的补充正文', custom: false, modules, layoutBlocks: modules.map(module => ({ kind: 'paragraph', moduleId: module.id })) }
  })
  ipcMain.handle('notes-list', () => [summarizeNote(note)])
  ipcMain.handle('projects-list', () => [])
  ipcMain.handle('catalog-load', () => null)
  ipcMain.handle('note-load', () => note)
  ipcMain.handle('note-save', (_event, updated) => { note = updated; return { ok: true, savedAt: updated.updatedAt } })
  ipcMain.handle('project-save', () => [])
  const window = new BrowserWindow({ width: 1280, height: 820, show: false, webPreferences: { preload: join(root, 'desktop/app/electron/preload.cjs'), contextIsolation: true, nodeIntegration: false, sandbox: true } })
  try {
    await window.loadFile(join(root, 'desktop/app/dist/index.html'))
    await pause(250)
    await window.webContents.executeJavaScript(`document.querySelector('button[title="继续"]').click()`)
    await pause(200)
    const measurements = []
    for (const [width, height] of [[1280, 820], [900, 680]]) {
      window.setSize(width, height)
      await pause(100)
      for (const index of [1, 2, 6]) {
        await window.webContents.executeJavaScript(`document.querySelectorAll('.section-list li')[${index}].click()`)
        await pause(80)
        const measurement = await window.webContents.executeJavaScript(`(() => {
          const scroller = document.querySelector('.editor-scroll');
          const initially = scroller.scrollTop;
          scroller.scrollTop = scroller.scrollHeight;
          const foot = document.querySelector('.editor-foot').getBoundingClientRect();
          const box = scroller.getBoundingClientRect();
          return { initially, client: scroller.clientHeight, content: scroller.scrollHeight, bottom: scroller.scrollTop, lastVisible: foot.bottom <= box.bottom + 2, globalInputs: [...document.querySelectorAll('.content-field-grid input')].length };
        })()`)
        assert.equal(measurement.initially, 0, '切章应回到顶部')
        assert.ok(measurement.content > measurement.client && measurement.bottom > 0, '长章节必须能滚动')
        assert.equal(measurement.lastVisible, true, '必须能看到章节末尾')
        assert.equal(measurement.globalInputs, 0, '第二步不重复输入项目共用参数')
        measurements.push({ width, height, chapter: index + 1, ...measurement })
      }
    }
    // 使用章节中的明确入口，避免依赖导航的按钮布局。
    if (await window.webContents.executeJavaScript(`Boolean(document.querySelector('.editor-scroll'))`)) {
      await window.webContents.executeJavaScript(`[...document.querySelectorAll('button')].find(button => button.textContent.includes('查看 / 修改 01')).click()`)
    }
    await pause(100)
    const parameterCheck = await window.webContents.executeJavaScript(`(() => {
      const field = [...document.querySelectorAll('label.field')].find(label => label.textContent.includes('基础形式'));
      const input = field?.querySelector('input');
      if (!input) return false;
      const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set;
      setter.call(input, '独立基础'); input.dispatchEvent(new Event('input', { bubbles: true }));
      return true;
    })()`)
    assert.equal(parameterCheck, true, '第一步必须展示基础形式')
    await pause(1000)
    assert.equal(note.fieldValues.foundation_type, '独立基础', '第一步修改应自动保存')
    assert.equal(note.sections.length, 7, '修改共用参数不能丢失章节')
    await window.webContents.executeJavaScript(`[...document.querySelectorAll('button')].find(button => button.textContent.includes('返回章节编制')).click()`)
    await pause(150)
    assert.equal(await window.webContents.executeJavaScript(`Boolean(document.querySelector('.editor-scroll')) && document.querySelector('.content-module p').textContent.includes('独立基础')`), true, '返回章节应引用最新参数，不重建正文')
    console.log(JSON.stringify({ status: 'DESKTOP_SCROLL_OK', measurements, parameterSaved: true }))
  } finally { window.destroy(); app.quit() }
}).catch(error => { console.error(error); app.exit(1) })
