const fs=require('node:fs');
const path=require('node:path');
function isUpstreamService(value){
  try{const host=new URL(value).hostname.toLowerCase();return host==='agnet.top'||host.endsWith('.agnet.top')||host==='yibiao.pro'||host.endsWith('.yibiao.pro');}catch{return false;}
}
function recordLocalRequest(root,url,body){
  try{
    const directory=path.join(root,'local-statistics');fs.mkdirSync(directory,{recursive:true});
    const parsed=new URL(url),isMetric=parsed.pathname==='/track';
    // 普通统计沿用原字段。账号/设备许可等请求只登记路径，不保存凭据或请求正文。
    let payload;
    if(isMetric&&typeof body==='string'){try{payload=JSON.parse(body);}catch{payload={unreadable:true};}}
    fs.appendFileSync(path.join(directory,isMetric?'events.ndjson':'blocked-services.ndjson'),JSON.stringify({at:new Date().toISOString(),path:parsed.pathname,payload,uploaded:false})+'\n','utf8');
  }catch{/* 统计写盘失败不影响编制；上游外发仍禁止。 */}
}
module.exports={recordLocalRequest,isUpstreamService};
