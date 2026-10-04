// 独立隐藏窗口，真实 preload/IPC/本地存储/worker；只替换文件选择对话框。
const { app, BrowserWindow, ipcMain, dialog } = require('electron')
const assert = require('node:assert/strict')
const { join, resolve } = require('node:path')
const { mkdtempSync, mkdirSync, writeFileSync, existsSync } = require('node:fs')
const { spawn } = require('node:child_process')
const base = process.env.DSS_TEST_ROOT
assert.ok(base && !/^c:/i.test(base))
const output = mkdtempSync(join(base,'bid-ui-'))
app.setPath('userData',join(output,'user'))
const root=resolve(__dirname,'..'), pause=ms=>new Promise(r=>setTimeout(r,ms))
app.whenReady().then(async()=>{
  const {registerBidIpc}=await import('../desktop/app/dist-electron/electron/bid-ipc.js')
  const {createBidStore}=await import('../desktop/app/dist-electron/src/features/bidding/store.js')
  const store=createBidStore(join(output,'data'))
  let importPath=join(output,'招标要求.txt'), exportPath=join(output,'投标草稿.docx'), cancel=false, failSave=false, delaySave=0
  writeFileSync(importPath,'测试资格要求：应提供项目负责人证明。')
  dialog.showOpenDialog=async()=>cancel?{canceled:true,filePaths:[]}:{canceled:false,filePaths:[importPath]}
  dialog.showSaveDialog=async()=>cancel?{canceled:true}:{canceled:false,filePath:exportPath}
  const worker=request=>new Promise((res,rej)=>{
    const child=spawn(process.env.DSS_DOTNET_EXE,[join(root,'desktop/worker/bin/Debug/net10.0/DocxWorkbench.Worker.dll')],{windowsHide:true,stdio:['pipe','pipe','pipe']})
    let text='',err='';child.stdout.on('data',s=>text+=s);child.stderr.on('data',s=>err+=s);child.on('error',rej);child.on('close',()=>{try{res(JSON.parse(text))}catch{rej(new Error(err||text))}});child.stdin.end(JSON.stringify(request))
  })
  registerBidIpc(join(output,'data'),worker,()=>{})
  ipcMain.removeHandler('bid-save')
  ipcMain.handle('bid-save',async(_e,bid)=>{if(delaySave)await pause(delaySave);return failSave?{ok:false,error:'测试模拟磁盘写入失败'}:store.save(bid)})
  ipcMain.handle('notes-list',()=>[]);ipcMain.handle('projects-list',()=>[]);ipcMain.handle('catalog-load',()=>null)
  ipcMain.handle('note-save',()=>({ok:true,savedAt:new Date().toISOString()}))
  const win=new BrowserWindow({width:1280,height:820,show:false,webPreferences:{preload:join(root,'desktop/app/electron/preload.cjs'),contextIsolation:true,nodeIntegration:false,sandbox:true}})
  const js=s=>win.webContents.executeJavaScript(s)
  const click=async text=>{await js(`(()=>{const b=[...document.querySelectorAll('button')].find(b=>b.textContent.trim()===${JSON.stringify(text)});if(!b||b.disabled)throw Error('按钮不可用: '+${JSON.stringify(text)});b.click()})()`);await pause(130)}
  const field=async(label,value)=>{await js(`(()=>{const e=[...document.querySelectorAll('label')].find(e=>e.querySelector('span')?.textContent===${JSON.stringify(label)})?.querySelector('input,textarea');if(!e)throw Error('找不到字段');Object.getOwnPropertyDescriptor(e.tagName==='TEXTAREA'?HTMLTextAreaElement.prototype:HTMLInputElement.prototype,'value').set.call(e,${JSON.stringify(value)});e.dispatchEvent(new Event('input',{bubbles:true}))})()`);await pause(70)}
  const nav=async i=>{await js(`document.querySelectorAll('.bid-sidebar nav button')[${i}].click()`);await pause(100)}
  const waitIdle=async()=>{for(let i=0;i<100;i++){if(!await js(`Boolean(document.querySelector('.bid-busy'))`))return;await pause(100)}throw Error('处理超时')}
  const checks=[]
  try{
    await win.loadFile(join(root,'desktop/app/dist/index.html'));await pause(300)
    await js(`[...document.querySelectorAll('.entry-card')].find(b=>b.textContent.includes('勘察设计投标')).click()`);await pause(250)
    assert.equal(await js(`document.querySelectorAll('.bid-type-card').length`),3)
    writeFileSync(join(output,'dashboard.png'),(await win.webContents.capturePage()).toPNG())
    await js(`document.querySelectorAll('.bid-type-card')[1].click()`);await pause(200)
    await field('项目名称','脱敏投标UI测试');await click('保存');await pause(100)
    let bid=store.list()[0];assert.equal(store.load(bid.id).bid.project.name,'脱敏投标UI测试')
    await nav(1);await click('导入本地资料');await waitIdle();await click('保存');await pause(150)
    assert.equal(store.load(bid.id).bid.sources.length,1)
    await click('从此处建立要求');await field('要求标题','负责人资格核对');await click('保存');await pause(100)
    assert.equal(store.load(bid.id).bid.requirements[0].excerpt,'测试资格要求：应提供项目负责人证明。')
    await nav(4)
    await js(`(()=>{const e=document.querySelector('.bid-body-editor');Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype,'value').set.call(e,'首版正文');e.dispatchEvent(new Event('input',{bubbles:true}))})()`);await pause(50)
    await click('添加简单表格');await js(`(()=>{const e=document.querySelector('.bid-table-editor textarea');Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype,'value').set.call(e,'成果名称');e.dispatchEvent(new Event('input',{bubbles:true}))})()`);await pause(80)
    await click('保存');await pause(100)
    assert.equal(store.load(bid.id).bid.sections[0].tables[0].rows[0][0],'成果名称')
    checks.push('入口/新建/导入/摘录要求/正文表格实际落盘')
    for(const [w,h] of [[1280,820],[900,680]]){win.setSize(w,h);await pause(100);assert.ok(await js(`document.documentElement.scrollWidth<=innerWidth+1`),'不应横向溢出');assert.ok(await js(`(()=>{const e=document.querySelector('.bid-content');e.scrollTop=e.scrollHeight;return e.scrollTop>0 && document.querySelector('.bid-editor .bid-check').getBoundingClientRect().bottom<=innerHeight})()`),'长页面末尾复核按钮必须可达');writeFileSync(join(output,`editor-${w}.png`),(await win.webContents.capturePage()).toPNG())}
    checks.push('两尺寸布局与末尾可达')
    failSave=true
    await js(`(()=>{const e=document.querySelector('.bid-body-editor');Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype,'value').set.call(e,'写入失败仍保留输入');e.dispatchEvent(new Event('input',{bubbles:true}))})()`);await pause(60);await click('投标项目');await waitIdle()
    assert.equal(await js(`Boolean(document.querySelector('.bid-workspace'))`),true);assert.ok((await js(`document.querySelector('[role=alert]').textContent`)).includes('写入失败'))
    failSave=false;delaySave=350;await click('保存')
    await js(`(()=>{const e=document.querySelector('.bid-body-editor');Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype,'value').set.call(e,'保存期间最后一次输入');e.dispatchEvent(new Event('input',{bubbles:true}))})()`);await pause(80);await click('投标项目');await waitIdle();delaySave=0
    assert.equal(store.load(bid.id).bid.sections[0].body,'保存期间最后一次输入')
    await click('继续编制');await waitIdle();await nav(5)
    assert.equal(await js(`[...document.querySelectorAll('button')].find(b=>b.textContent==='导出已核对稿').disabled`),true)
    cancel=true;await click('导出 Word 草稿');await waitIdle();assert.equal(existsSync(exportPath),false)
    cancel=false;await click('导出 Word 草稿');await waitIdle();assert.ok(existsSync(exportPath));assert.equal(store.load(bid.id).bid.exports.length,1)
    checks.push('写入失败不离开/保存期间输入/缺项拦截/取消与真实DOCX导出')
    exportPath=join(output,'投标备份.json');await click('备份本标及附件');await waitIdle();assert.ok(existsSync(exportPath))
    await click('投标项目');await waitIdle();importPath=exportPath;await click('恢复备份');await waitIdle();assert.equal(store.list().length,2)
    await click('投标项目');await waitIdle()
    for(const index of [0,2]){
      await js(`document.querySelectorAll('.bid-type-card')[${index}].click()`);await waitIdle();await pause(100)
      await field('项目名称',index===0?'勘察脱敏测试':'联合脱敏测试');await nav(5)
      exportPath=join(output,`类型${index}草稿.docx`);await click('导出 Word 草稿');await waitIdle();assert.ok(existsSync(exportPath))
      await click('投标项目');await waitIdle()
    }
    checks.push('勘察/设计/联合三类型实际草稿导出')
    await click('工作台首页');await waitIdle();assert.ok(await js(`[...document.querySelectorAll('.entry-card')].some(e=>e.textContent.includes('设计说明'))`))
    checks.push('备份恢复新增副本/回到原工作台')
    const report={status:'BIDDING_UI_OK',checks,output};writeFileSync(join(output,'result.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report))
  }finally{win.destroy();app.quit()}
}).catch(e=>{console.error(e);app.exit(1)})
