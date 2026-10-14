// 隐藏窗口，真实界面/preload/IPC/safeStorage；只替换DeepSeek HTTP，不发送真实资料。
const {app,BrowserWindow,ipcMain,safeStorage}=require('electron')
const assert=require('node:assert/strict')
const {join,resolve}=require('node:path')
const {pathToFileURL}=require('node:url')
const {mkdtempSync,writeFileSync}=require('node:fs')
const base=process.env.DSS_TEST_ROOT;assert.ok(base&&!/^c:/i.test(base))
const output=mkdtempSync(join(base,'candidate-ui-')),root=resolve(__dirname,'..')
const runtime=process.env.DSS_RUNTIME_APP||join(root,'desktop/app')
app.setPath('userData',join(output,'user'))
const pause=ms=>new Promise(r=>setTimeout(r,ms))
app.whenReady().then(async()=>{
  const load=path=>import(pathToFileURL(join(runtime,'dist-electron',path)).href)
  const {registerBidIpc}=await load('electron/bid-ipc.js')
  const {createBidAiService}=await load('electron/bid-ai.js')
  const {createBidStore}=await load('src/features/bidding/store.js')
  const {newBid}=await load('src/features/bidding/model.js')
  const data=join(output,'data'),store=createBidStore(data),file=join(output,'脱敏招标资料.txt')
  writeFileSync(file,'原件测试，无真实资料')
  const source=store.importAsset(file);source.version='初稿'
  source.blocks=Array.from({length:31},(_,i)=>({location:`文本行 ${i+1} / 文字块 1`,text:i===25?'负责人须提供有效注册证书；不接受过期证明。':`脱敏普通说明第${i+1}行。`}))
  const bid=newBid('design');bid.sources=[source];bid.sections[0].body='人工正文不可覆盖';assert.equal(store.save(bid).ok,true)
  let calls=0
  const ai=createBidAiService(data,{available:()=>safeStorage.isEncryptionAvailable(),encrypt:s=>safeStorage.encryptString(s),decrypt:b=>safeStorage.decryptString(b)},async()=>{
    calls++;return new Response(JSON.stringify({choices:[{finish_reason:'stop',message:{content:JSON.stringify({requirements:[{title:'负责人证书要求',kind:'资格',reference:{sourceId:source.id,index:25,quote:source.blocks[25].text}}]})}}]}))
  })
  registerBidIpc(data,async()=>{throw Error('本测试不调用worker')},()=>{},ai)
  ipcMain.handle('notes-list',()=>[]);ipcMain.handle('projects-list',()=>[]);ipcMain.handle('catalog-load',()=>null)
  const win=new BrowserWindow({width:1280,height:820,show:false,webPreferences:{preload:join(runtime,'electron/preload.cjs'),sandbox:true,contextIsolation:true,nodeIntegration:false,backgroundThrottling:false}})
  const js=s=>win.webContents.executeJavaScript(s)
  const click=async text=>{await js(`(()=>{const b=[...document.querySelectorAll('button')].find(b=>b.textContent.trim()===${JSON.stringify(text)});if(!b||b.disabled)throw Error('按钮不可用 '+${JSON.stringify(text)});b.click()})()`);await pause(120)}
  const input=async(label,value)=>{await js(`(()=>{const e=[...document.querySelectorAll('label')].find(e=>e.querySelector('span')?.textContent===${JSON.stringify(label)}).querySelector('input');Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(e,${JSON.stringify(value)});e.dispatchEvent(new Event('input',{bubbles:true}))})()`);await pause(60)}
  const idle=async()=>{for(let i=0;i<100;i++){if(!await js(`!!document.querySelector('.bid-busy')`))return;await pause(100)}throw Error('等待UI超时')}
  const waitResult=async()=>{for(let i=0;i<100;i++){if(await js(`!!document.querySelector('.bid-ai-job .bid-candidate')`))return;await pause(100)}throw Error('任务结果未显示')}
  const checks=[]
  try{
    await win.loadFile(join(runtime,'dist/index.html'));await pause(180)
    await js(`[...document.querySelectorAll('.entry-card')].find(b=>b.textContent.includes('勘察设计投标')).click()`);await pause(180)
    await click('继续编制');await click('AI辅助与设置');await input('API密钥','TEST-ONLY-NOT-A-REAL-KEY');await click('保存AI设置');await idle()
    await js(`document.querySelector('.bid-ai-panel .bid-check input').click()`);await pause(60)
    for(let i=0;i<12;i++){await click('预览本次发送内容');await click('取消发送');await idle()}
    assert.equal(calls,0);checks.push('连续12次预览取消0请求、仍可准备')
    await click('预览本次发送内容');await click('确认发送并生成');await idle();await waitResult()
    assert.equal(calls,1);await js(`document.querySelector('.bid-ai-job').open=true`)
    assert.ok(await js(`document.querySelector('.bid-ai-reference').textContent.includes('初稿')`))
    await click('查看引用原文');await idle()
    assert.ok(await js(`document.querySelector('.bid-source-focused').textContent.includes('不接受过期证明')`))
    assert.equal(await js(`document.querySelector('.bid-source-blocks select').value`),'1')
    assert.equal(calls,1);checks.push('引用定位到第二组第26行、不重发AI')
    await input('资料版本/日期标记','补遗核对版');await click('保存');await idle();await click('AI辅助与设置');await pause(200)
    await js(`document.querySelector('.bid-ai-job').open=true`)
    assert.ok(await js(`document.querySelector('.bid-ai-job').textContent.includes('资料版本、类型或相关补遗已变化')`))
    assert.ok(await js(`[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==='加入所选候选').disabled`))
    assert.ok(await js(`document.querySelector('.bid-ai-job input[type=checkbox]').disabled`))
    assert.equal(store.load(bid.id).bid.requirements.length,0);checks.push('版本变化候选不可勾选/采纳、旧结果保留')
    for(const [width,height] of [[1280,820],[900,680]]){
      win.setSize(width,height);await js(`document.querySelector('.bid-ai-job').scrollIntoView({block:'start'});new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)))`);await pause(150);assert.ok(await js(`document.documentElement.scrollWidth<=innerWidth+1`))
      writeFileSync(join(output,`review-${width}.png`),(await win.webContents.capturePage()).toPNG())
    }
    await click('按此任务重新准备');await click('预览本次发送内容');await click('确认发送并生成');await idle()
    for(let i=0;i<100;i++){if(ai.list(bid.id).filter(j=>j.state==='succeeded').length===2)break;await pause(100)}
    await pause(1600);assert.equal(calls,2)
    await js(`const current=[...document.querySelectorAll('.bid-ai-job')].find(e=>e.textContent.includes('版本：补遗核对版'));current.open=true;current.querySelector('input[type=checkbox]').click()`);await pause(60)
    await click('加入所选候选');await idle()
    const saved=store.load(bid.id).bid;assert.equal(saved.requirements.length,1);assert.equal(saved.requirements[0].confirmed,false)
    assert.equal(saved.sections[0].body,'人工正文不可覆盖');checks.push('重新预览生成后可采纳、保留人工正文')
    const report={status:'BID_CANDIDATE_UI_OK',checks,calls,realNetworkRequests:0,encryption:'Windows safeStorage',runtime,output}
    writeFileSync(join(output,'result.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report))
  }finally{win.destroy();app.quit()}
}).catch(error=>{console.error(error);app.exit(1)})
