// 直接启动便携EXE，以隔离数据验证可见主窗口、内嵌入口与退出；不触碰用户实例。
import { spawn, execFileSync } from 'node:child_process'
import { mkdtempSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import assert from 'node:assert/strict'
const base=process.env.DSS_TEST_ROOT, bundle=process.env.DSS_BID_BUNDLE
assert.ok(base && bundle && !/^c:/i.test(base))
const output=mkdtempSync(join(base,'platform-exe-'))
const child=spawn(join(bundle,'client/EngiSpace.exe'),['--remote-debugging-port=19340','--remote-debugging-address=127.0.0.1'],{env:{...process.env,DSS_USER_DATA_ROOT:join(output,'user')},windowsHide:false,stdio:['ignore','pipe','pipe']})
let logs='';child.stderr.on('data',value=>{logs+=value});child.stdout.resume()
const exited=new Promise(resolve=>child.once('exit',code=>resolve(code)))
const pause=ms=>new Promise(resolve=>setTimeout(resolve,ms))
const sockets=[]
async function wait(predicate,label){for(let i=0;i<150;i++){try{const value=await predicate();if(value)return value}catch{}await pause(100)}throw Error(label)}
async function tabs(){return await (await fetch('http://127.0.0.1:19340/json')).json()}
async function connect(page){
  const socket=new WebSocket(page.webSocketDebuggerUrl),pending=new Map();let id=0;sockets.push(socket)
  await new Promise((resolve,reject)=>{socket.onopen=resolve;socket.onerror=reject})
  socket.onmessage=event=>{const value=JSON.parse(event.data);pending.get(value.id)?.(value);pending.delete(value.id)}
  return async expression=>await new Promise((resolve,reject)=>{const key=++id;pending.set(key,value=>value.error||value.result?.exceptionDetails?reject(Error(JSON.stringify(value))):resolve(value.result.result.value));socket.send(JSON.stringify({id:key,method:'Runtime.evaluate',params:{expression,awaitPromise:true,returnByValue:true}}))})
}
function nativeState(){
  return JSON.parse(execFileSync('powershell.exe',['-NoProfile','-Command',`Get-Process -Id ${child.pid} | Select-Object Id,MainWindowHandle,MainWindowTitle | ConvertTo-Json -Compress`],{windowsHide:true,encoding:'utf8'}))
}
try{
  const page=await wait(async()=> (await tabs()).find(value=>value.type==='page'&&value.url.includes('/app/dist/index.html')),'平台EXE首页未出现')
  const main=await connect(page);await wait(()=>main(`document.querySelectorAll('.entry-card').length>0`),'平台首页未就绪')
  const before=await wait(()=>{const state=nativeState();return state.MainWindowHandle?state:false},'成品主窗口不可见')
  await main(`[...document.querySelectorAll('.entry-card')].find(button=>button.textContent.includes('勘察设计投标')).click();true`)
  const bidding=await wait(async()=> (await tabs()).find(value=>value.type==='page'&&value.url.includes('/bidding-framework/')&&value.url.includes('/dist/index.html')),'成品没有投标视图')
  const panel=await connect(bidding);await wait(()=>panel(`document.body.textContent.includes('选择招标文件')`),'成品投标未就绪')
  assert.equal(await panel(`window.yibiao.embeddedInWorkbench`),true)
  assert.equal(await panel(`navigator.userAgent.includes('Electron/44')`),true)
  const after=nativeState();assert.equal(after.MainWindowHandle,before.MainWindowHandle)
  await panel(`document.querySelector('.engispace-module-header button').click();true`)
  await wait(async()=> !(await tabs()).some(value=>value.id===bidding.id),'返回首页没有退出投标视图')
  await wait(()=>main(`!document.body.textContent.includes('正在打开投标')`),'平台返回仍在等待')
  assert.equal(nativeState().MainWindowHandle,before.MainWindowHandle)
  await main('window.close();true')
  assert.equal(await Promise.race([exited,pause(15000).then(()=>-99)]),0)
  const report={status:'BID_PLATFORM_EXE_OK',output,pid:child.pid,windowHandle:before.MainWindowHandle,checks:['直接成品EXE可见主窗口','平台入口原框架加载','44前端内嵌模式','进入与返回主窗口句柄保持','成品关窗退出0']}
  writeFileSync(join(output,'result.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report))
}catch(error){writeFileSync(join(output,'stderr.log'),logs);console.error(error);process.exitCode=1;if(child.exitCode===null)child.kill()}
finally{for(const socket of sockets)socket.close()}
