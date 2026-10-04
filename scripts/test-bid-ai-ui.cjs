const {app,BrowserWindow,ipcMain,safeStorage}=require('electron')
const assert=require('node:assert/strict')
const {join,resolve}=require('node:path')
const {mkdtempSync,writeFileSync,readFileSync}=require('node:fs')
const base=process.env.DSS_TEST_ROOT;assert.ok(base&&!/^c:/i.test(base))
const output=mkdtempSync(join(base,'ai-ui-')),root=resolve(__dirname,'..')
app.setPath('userData',join(output,'user'))
const pause=ms=>new Promise(r=>setTimeout(r,ms))
app.whenReady().then(async()=>{
  const {registerBidIpc}=await import('../desktop/app/dist-electron/electron/bid-ipc.js')
  const {createBidAiService}=await import('../desktop/app/dist-electron/electron/bid-ai.js')
  const {createBidStore}=await import('../desktop/app/dist-electron/src/features/bidding/store.js')
  const {newBid}=await import('../desktop/app/dist-electron/src/features/bidding/model.js')
  const data=join(output,'data'),store=createBidStore(data),file=join(output,'招标测试.txt');writeFileSync(file,'脱敏原件')
  const source=store.importAsset(file);source.blocks=[{location:'第1段',text:'项目负责人必须具有注册资格。'},{location:'第2段',text:'设计方案评分最高10分。'}]
  const bid=newBid('design');bid.sources=[source];bid.sections[0].body='人工编写，不可覆盖';assert.equal(store.save(bid).ok,true)
  let calls=0
  const ai=createBidAiService(data,{available:()=>safeStorage.isEncryptionAvailable(),encrypt:s=>safeStorage.encryptString(s),decrypt:b=>safeStorage.decryptString(b)},async()=>{
    calls++;return new Response(JSON.stringify({choices:[{finish_reason:'stop',message:{content:JSON.stringify({requirements:[{title:'设计方案评分',kind:'评分',reference:{sourceId:source.id,index:1,quote:source.blocks[1].text}}]})}}],usage:{prompt_tokens:12,completion_tokens:8,total_tokens:20}}))
  })
  registerBidIpc(data,async()=>{throw Error('本测试不调用worker')},()=>{},ai)
  ipcMain.handle('notes-list',()=>[]);ipcMain.handle('projects-list',()=>[]);ipcMain.handle('catalog-load',()=>null)
  const win=new BrowserWindow({width:1280,height:820,show:false,webPreferences:{preload:join(root,'desktop/app/electron/preload.cjs'),sandbox:true,contextIsolation:true,nodeIntegration:false}})
  const js=s=>win.webContents.executeJavaScript(s)
  const click=async text=>{await js(`(()=>{const b=[...document.querySelectorAll('button')].find(b=>b.textContent.trim()===${JSON.stringify(text)});if(!b||b.disabled)throw Error('按钮不可用 '+${JSON.stringify(text)});b.click()})()`);await pause(150)}
  const input=async(label,value)=>{await js(`(()=>{const e=[...document.querySelectorAll('label')].find(e=>e.querySelector('span')?.textContent===${JSON.stringify(label)}).querySelector('input');Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(e,${JSON.stringify(value)});e.dispatchEvent(new Event('input',{bubbles:true}))})()`);await pause(60)}
  try{
    await win.loadFile(join(root,'desktop/app/dist/index.html'));await pause(200);await js(`[...document.querySelectorAll('.entry-card')].find(b=>b.textContent.includes('勘察设计投标')).click()`);await pause(200);await click('继续编制')
    await js(`document.querySelectorAll('.bid-sidebar nav button')[1].click()`);await pause(80);await click('本地提取要求候选')
    assert.equal(await js(`document.querySelectorAll('.bid-candidates .bid-candidate').length`),2)
    await js(`document.querySelector('.bid-candidates input[type=checkbox]').click()`);await pause(60);await click('加入所选要求（1）');await click('保存');assert.equal(store.load(bid.id).bid.requirements.length,1)
    await click('AI辅助与设置');await input('API密钥','TEST-ONLY-NOT-A-REAL-KEY');await click('保存AI设置')
    assert.ok(!readFileSync(join(data,'bid-ai/settings.json'),'utf8').includes('TEST-ONLY'))
    await js(`document.querySelector('.bid-ai-panel .bid-check input').click()`);await pause(60);await click('预览本次发送内容');assert.equal(calls,0)
    assert.ok((await js(`document.querySelector('[aria-label="本次发送内容"]').value`)).includes(source.blocks[1].text));await click('取消发送');assert.equal(calls,0)
    await click('预览本次发送内容');await click('确认发送并生成');await pause(250);assert.equal(calls,1);assert.equal(store.load(bid.id).bid.requirements.length,1)
    await js(`document.querySelector('.bid-ai-job').open=true;document.querySelector('.bid-ai-job input[type=checkbox]').click()`);await pause(80);await click('加入所选候选');await pause(100)
    const saved=store.load(bid.id).bid;assert.equal(saved.requirements.length,2);assert.ok(saved.requirements.every(r=>!r.confirmed));assert.equal(saved.sections[0].body,'人工编写，不可覆盖')
    for(const [w,h] of [[1280,820],[900,680]]){win.setSize(w,h);await pause(80);assert.ok(await js(`document.documentElement.scrollWidth<=innerWidth+1`));await js(`document.querySelector('.bid-content').scrollTop=0`);writeFileSync(join(output,`ai-${w}.png`),(await win.webContents.capturePage()).toPNG())}
    const report={status:'BID_AI_UI_OK',calls,realNetworkRequests:0,encryption:'Windows safeStorage',output};writeFileSync(join(output,'result.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report))
  }finally{win.destroy();app.quit()}
}).catch(e=>{console.error(e);app.exit(1)})
