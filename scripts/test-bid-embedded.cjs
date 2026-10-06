// 同主窗口内嵌真实业务：Electron44页面 + Electron41引擎 + 原SQLite/Word助手。
const {app,BrowserWindow,dialog}=require('electron');
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {pathToFileURL}=require('node:url');
const root=path.resolve(__dirname,'..'),base=process.env.DSS_TEST_ROOT;
assert.ok(base&&!/^c:/i.test(base));
const output=fs.mkdtempSync(path.join(base,'embedded-'));
const bundle=process.env.DSS_BID_BUNDLE;
const framework=bundle?path.join(bundle,'client/resources/bidding-framework/resources/app.asar'):path.join(root,'desktop/bidding-framework/client');
const wire=require(path.join(framework,'electron/services/engispaceWire.cjs'));
if(bundle){Object.defineProperty(app,'isPackaged',{value:true});Object.defineProperty(process,'resourcesPath',{value:path.join(bundle,'client/resources')});}
process.env.DSS_USER_DATA_ROOT=path.join(output,'user');
process.env.YIBIAO_OPENXML_HELPER_DIR=bundle?path.join(bundle,'client/resources/bidding-framework/resources/openxml-tools/win32-x64'):path.join(framework,'vendor/openxml-tools/win32-x64');
let selection=[],savePath=path.join(output,'嵌入导出.docx'),dialogOwner;
dialog.showOpenDialog=async(owner,_options)=>{dialogOwner=owner;return {canceled:!selection.length,filePaths:selection}};
dialog.showSaveDialog=async(owner,_options)=>{dialogOwner=owner;return {canceled:false,filePath:savePath}};
const cp=require('node:child_process'),nativeSpawn=cp.spawn;
let engine,engineReady,engineExit;
cp.spawn=(command,args,options)=>{
  const child=nativeSpawn(command,args,options);
  if(args.includes('--engispace-backend')){engine=child;engineExit=new Promise(r=>child.once('exit',r));child.on('message',raw=>{const m=wire.decode(raw);if(m.kind==='ready')engineReady=m});child.stderr.on('data',b=>fs.appendFileSync(path.join(output,'engine-stderr.log'),b));child.stdout.on('data',b=>fs.appendFileSync(path.join(output,'engine-stdout.log'),b));}
  return child;
};
require('node:module').syncBuiltinESMExports();
const pause=ms=>new Promise(r=>setTimeout(r,ms));
const wait=async(predicate,message)=>{for(let i=0;i<150;i++){if(await predicate())return;await pause(100)}throw Error(message)};
app.on('browser-window-created',(_event,win)=>{
  const nativeShow=win.show.bind(win);
  win.show=()=>{};
  win.webContents.once('did-finish-load',async()=>{
    try{
      const main=s=>win.webContents.executeJavaScript(s);await pause(300);const nativeId=win.id;
      await main(`[...document.querySelectorAll('.entry-card')].find(b=>b.textContent.includes('勘察设计投标')).click()`);
      await wait(()=>win.contentView.children.length>0,'投标视图未进入平台');
      let panel=win.contentView.children[0];const js=s=>panel.webContents.executeJavaScript(s);
      panel.webContents.on('console-message',event=>{if(event.level==='error')fs.appendFileSync(path.join(output,'renderer-errors.log'),event.message+'\n')});
      await wait(async()=>await js(`document.body.textContent.includes('选择招标文件')`),'原框架未就绪');
      console.log('EMBEDDED_UI_READY');
      assert.equal(win.id,nativeId);assert.equal(BrowserWindow.getAllWindows().length,1);
      assert.equal(engineReady.abi,'145');assert.equal(engineReady.visibleWindowCount,0);
      assert.equal(await js(`window.yibiao.embeddedInWorkbench`),true);
      assert.equal(await js(`navigator.userAgent.includes('Electron/44')`),true);
      await js(`fetch('https://agnet.top/track',{method:'POST',body:JSON.stringify({event:'embedded-check'})}).catch(()=>true)`);
      await js(`fetch('https://agnet.top/api/license',{method:'POST',body:JSON.stringify({key:'dummy-do-not-log-key'})}).catch(()=>true)`);
      const statistics=path.join(output,'user/data/bidding-framework/local-statistics');
      assert.ok(fs.readFileSync(path.join(statistics,'events.ndjson'),'utf8').includes('embedded-check'));
      assert.ok(!fs.readFileSync(path.join(statistics,'blocked-services.ndjson'),'utf8').includes('dummy-do-not-log-key'));
      const original=panel.getBounds();win.setSize(1100,780);await pause(150);assert.notDeepEqual(panel.getBounds(),original);
      const click=()=>js(`(()=>{const b=[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==='选择招标文件');if(!b)throw Error('没有选择文件按钮');b.click()})()`);
      await click();await pause(200);assert.equal((await js(`window.yibiao.technicalPlan.loadState()`)).tenderFiles.length,0);assert.equal(dialogOwner,win);
      const file=path.join(output,'脱敏招标.txt');fs.writeFileSync(file,'项目负责人须提供注册证书。\n不接受过期证明。');selection=[file];await click();
      await wait(()=>js(`document.body.textContent.includes('不接受过期证明')`),'真实本机文件导入失败');
      console.log('EMBEDDED_IMPORT_OK');
      assert.equal(dialogOwner,win);assert.equal((await js(`window.yibiao.technicalPlan.loadState()`)).tenderFiles.length,1);
      await js(`window.yibiao.technicalPlan.saveOutline({project_name:'平台内核验',outline:[{id:'chapter',level:1,title:'勘察工作安排',children:[]}]})`);
      const config=require(path.join(framework,'electron/services/exportFormatDefaults.cjs')).cloneDefaultExportFormat();
      const result=await js(`window.yibiao.export.exportWord({source:'technical-plan',export_format:${JSON.stringify(config)}})`);assert.equal(result.success,true);assert.equal(dialogOwner,win);
      console.log('EMBEDDED_WORD_OK');
      const preview=await js(`(async()=>{const value=await window.yibiao.templates.renderPreview('<h1>二进制预览</h1><p>保留原文条件。</p>',${JSON.stringify(config)});return {typed:value.bytes instanceof Uint8Array,length:value.bytes.length,first:[...value.bytes.slice(0,2)]}})()`);
      assert.equal(preview.typed,true);assert.ok(preview.length>1000);assert.deepEqual(preview.first,[80,75]);
      const images=path.join(output,'user/data/bidding-framework/workspace/imported-images');fs.mkdirSync(images,{recursive:true});fs.writeFileSync(path.join(images,'check.png'),Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a8V8AAAAASUVORK5CYII=','base64'));
      const image=await js(`(async()=>{const value=await fetch('yibiao-asset://imported-images/check.png');return [...new Uint8Array(await value.arrayBuffer()).slice(0,4)]})()`);assert.deepEqual(image,[137,80,78,71]);
      assert.equal(await js(`fetch('yibiao-asset://imported-images/..%5c..%5coutside.txt').then(value=>value.status)`),403);
      const AdmZip=require(path.join(framework,'node_modules/adm-zip'));assert.ok(new AdmZip(savePath).readAsText('word/document.xml').includes('勘察工作安排'));
      await js(`document.querySelectorAll('.app-toast-close').forEach(button=>button.click());true`);
      nativeShow();panel.webContents.invalidate();await pause(350);
      fs.writeFileSync(path.join(output,'platform-bidding.png'),(await panel.webContents.capturePage()).toPNG());win.hide();
      await js(`document.querySelector('.engispace-module-header button').click();true`);
      await wait(()=>win.contentView.children.length===0,'返回首页没有移除投标视图');assert.equal(await Promise.race([engineExit,pause(10000).then(()=>-99)]),0);
      assert.equal(win.id,nativeId);assert.equal(BrowserWindow.getAllWindows().length,1);
      await main(`[...document.querySelectorAll('.entry-card')].find(b=>b.textContent.includes('勘察设计投标')).click()`);
      await wait(()=>win.contentView.children.length>0,'重新进入失败');panel=win.contentView.children[0];
      await wait(()=>panel.webContents.executeJavaScript(`document.body.textContent.includes('阶段')||document.body.textContent.includes('选择标书')`),'重新进入未加载');
      const state=await panel.webContents.executeJavaScript(`window.yibiao.technicalPlan.loadState()`);assert.equal(state.tenderFiles.length,1);assert.equal(state.outlineData.outline[0].title,'勘察工作安排');
      engine.kill();await wait(()=>win.contentView.children.length===0,'引擎失败没有恢复平台');assert.ok(await main(`document.body.textContent.includes('投标引擎已断开')`));
      await main(`[...document.querySelectorAll('.entry-card')].find(b=>b.textContent.includes('勘察设计投标')).click()`);await wait(()=>win.contentView.children.length>0,'失败后重试未进入');panel=win.contentView.children[0];
      await wait(()=>panel.webContents.executeJavaScript(`document.body.textContent.includes('选择标书')`),'重试未加载');assert.equal((await panel.webContents.executeJavaScript(`window.yibiao.technicalPlan.loadState()`)).tenderFiles.length,1);
      const report={status:'BID_EMBEDDED_OK',mode:bundle?'packaged':'source',output,checks:['单个平台主窗口/后台无可见窗口','44前端/41引擎ABI145','上游统计阻断/本机留存/许可凭据不落统计','窗口缩放视图跟随','平台原生文件选择/取消/TXT导入','原SQLite目录保存','平台原生Word导出/正文读回','真实DOCX二进制预览跨版本返回','受限图片资源可读/越界拒绝','返回首页同窗口且引擎退出0','重新进入资料目录保留','引擎故障平台保留/可重试']};
      win.once('closed',async()=>{assert.equal(await Promise.race([engineExit,pause(10000).then(()=>-99)]),0);report.checks.push('平台关窗经过原离开保护/后台退出0');fs.writeFileSync(path.join(output,'result.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report));});win.close();
    }catch(error){console.error(error);console.error('OUTPUT',output);const panel=win.contentView.children[0];if(panel&&!panel.webContents.isDestroyed())console.error('BODY',await panel.webContents.executeJavaScript('document.body.textContent.slice(0,1500)'));if(engine&&!engine.killed)engine.kill();app.exit(1)}
  });
});
import(pathToFileURL(bundle?path.join(bundle,'client/resources/app/dist-electron/electron/main.js'):path.join(root,'desktop/app/dist-electron/electron/main.js')).href).catch(error=>{console.error(error);app.exit(1)});
