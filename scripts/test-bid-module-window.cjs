// 真实窗口显示/最小化恢复/平台返回；只使用隔离数据，不操作用户运行中的实例。
const {app}=require('electron');
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),http=require('node:http');
const {pathToFileURL}=require('node:url');
const bundle=process.env.DSS_BID_BUNDLE;
assert.ok(bundle&&process.env.DSS_TEST_ROOT&&!/^c:/i.test(process.env.DSS_TEST_ROOT));
const output=fs.mkdtempSync(path.join(process.env.DSS_TEST_ROOT,'module-window-'));
process.env.DSS_USER_DATA_ROOT=path.join(output,'user');
process.env.DSS_FRAMEWORK_HIDE='1'; // 确认生产启动器确实清除此检查开关。
Object.defineProperty(app,'isPackaged',{value:true});
Object.defineProperty(process,'resourcesPath',{value:path.join(bundle,'client/resources')});
const cp=require('node:child_process'),nativeSpawn=cp.spawn;
let child,childExit,ready,failNext=false;
cp.spawn=function(command,args,options){
  const module=command.endsWith('EngiSpace-Bidding.exe');
  if(module){assert.equal(options.windowsHide,false);assert.equal(options.env.DSS_FRAMEWORK_HIDE,undefined)}
  if(module&&failNext){failNext=false;return nativeSpawn(path.join(output,'missing-module.exe'),[],options)}
  const result=nativeSpawn(command,module?[...args,'--remote-debugging-port=19239','--remote-debugging-address=127.0.0.1']:args,options);
  if(module&&options.stdio!=='ignore'){
    child=result;childExit=new Promise(r=>result.once('exit',r));let buffered='';
    result.stdout.on('data',bytes=>{buffered+=bytes.toString();const line=buffered.split(/\r?\n/).find(line=>line.startsWith('ENGISPACE_BIDDING_READY '));if(line){try{ready=JSON.parse(line.slice('ENGISPACE_BIDDING_READY '.length))}catch{}}});
  }
  return result;
};
require('node:module').syncBuiltinESMExports();
const pause=ms=>new Promise(r=>setTimeout(r,ms));
const json=url=>new Promise((resolve,reject)=>http.get(url,res=>{let data='';res.on('data',s=>data+=s);res.on('end',()=>{try{resolve(JSON.parse(data))}catch(e){reject(e)}})}).on('error',reject));
async function connect(){
  for(let i=0;i<100;i++){
    try{
      const tabs=await json('http://127.0.0.1:19239/json');const page=tabs.find(t=>t.type==='page'&&t.url.includes('index.html'));
      if(page){
        const ws=new WebSocket(page.webSocketDebuggerUrl),pending=new Map();let next=0;
        ws.onmessage=e=>{const m=JSON.parse(e.data);if(m.id&&pending.has(m.id)){pending.get(m.id)(m);pending.delete(m.id)}};
        await new Promise((r,j)=>{ws.onopen=r;ws.onerror=j});
        const call=(method,params={})=>new Promise((resolve,reject)=>{const id=++next;pending.set(id,m=>m.error||m.result?.exceptionDetails?reject(Error(JSON.stringify(m))):resolve(m.result));ws.send(JSON.stringify({id,method,params}))});
        return {ws,call,eval:async expression=>(await call('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true})).result.value};
      }
    }catch{}await pause(100);
  }throw Error('子模块测试连接未出现');
}
app.on('browser-window-created',(_event,win)=>{
  win.show=()=>{}; // 父测试窗口隐藏，子模块必须真实显示。
  let hides=0,restores=0;const nativeHide=win.hide.bind(win);win.hide=()=>{hides++;nativeHide()};
  win.webContents.once('did-finish-load',async()=>{
    try{
      const js=s=>win.webContents.executeJavaScript(s);await pause(300);
      await js(`[...document.querySelectorAll('.entry-card')].find(b=>b.textContent.includes('勘察设计投标')).click()`);
      for(let i=0;i<250&&!hides;i++)await pause(100);
      assert.ok(hides>0,'原生投标窗口可见后才应隐藏父平台');assert.ok(ready);assert.equal(ready.visible,true);
      assert.deepEqual(ready.bounds,win.getBounds());
      const cdp=await connect();
      for(let i=0;i<100;i++){if(await cdp.eval(`document.body.textContent.includes('选择招标文件')`))break;await pause(100)}
      assert.ok(await cdp.eval(`document.body.textContent.includes('返回平台首页')&&document.body.textContent.includes('选择招标文件')`));
      const native=action=>JSON.parse(cp.execFileSync('powershell.exe',['-NoProfile','-NonInteractive','-File',path.join(__dirname,'test-bid-native-window.ps1'),'-TargetProcessId',String(child.pid),'-OwnerProcessId',String(process.pid),'-Action',action],{windowsHide:true,encoding:'utf8'}));
      const first=native('Read');assert.equal(first.visible,true);assert.equal(first.minimized,false);
      assert.equal(native('Minimize').minimized,true);
      const again=await js(`window.workbench.openBidFramework()`);assert.equal(again.success,true);
      assert.equal(native('Read').minimized,false);
      win.show=()=>{restores++};
      await cdp.eval(`document.querySelector('.engispace-module-header button').click();true`);cdp.ws.close();
      assert.equal(await Promise.race([childExit,pause(10000).then(()=>-99)]),0);assert.ok(restores>0);
      const before=hides;failNext=true;
      const failed=await js(`window.workbench.openBidFramework()`);assert.equal(failed.success,false);assert.equal(hides,before);assert.ok(restores>1);
      const log=fs.readFileSync(path.join(output,'user/data/bidding-framework/module-startup.log'),'utf8');assert.ok(log.includes('visible-ready')&&log.includes('spawn-error'));
      await js(`document.querySelector('[data-open-legacy-bid]').click()`);await pause(200);assert.ok(await js(`!!document.querySelector('.bid-type-card')`));
      const report={status:'BID_MODULE_WINDOW_OK',output,checks:['生产GUI未隐藏/不继承隐藏测试开关','原生窗口实际可见后才隐藏平台','跟随平台窗口位置和尺寸','平台模块导航/直接选文件入口','最小化后重新点击入口恢复既有模块','返回平台首页正常退出0','启动失败保留平台且可重试','本地启动日志只记录状态','旧版草稿入口保持']};
      fs.writeFileSync(path.join(output,'result.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report));win.close();
    }catch(error){console.error(error);if(child&&!child.killed)child.kill();app.exit(1)}
  });
});
import(pathToFileURL(path.join(bundle,'client/resources/app/dist-electron/electron/main.js')).href).catch(error=>{console.error(error);app.exit(1)});
