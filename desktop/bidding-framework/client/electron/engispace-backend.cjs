// 保留原业务服务在Electron41中运行，通过父子进程通道供平台内页面调用。
const {app,BrowserWindow,ipcMain,dialog,protocol,net}=require('electron');
const fs=require('node:fs'),path=require('node:path');
const wire=require('./services/engispaceWire.cjs');
if(typeof process.send!=='function')throw Error('投标引擎必须由平台本机通道启动。');
const handlers=new Map(),listeners=new Map(),nativePending=new Map();
let window,services,nativeWindowSend,closing=false,nextNativeId=0,initializing=true;
const send=message=>{if(process.connected)process.send(wire.encode(message),()=>{});};
const nativeHandle=ipcMain.handle.bind(ipcMain),nativeRemove=ipcMain.removeHandler.bind(ipcMain),nativeOn=ipcMain.on.bind(ipcMain),nativeRemoveListeners=ipcMain.removeAllListeners.bind(ipcMain);
const publishRegistry=()=>{if(!initializing)send({version:1,kind:'registry',invoke:[...handlers.keys()],events:[...listeners.keys()]});};
ipcMain.handle=(channel,handler)=>{nativeHandle(channel,handler);handlers.set(channel,handler);publishRegistry();};
ipcMain.removeHandler=channel=>{nativeRemove(channel);handlers.delete(channel);};
ipcMain.on=(channel,listener)=>{nativeOn(channel,listener);const current=listeners.get(channel)||[];current.push(listener);listeners.set(channel,current);publishRegistry();return ipcMain;};
ipcMain.removeAllListeners=channel=>{nativeRemoveListeners(channel);if(channel)listeners.delete(channel);else listeners.clear();return ipcMain;};
function nativeDialog(method,args){
  const options=args.at(-1)||{},id=++nextNativeId;
  // 对话框只传原有选项中的数据字段，不传窗口对象或回调。
  const allowed=['title','defaultPath','buttonLabel','filters','properties','message','detail','buttons','type','defaultId','cancelId','checkboxLabel','checkboxChecked','normalizeAccessKeys'];
  const value=Object.fromEntries(allowed.filter(key=>Object.hasOwn(options,key)).map(key=>[key,options[key]]));
  return new Promise((resolve,reject)=>{nativePending.set(id,{resolve,reject});send({version:1,kind:'native-dialog',id,method,options:value});});
}
for(const method of ['showOpenDialog','showSaveDialog','showMessageBox'])dialog[method]=(...args)=>nativeDialog(method,args);
protocol.registerSchemesAsPrivileged([{scheme:'yibiao-asset',privileges:{standard:true,secure:true,supportFetchAPI:true}}]);
app.whenReady().then(async()=>{
  protocol.handle('yibiao-asset',request=>{
    try{const url=new URL(request.url);if(!['credential-library','generated-images','imported-images'].includes(url.hostname))return new Response('Not found',{status:404});const base=path.resolve(app.getPath('userData'),'workspace',url.hostname),file=path.resolve(base,decodeURIComponent(url.pathname.replace(/^\/+/,'')));if(!file.startsWith(base+path.sep))return new Response('Forbidden',{status:403});return net.fetch(require('node:url').pathToFileURL(file).href);}catch{return new Response('Not found',{status:404});}
  });
  window=new BrowserWindow({show:false,webPreferences:{sandbox:false,contextIsolation:true,nodeIntegration:false}});
  nativeWindowSend=window.webContents.send.bind(window.webContents);
  window.webContents.send=(channel,...args)=>send({version:1,kind:'event',channel,args});
  const loaded=window.loadURL('about:blank');
  const {registerIpcHandlers}=require('./ipc/index.cjs');
  services=registerIpcHandlers({app,mainWindow:window,checkAndDownloadUpdate:async()=>({disabled:true}),triggerUpdateDownload:async()=>({disabled:true}),quitAndInstall:()=>({disabled:true}),getLatestVersion:async()=>null,getUpdateDownloadUrl:()=>'',gpuStartupState:{hardwareAccelerationEnabled:!app.commandLine.hasSwitch('disable-gpu')}});
  await loaded;
  initializing=false;
  send({version:1,kind:'ready',invoke:[...handlers.keys()],events:[...listeners.keys()],electron:process.versions.electron,abi:process.versions.modules,visibleWindowCount:BrowserWindow.getAllWindows().filter(window=>window.isVisible()).length});
}).catch(error=>{send({version:1,kind:'fatal',message:error.message});app.exit(1);});
process.on('message',async raw=>{
  let message;try{message=wire.decode(raw);}catch{return;}
  if(message?.version!==1)return;
  if(message.kind==='native-result'){
    const pending=nativePending.get(message.id);if(!pending)return;nativePending.delete(message.id);
    if(message.error)pending.reject(Error(message.error));else pending.resolve(message.value);return;
  }
  if(message.kind==='shutdown'){console.log('ENGINE_SHUTDOWN_RECEIVED');app.quit();return;}
  if(!window||window.isDestroyed())return;
  const event={sender:window.webContents,senderFrame:window.webContents.mainFrame};
  if(message.kind==='send'){
    for(const listener of listeners.get(message.channel)||[])listener(event,...(message.args||[]));return;
  }
  if(message.kind!=='invoke')return;
  try{
    const handler=handlers.get(message.channel);if(!handler)throw Error('不支持的投标操作');
    // 引擎没有独立发行/重启入口，不能从内嵌页另起不可控的进程。
    if(['app:start-gpu-hardware-acceleration-trial','app:relaunch-with-gpu-hardware-acceleration-disabled','app:quit-and-install'].includes(message.channel))throw Error('平台内投标模块不单独重启或升级，请先返回平台首页。');
    const value=await handler(event,...(message.args||[]));send({version:1,kind:'result',id:message.id,value});
  }catch(error){send({version:1,kind:'result',id:message.id,error:{message:error.message,code:error.code}});}
});
process.on('disconnect',()=>app.quit());
app.on('before-quit',event=>{
  if(closing)return;event.preventDefault();closing=true;
  for(const pending of nativePending.values())pending.reject(Error('投标引擎关闭'));nativePending.clear();
  Promise.resolve().then(()=>services?.closeServices?.()).catch(()=>{}).finally(()=>{
    if(window&&!window.isDestroyed())window.webContents.send=nativeWindowSend;
    if(process.connected)process.disconnect();
    app.exit(0);
  });
});
