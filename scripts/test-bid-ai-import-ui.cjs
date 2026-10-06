// 复现1006截图中的空资料列表；真实dialog入口/IPC/TXT解析/保存，HTTP仅模拟。
const {app,BrowserWindow,ipcMain,dialog,safeStorage}=require('electron')
const assert=require('node:assert/strict')
const {join,resolve}=require('node:path')
const {mkdtempSync,writeFileSync}=require('node:fs')
const base=process.env.DSS_TEST_ROOT;assert.ok(base&&!/^c:/i.test(base))
const output=mkdtempSync(join(base,'ai-import-')),root=resolve(__dirname,'..')
app.setPath('userData',join(output,'user'))
const pause=ms=>new Promise(r=>setTimeout(r,ms))
app.whenReady().then(async()=>{
  const {registerBidIpc}=await import('../desktop/app/dist-electron/electron/bid-ipc.js')
  const {createBidAiService}=await import('../desktop/app/dist-electron/electron/bid-ai.js')
  const {createBidStore}=await import('../desktop/app/dist-electron/src/features/bidding/store.js')
  const {newBid}=await import('../desktop/app/dist-electron/src/features/bidding/model.js')
  const data=join(output,'data'),store=createBidStore(data),bid=newBid('design');bid.sections[0].body='人工正文保留';assert.equal(store.save(bid).ok,true)
  const file=join(output,'脱敏招标资料.txt');writeFileSync(file,'负责人须提供注册证书，不接受过期证明。')
  const image=join(output,'扫描页.png');writeFileSync(image,'仅用于未OCR入口测试，不作为真实图片')
  let selected=null,calls=0,dialogCalls=0
  dialog.showOpenDialog=async()=>{dialogCalls++;return {canceled:!selected,filePaths:selected?[selected]:[]}}
  const ai=createBidAiService(data,{available:()=>safeStorage.isEncryptionAvailable(),encrypt:s=>safeStorage.encryptString(s),decrypt:b=>safeStorage.decryptString(b)},async()=>{calls++;throw Error('本测试不应发送HTTP')})
  ai.configure('deepseek-flash','TEST-ONLY-NOT-A-REAL-KEY')
  registerBidIpc(data,async()=>{throw Error('本测试不调用worker')},()=>{},ai)
  ipcMain.handle('notes-list',()=>[]);ipcMain.handle('projects-list',()=>[]);ipcMain.handle('catalog-load',()=>null)
  const win=new BrowserWindow({width:1280,height:820,show:false,webPreferences:{preload:join(root,'desktop/app/electron/preload.cjs'),sandbox:true,contextIsolation:true,nodeIntegration:false,backgroundThrottling:false}})
  const js=s=>win.webContents.executeJavaScript(s)
  const click=async text=>{await js(`(()=>{const b=[...document.querySelectorAll('button')].find(b=>b.textContent.trim()===${JSON.stringify(text)});if(!b||b.disabled)throw Error('按钮不可用 '+${JSON.stringify(text)});b.click()})()`);await pause(150)}
  const idle=async()=>{for(let i=0;i<100;i++){if(!await js(`!!document.querySelector('.bid-busy')`))return;await pause(100)}throw Error('UI未结束')}
  const checks=[]
  try{
    await win.loadFile(join(root,'desktop/app/dist/index.html'));await pause(160);await js(`[...document.querySelectorAll('.entry-card')].find(b=>b.textContent.includes('勘察设计投标')).click()`);await pause(180)
    await click('继续编制');await click('AI辅助与设置')
    assert.ok(await js(`document.querySelector('.bid-ai-panel').textContent.includes('还没有招标资料')`));await click('选择并导入招标文件');await idle()
    assert.equal(dialogCalls,1);assert.equal(store.load(bid.id).bid.sources.length,0);assert.equal(calls,0);checks.push('空资料有选择入口、取消不变更也不发送')
    selected=file;await click('选择并导入招标文件');await idle()
    const saved=store.load(bid.id).bid;assert.equal(saved.sources.length,1);assert.ok(saved.sources[0].blocks[0].text.includes('不接受过期证明'))
    assert.ok(await js(`document.querySelector('.bid-ai-panel .bid-check input').checked`));assert.equal(calls,0)
    await click('预览本次发送内容');assert.ok(await js(`document.querySelector('[aria-label="本次发送内容"]').value.includes('不接受过期证明')`));await click('取消发送');await idle();checks.push('直接导入TXT提取保存、自动选中、预览仍0请求')
    selected=image;await click('选择并导入招标文件');await idle()
    assert.equal(store.load(bid.id).bid.sources.length,2);assert.ok(await js(`document.querySelector('.bid-ai-panel').textContent.includes('尚未执行OCR')`))
    assert.ok(await js(`[...document.querySelectorAll('.bid-ai-panel .bid-check input')].at(-1).disabled`));assert.equal(calls,0);checks.push('图片原件保留、未OCR原因明确、不能发送空文字')
    assert.equal(store.load(bid.id).bid.sections[0].body,'人工正文保留')
    writeFileSync(join(output,'result.json'),JSON.stringify({status:'BID_AI_IMPORT_UI_OK',checks,calls,dialogCalls,output},null,2));console.log(JSON.stringify({status:'BID_AI_IMPORT_UI_OK',checks,calls,dialogCalls,output}))
  }finally{win.destroy();app.quit()}
}).catch(error=>{console.error(error);app.exit(1)})
