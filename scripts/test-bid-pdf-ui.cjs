// 独立隐藏窗口验证真实PDF导入/IPC/界面/worker，只替换选择对话框和DeepSeek HTTP。
const {app,BrowserWindow,ipcMain,dialog,safeStorage}=require('electron')
const assert=require('node:assert/strict')
const {join,resolve}=require('node:path')
const {pathToFileURL}=require('node:url')
const {mkdtempSync,mkdirSync,writeFileSync}=require('node:fs')
const {spawn}=require('node:child_process')
const base=process.env.DSS_TEST_ROOT,fixtures=process.env.DSS_PDF_FIXTURES
assert.ok(base&&!/^c:/i.test(base)&&fixtures)
const output=mkdtempSync(join(base,'pdf-ui-')),root=resolve(__dirname,'..')
const runtime=process.env.DSS_RUNTIME_APP||join(root,'desktop/app')
app.setPath('userData',join(output,'user'))
const pause=ms=>new Promise(r=>setTimeout(r,ms))
app.whenReady().then(async()=>{
  const load=path=>import(pathToFileURL(join(runtime,'dist-electron',path)).href)
  const {registerBidIpc}=await load('electron/bid-ipc.js')
  const {createBidAiService}=await load('electron/bid-ai.js')
  const {createBidStore}=await load('src/features/bidding/store.js')
  const data=join(output,'data'),store=createBidStore(data)
  let importPath=join(fixtures,'text.pdf'),calls=0,sent
  dialog.showOpenDialog=async()=>({canceled:false,filePaths:[importPath]})
  const ai=createBidAiService(data,{available:()=>safeStorage.isEncryptionAvailable(),encrypt:s=>safeStorage.encryptString(s),decrypt:b=>safeStorage.decryptString(b)},async(_url,options)=>{
    calls++;sent=JSON.parse(JSON.parse(options.body).messages[1].content)
    const block=sent.sources[0].blocks[0]
    return new Response(JSON.stringify({choices:[{finish_reason:'stop',message:{content:JSON.stringify({requirements:[{title:'截止按补遗确认',kind:'提交事项',reference:{sourceId:sent.sources[0].id,index:block.index,quote:'最终截止时间应以补遗为准。'}}]})}}]}))
  })
  const worker=request=>new Promise((res,rej)=>{
    const command=process.env.DSS_WORKER_EXE||process.env.DSS_DOTNET_EXE
    const args=process.env.DSS_WORKER_EXE?[]:[join(root,'desktop/worker/bin/Debug/net10.0/DocxWorkbench.Worker.dll')]
    const child=spawn(command,args,{windowsHide:true,stdio:['pipe','pipe','pipe']})
    let text='',error='';child.stdout.on('data',s=>text+=s);child.stderr.on('data',s=>error+=s);child.on('error',rej);child.on('close',()=>{try{res(JSON.parse(text))}catch{rej(Error(error||text))}});child.stdin.end(JSON.stringify(request))
  })
  registerBidIpc(data,worker,()=>{},ai)
  ipcMain.handle('notes-list',()=>[]);ipcMain.handle('projects-list',()=>[]);ipcMain.handle('catalog-load',()=>null)
  const win=new BrowserWindow({width:1280,height:820,show:false,webPreferences:{preload:join(runtime,'electron/preload.cjs'),sandbox:true,contextIsolation:true,nodeIntegration:false}})
  const js=s=>win.webContents.executeJavaScript(s)
  const click=async text=>{await js(`(()=>{const b=[...document.querySelectorAll('button')].find(b=>b.textContent.trim()===${JSON.stringify(text)});if(!b||b.disabled)throw Error('按钮不可用 '+${JSON.stringify(text)});b.click()})()`);await pause(130)}
  const field=async(label,value)=>{await js(`(()=>{const e=[...document.querySelectorAll('label')].find(e=>e.querySelector('span')?.textContent===${JSON.stringify(label)}).querySelector('input');Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(e,${JSON.stringify(value)});e.dispatchEvent(new Event('input',{bubbles:true}))})()`);await pause(80)}
  const idle=async()=>{for(let i=0;i<100;i++){if(!await js(`!!document.querySelector('.bid-busy')`))return;await pause(100)}throw Error('等待UI超时')}
  const checks=[]
  try{
    await win.loadFile(join(runtime,'dist/index.html'));await pause(200)
    await js(`[...document.querySelectorAll('.entry-card')].find(b=>b.textContent.includes('勘察设计投标')).click()`);await pause(150)
    await js(`document.querySelectorAll('.bid-type-card')[1].click()`);await pause(200)
    await js(`document.querySelectorAll('.bid-sidebar nav button')[1].click()`);await pause(80)
    await click('导入本地资料');await idle();await click('保存');await pause(100)
    const bidId=store.list()[0].id,source=store.load(bidId).bid.sources[0]
    assert.equal(source.blocks.length,3);assert.equal(await js(`document.querySelectorAll('.bid-source-blocks>div:not(.bid-toolbar)').length`),3)
    await click('从此处建立要求');await field('要求标题','负责人注册证明');await click('保存')
    await click('定位来源摘录');assert.ok(await js(`document.querySelector('.bid-source-focused').textContent.includes('注册证书')`))
    await field('资料版本/日期标记','2');await click('保存');assert.equal(store.load(bidId).bid.sources[0].version,'2')
    checks.push('真实PDF导入/逐页正文/要求原文定位/修改版本保存')
    await click('AI辅助与设置');await field('API密钥','TEST-ONLY-NOT-A-REAL-KEY');await click('保存AI设置')
    await js(`document.querySelector('.bid-ai-panel .bid-fieldset .bid-check input').click()`);await pause(80)
    await js(`[...document.querySelectorAll('.bid-ai-panel .bid-check')].find(e=>e.textContent.includes('只发送指定物理页')).querySelector('input').click()`);await pause(80)
    await field('起始物理页','3');await field('结束物理页','3');await click('预览本次发送内容')
    const preview=await js(`document.querySelector('[aria-label="本次发送内容"]').value`);assert.ok(preview.includes('最终截止'));assert.ok(!preview.includes('注册证书'));assert.equal(calls,0)
    await click('取消发送');assert.equal(calls,0);await click('预览本次发送内容');await click('确认发送并生成');await pause(250)
    assert.equal(calls,1);assert.ok(sent.sources[0].blocks.every(b=>b.location.startsWith('PDF物理页 3 /')))
    await js(`document.querySelector('.bid-ai-job').open=true;document.querySelector('.bid-ai-job input').click()`);await pause(80);await click('加入所选候选');await idle()
    assert.ok(store.load(bidId).bid.requirements.some(r=>r.title==='截止按补遗确认'&&r.location.startsWith('PDF物理页 3 /')&&!r.confirmed))
    checks.push('选择PDF单页/取消0请求/确认1次模拟请求/逐字引用人工采纳')
    for(const [w,h] of [[1280,820],[900,680]]){
      win.setSize(w,h);await pause(80);assert.ok(await js(`document.documentElement.scrollWidth<=innerWidth+1`));await js(`document.querySelector('.bid-content').scrollTop=0`)
      writeFileSync(join(output,`pdf-ai-${w}.png`),(await win.webContents.capturePage()).toPNG())
    }
    await click('收起AI辅助');importPath=join(fixtures,'scan.pdf');await click('导入本地资料');await idle();await click('保存');await pause(100)
    assert.ok(await js(`document.querySelector('.bid-content').textContent.includes('未执行OCR')`));assert.equal(store.load(bidId).bid.sources[1].blocks.length,0)
    checks.push('扫描件无文字明确提示/两尺寸无横向溢出')
    const report={status:'BID_PDF_UI_OK',checks,calls,realNetworkRequests:0,output};writeFileSync(join(output,'result.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report))
  }finally{win.destroy();app.quit()}
}).catch(e=>{console.error(e);app.exit(1)})
