// EngiSpace接续：保留上游业务入口，隔离数据并把上游自动统计留在本机。
const {app,session}=require('electron');
const fs=require('node:fs');
const path=require('node:path');
const root=path.resolve(process.env.ENGISPACE_BIDDING_DATA_ROOT||path.join(path.dirname(process.execPath),'../portable-data/bidding-framework'));
if(path.parse(root).root.toLowerCase()==='c:\\')throw Error('投标模块请放在G盘项目目录');
fs.mkdirSync(path.join(root,'temp'),{recursive:true});
app.setPath('userData',root);app.setPath('sessionData',root);app.setPath('temp',path.join(root,'temp'));
process.env.TEMP=path.join(root,'temp');process.env.TMP=process.env.TEMP;
process.env.ENGISPACE_BIDDING_INTEGRATED='1';
app.setName('EngiSpace Bidding');
if(!app.requestSingleInstanceLock()){app.quit();}else{
  const {recordLocalRequest,isUpstreamService}=require('./services/engispaceLocalAnalytics.cjs');
  const guard=fetcher=>async(url,options={})=>{
    const target=String(url?.url||url);
    if(isUpstreamService(target)){
      recordLocalRequest(root,target,options.body);
      throw Error('上游自动联网已关闭；统计与诊断保留本机。请配置自己的AI/解析API。');
    }
    return fetcher(url,options);
  };
  global.fetch=guard(global.fetch);
  const undici=require('undici');undici.fetch=guard(undici.fetch);
  app.whenReady().then(()=>session.defaultSession.webRequest.onBeforeRequest({urls:['http://*/*','https://*/*']},(details,callback)=>{
    if(isUpstreamService(details.url)){
      const bytes=(details.uploadData||[]).filter(item=>item.bytes).map(item=>item.bytes);
      recordLocalRequest(root,details.url,bytes.length?Buffer.concat(bytes).toString('utf8'):undefined);
      callback({cancel:true});
    }else callback({});
  }));
  app.on('second-instance',()=>{const {BrowserWindow}=require('electron');const window=BrowserWindow.getAllWindows()[0];if(window){if(window.isMinimized())window.restore();window.show();window.focus();}});
  app.on('browser-window-created',(_event,window)=>{
    if(process.env.DSS_FRAMEWORK_HIDE==='1')window.show=()=>{};
  });
  require('./main.cjs');
}
