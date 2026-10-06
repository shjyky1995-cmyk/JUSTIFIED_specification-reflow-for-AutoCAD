// 隐藏运行真实父主进程和成品子EXE，CDP只绑定本机且只在此测试追加。
const {app}=require('electron');
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),http=require('node:http');
const {pathToFileURL}=require('node:url');
const bundle=process.env.DSS_BID_BUNDLE;
const output=fs.mkdtempSync(path.join(process.env.DSS_TEST_ROOT,'framework-launch-'));
process.env.DSS_USER_DATA_ROOT=path.join(output,'user');
process.env.DSS_FRAMEWORK_HIDE='1';
Object.defineProperty(app,'isPackaged',{value:true});
Object.defineProperty(process,'resourcesPath',{value:path.join(bundle,'client/resources')});
const cp=require('node:child_process'),nativeSpawn=cp.spawn;
let child,childExit,closed=false;
cp.spawn=function(command,args,options){
  const result=nativeSpawn(command,command.endsWith('EngiSpace-Bidding.exe')?[...args,'--remote-debugging-port=19238','--remote-debugging-address=127.0.0.1']:args,options);
  if(command.endsWith('EngiSpace-Bidding.exe')){child=result;childExit=new Promise(r=>result.once('exit',r));}
  return result;
};
require('node:module').syncBuiltinESMExports();
const pause=ms=>new Promise(r=>setTimeout(r,ms));
const json=url=>new Promise((resolve,reject)=>http.get(url,res=>{let data='';res.on('data',s=>data+=s);res.on('end',()=>{try{resolve(JSON.parse(data))}catch(e){reject(e)}})}).on('error',reject));
async function connect(){
  for(let i=0;i<100;i++){
    try{
      const tabs=await json('http://127.0.0.1:19238/json');
      const page=tabs.find(t=>t.type==='page'&&t.url.includes('index.html'));
      if(page){
        const ws=new WebSocket(page.webSocketDebuggerUrl),pending=new Map();let next=0;
        ws.onmessage=e=>{const m=JSON.parse(e.data);if(m.id&&pending.has(m.id)){pending.get(m.id)(m);pending.delete(m.id)}};
        await new Promise((r,j)=>{ws.onopen=r;ws.onerror=j});
        return {ws,eval:code=>new Promise((resolve,reject)=>{const id=++next;pending.set(id,m=>m.error||m.result?.exceptionDetails?reject(Error(JSON.stringify(m))):resolve(m.result.result.value));ws.send(JSON.stringify({id,method:'Runtime.evaluate',params:{expression:code,returnByValue:true,awaitPromise:true}}))})};
      }
    }catch{}await pause(100);
  }throw Error('成品子窗口未开放本机测试连接');
}
app.on('browser-window-created',(_event,win)=>{
  win.show=()=>{};
  let hides=0;const hide=win.hide.bind(win);win.hide=()=>{hides++;hide()};
  win.webContents.once('did-finish-load',async()=>{
    try{
      await pause(300);const js=s=>win.webContents.executeJavaScript(s);
      await js(`[...document.querySelectorAll('.entry-card')].find(b=>b.textContent.includes('勘察设计投标')).click()`);
      for(let i=0;i<200&&!hides;i++)await pause(100);
      assert.ok(hides>0,'首页应在子模块加载后隐藏');assert.ok(child);
      const cdp=await connect();
      assert.ok(await cdp.eval(`document.body.textContent.includes('EngiSpace')&&document.body.textContent.includes('选择招标文件')`));
      assert.ok(await cdp.eval(`typeof window.yibiao.technicalPlan.loadState==='function'`));
      assert.ok(fs.existsSync(path.join(output,'user/data/bidding-framework/workspace/yibiao.sqlite'))||fs.existsSync(path.join(output,'user/bidding-framework/workspace/yibiao.sqlite')));
      const returned=new Promise(r=>{const show=win.show;win.show=()=>{closed=true;show();r()}});
      await cdp.eval(`setTimeout(()=>window.close(),100);true`);cdp.ws.close();
      assert.equal(await Promise.race([childExit,pause(10000).then(()=>-99)]),0);
      await Promise.race([returned,pause(3000)]);assert.ok(closed,'子模块关闭后应恢复父窗口');
      await js(`document.querySelector('[data-open-legacy-bid]').click()`);await pause(250);
      assert.ok(await js(`!!document.querySelector('.bid-type-card')`));
      const report={status:'FRAMEWORK_LAUNCH_OK',output,checks:['首页启动真实独立成品EXE','准备完成后父窗口隐藏','成品原文件选择入口/SQLite初始化','关闭子窗口退出0并返回父工作台','旧版投标草稿入口可用']};
      fs.writeFileSync(path.join(output,'result.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report));win.close();
    }catch(e){console.error(e);if(child&&!child.killed)child.kill();app.exit(1);}
  });
});
import(pathToFileURL(path.join(bundle,'client/resources/app/dist-electron/electron/main.js')).href).catch(e=>{console.error(e);app.exit(1)});
