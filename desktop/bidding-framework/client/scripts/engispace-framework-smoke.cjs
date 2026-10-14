// 本地移植原型检查：真实上游IPC/SQLite/文件导入；所有网络以测试拒绝器替换。
const {app,BrowserWindow,dialog,session}=require('electron');
const assert=require('node:assert/strict');
const path=require('node:path');
const fs=require('node:fs');
const root=path.resolve(__dirname,'..');
const base=process.env.DSS_TEST_ROOT;
assert.ok(base&&!/^c:/i.test(base));
const output=fs.mkdtempSync(path.join(base,'framework-smoke-'));
app.setPath('userData',path.join(output,'user'));
let networkAttempts=0;
const rejectNetwork=async()=>{networkAttempts++;throw Error('TEST_NETWORK_DISABLED');};
global.fetch=rejectNetwork;
require('undici').fetch=rejectNetwork;
const pause=ms=>new Promise(resolve=>setTimeout(resolve,ms));
const watchdog=setTimeout(()=>{console.error('FRAMEWORK_CHECK_TIMEOUT');app.exit(1);},30000);
app.whenReady().then(async()=>{
  session.defaultSession.webRequest.onBeforeRequest({urls:['http://*/*','https://*/*']},(_details,callback)=>{networkAttempts++;callback({cancel:true});});
  const {registerIpcHandlers}=require('../electron/ipc/index.cjs');
  const win=new BrowserWindow({width:1280,height:820,show:false,webPreferences:{preload:path.join(root,'electron/preload.cjs'),contextIsolation:true,nodeIntegration:false,backgroundThrottling:false}});
  win.webContents.on('console-message',(_event,_level,message)=>console.log('renderer:',message));
  win.webContents.on('render-process-gone',(_event,details)=>console.error('RENDER_GONE',details.reason));
  const js=code=>win.webContents.executeJavaScript(code);
  let selection=[];
  dialog.showOpenDialog=async()=>({canceled:!selection.length,filePaths:selection});
  const services=registerIpcHandlers({app,mainWindow:win,checkAndDownloadUpdate:async()=>null,triggerUpdateDownload:async()=>null,quitAndInstall:()=>{},getLatestVersion:async()=>null,getUpdateDownloadUrl:()=>''});
  const wait=async(check,message)=>{for(let i=0;i<120;i++){if(await check())return;await pause(100);}throw Error(message);};
  const click=async text=>{await js(`(()=>{const element=[...document.querySelectorAll('button')].find(e=>e.textContent.trim()===${JSON.stringify(text)});if(!element||element.disabled)throw Error('按钮不可用 '+${JSON.stringify(text)});element.click()})()`);await pause(150);};
  try{
    console.log('CHECK_LOAD_START');await win.loadFile(path.join(root,'dist/index.html'));console.log('CHECK_LOAD_FINISHED');
    await wait(()=>js(`!![...document.querySelectorAll('button')].find(e=>e.textContent.trim()==='选择招标文件')`),'上游选择文件入口未出现');
    assert.ok(await js(`document.body.textContent.includes('EngiSpace')`));
    await click('选择招标文件');
    const empty=await js(`window.yibiao.technicalPlan.loadState()`);assert.equal((empty.tenderFiles||[]).length,0);
    const file=path.join(output,'脱敏招标条件.txt');fs.writeFileSync(file,'项目负责人须提供有效注册证书。\n不接受过期证明；条件以补遗为准。','utf8');
    selection=[file];await click('选择招标文件');
    await wait(()=>js(`document.body.textContent.includes('不接受过期证明')`),'真实导入后正文未出现');
    const loaded=await js(`window.yibiao.technicalPlan.loadState()`);assert.equal(loaded.tenderFiles.length,1);
    const markdown=await js(`window.yibiao.technicalPlan.readTenderMarkdown()`);assert.ok(markdown.includes('条件以补遗为准'));
    assert.ok(fs.existsSync(path.join(output,'user/workspace/yibiao.sqlite')));
    const stateAgain=await js(`window.yibiao.technicalPlan.loadState()`);assert.equal(stateAgain.tenderFiles[0].id,loaded.tenderFiles[0].id);
    const config=await js(`window.yibiao.technicalPlan.loadGenerationConfig()`);assert.ok(config);
    for(const [width,height] of [[1280,820],[900,680]]){
      win.setSize(width,height);await pause(400);await js(`document.querySelector('.upload-board').scrollIntoView({block:'start'});new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)))`);await pause(200);
      assert.ok(await js(`document.documentElement.scrollWidth<=innerWidth+1`));
      fs.writeFileSync(path.join(output,`framework-${width}.png`),(await win.webContents.capturePage()).toPNG());
    }
    const report={status:'BID_FRAMEWORK_SMOKE_OK',checks:['真实上游IPC/SQLite初始化','空工作区可选择招标文件且取消不改变','真实TXT导入、否定条件完整、保存回读','上游生成配置可加载','EngiSpace品牌/两尺寸无横向溢出'],networkAttempts,realNetworkRequests:0,output};
    fs.writeFileSync(path.join(output,'result.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report));
  }finally{await Promise.race([services.closeServices?.(),pause(2000)]);clearTimeout(watchdog);win.destroy();app.exit(0);}
}).catch(error=>{console.error(error);app.exit(1);});
