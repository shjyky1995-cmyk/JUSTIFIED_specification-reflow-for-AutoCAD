import assert from 'node:assert/strict'
import { mkdtempSync, writeFileSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { createBidStore } from '../desktop/app/src/features/bidding/store.ts'
import { newBid, bidId, updateBidSource, addSource } from '../desktop/app/src/features/bidding/model.ts'
import { createBidAiService } from '../desktop/app/electron/bid-ai.ts'

const base=process.env.DSS_TEST_ROOT
assert.ok(base&&!/^c:/i.test(base),'测试输出须在G盘项目目录')
const output=mkdtempSync(join(base,'candidate-review-')),store=createBidStore(output)
const file=join(output,'脱敏招标资料.txt');writeFileSync(file,'测试原件，不联网')
const source=store.importAsset(file)
source.version='初稿';source.blocks=[{location:'第1段',text:'普通项目描述。'},{location:'第2段',text:'负责人须提供注册证书；不接受过期证明。'}]
let bid=newBid('design');bid.sources=[source];bid.sections[0].body='人工正文，始终保留'
function save(value){const saved=store.save(value);assert.equal(saved.ok,true,saved.error);return store.load(value.id).bid}
bid=save(bid)
let calls=0,mode='success',streamCanceled=0
const reply=input=>JSON.stringify({choices:[{finish_reason:'stop',message:{content:JSON.stringify({requirements:[{title:'负责人资格',kind:'资格',reference:{sourceId:input.sources[0].id,index:input.sources[0].blocks[1].index,quote:input.sources[0].blocks[1].text}}]})}}]})
const fetcher=async(_url,options)=>{
  calls++
  if(mode==='body-wait')return new Response(new ReadableStream({start(controller){controller.enqueue(new TextEncoder().encode('{'))},cancel(){streamCanceled++;return new Promise(()=>{})}}))
  return new Response(reply(JSON.parse(JSON.parse(options.body).messages[1].content)))
}
const vault={available:()=>true,encrypt:s=>Buffer.from(s),decrypt:b=>b.toString()}
const service=createBidAiService(output,vault,fetcher,90)
service.configure('deepseek-flash','TEST-ONLY-NOT-A-REAL-KEY')
const selection={bidId:bid.id,kind:'requirements',sourceIds:[source.id],sectionId:''}
let checks=0
async function check(name,fn){await fn();checks++;console.log('PASS '+name)}
const pause=ms=>new Promise(r=>setTimeout(r,ms))
async function finish(job){for(let i=0;i<100;i++){const j=service.list(bid.id).find(j=>j.id===job.id);if(j.state!=='running')return j;await pause(10)}throw Error('任务未结束')}
const start=()=>service.start(service.prepare(selection).token,true)
const ready=()=>finish(start())
await check('连续取消12次预览不收费、不耗尽待确认额度',async()=>{
  const previous=calls
  for(let i=0;i<12;i++){const p=service.prepare(selection);service.discard(p.token);assert.throws(()=>service.start(p.token,true),/确认已失效/)}
  assert.equal(calls,previous)
})
let job=await ready()
await check('新任务保存原资料版本、引用位置且不自动写正文',()=>{
  assert.equal(job.sourceContext[0].version,'初稿');assert.equal(job.reviewIssue,'')
  assert.equal(job.result.requirements[0].reference.index,1)
  assert.equal(store.load(bid.id).bid.requirements.length,0)
})
await check('修改核对状态及资料列表顺序仍可采纳',()=>{
  const unrelated={...source,id:bidId(),name:'不相关招标资料',version:'独立资料'}
  bid=save({...bid,sources:[unrelated,{...bid.sources[0],confirmed:true}],requirementsReviewed:true})
  assert.equal(service.list(bid.id).find(j=>j.id===job.id).reviewIssue,'')
  bid=service.apply(bid.id,job.id,[0]);assert.equal(bid.requirements.length,1);assert.equal(bid.requirements[0].confirmed,false)
  bid=service.apply(bid.id,job.id,[0]);assert.equal(bid.requirements.length,1)
  const unrelatedId=bid.sources.find(s=>s.id!==source.id).id
  bid=save(updateBidSource(bid,unrelatedId,{version:'不相关资料第二版'}))
  assert.equal(service.list(bid.id).find(j=>j.id===job.id).reviewIssue,'')
})
for(const [name,patch] of [['版本',{version:'第二版'}],['类型',{kind:'补遗答疑'}],['补遗目标',{supersedes:bid.sources.find(s=>s.id!==source.id).id}]]){
  job=await ready()
  await check('仅改变资料'+name+'也拒绝旧候选、保留原结果和人工正文',()=>{
    const previous=store.load(bid.id).bid
    bid=save(updateBidSource(bid,source.id,patch))
    const record=service.list(bid.id).find(j=>j.id===job.id)
    assert.match(record.reviewIssue,/变化/);assert.equal(record.state,'succeeded');assert.ok(record.result)
    assert.throws(()=>service.apply(bid.id,job.id,[0]),/变化/)
    assert.equal(bid.sections[0].body,'人工正文，始终保留')
    bid=save({...previous,revision:bid.revision})
  })
}
job=await ready()
await check('新增所选资料的补遗使旧候选失效；另一标段不受影响',()=>{
  const other=save(newBid('survey'))
  const supplement={...source,id:bidId(),name:'补遗一',kind:'补遗答疑',supersedes:source.id,version:'补遗版'}
  bid=save(addSource(bid,supplement))
  assert.match(service.list(bid.id).find(j=>j.id===job.id).reviewIssue,/补遗/)
  assert.throws(()=>service.apply(bid.id,job.id,[0]),/补遗/)
  assert.equal(store.load(other.id).bid.sources.length,0);assert.equal(service.list(other.id).length,0)
})
job=await ready()
await check('补遗的后续补遗变化也失效、其他独立资料版本不影响',()=>{
  const parent=bid.sources.at(-1)
  bid=save(addSource(bid,{...source,id:bidId(),name:'补遗二',kind:'补遗答疑',supersedes:parent.id}))
  assert.match(service.list(bid.id).find(j=>j.id===job.id).reviewIssue,/补遗/)
})
job=await ready()
await check('只选补遗时关联原版的版本变化也拒绝旧候选',async()=>{
  const previous=store.load(bid.id).bid
  selection.sourceIds=[bid.sources.find(s=>s.kind==='补遗答疑').id]
  const childJob=await ready();assert.equal(childJob.state,'succeeded')
  bid=save(updateBidSource(bid,source.id,{version:'原版新增更正'}))
  assert.throws(()=>service.apply(bid.id,childJob.id,[0]),/变化/)
  bid=save({...previous,revision:bid.revision});selection.sourceIds=[source.id]
})
job=await ready()
await check('无指定影响范围的补遗也要求重新生成',()=>{
  bid=save(addSource(bid,{...source,id:bidId(),name:'全标段补遗',kind:'补遗答疑',supersedes:''}))
  assert.throws(()=>service.apply(bid.id,job.id,[0]),/补遗/)
})
job=await ready()
await check('仅修改未引用段落也显示过期、采纳拒绝',()=>{
  bid=save({...bid,sources:bid.sources.map(s=>s.id===source.id?{...s,blocks:[{...s.blocks[0],text:'条件已经改变。'},s.blocks[1]]}:s)})
  assert.match(service.list(bid.id).find(j=>j.id===job.id).reviewIssue,/正文/)
  assert.throws(()=>service.apply(bid.id,job.id,[0]),/变化/)
})
job=await ready()
await check('旧任务没有资料版本快照保留可看、不能推定一致采纳',()=>{
  const old={...job,id:bidId()};delete old.sourceContext;delete old.reviewIssue
  writeFileSync(join(output,'bid-ai/jobs',old.id+'.json'),JSON.stringify(old))
  assert.match(service.list(bid.id).find(j=>j.id===old.id).reviewIssue,/旧任务/)
  assert.throws(()=>service.apply(bid.id,old.id,[0]),/旧任务/)
  assert.ok(JSON.parse(readFileSync(join(output,'bid-ai/jobs',old.id+'.json'),'utf8')).result)
})
await check('原件被替换时列表提示不可核对、采纳拒绝',()=>{
  const asset=store.assetPath(source.asset),bytes=readFileSync(asset)
  writeFileSync(asset,'损坏测试原件')
  assert.match(service.list(bid.id).find(j=>j.id===job.id).reviewIssue,/原件/)
  assert.throws(()=>service.apply(bid.id,job.id,[0]),/原件/)
  writeFileSync(asset,bytes)
})
mode='body-wait'
await check('已接收HTTP响应后取消正文读取仍结束、不采纳残片',async()=>{
  const running=start();await pause(10);service.cancel(bid.id,running.id)
  const completed=await finish(running);assert.equal(completed.state,'canceled');assert.equal(completed.result,null)
  assert.match(completed.error,/计费/);assert.equal(streamCanceled,1)
})
await check('正文流停滞超时可恢复、不等待流取消的完成',async()=>{
  const completed=await finish(start());assert.equal(completed.state,'failed');assert.match(completed.error,/超时/)
  assert.equal(streamCanceled,2);assert.equal(completed.result,null)
})
mode='success'
await check('取消及超时后可手动重新准备，成功结果重启保留',async()=>{
  const completed=await ready();assert.equal(completed.state,'succeeded')
  const previous=calls,restarted=createBidAiService(output,vault,fetcher)
  assert.equal(restarted.list(bid.id).find(j=>j.id===completed.id).state,'succeeded');assert.equal(calls,previous)
  assert.equal(store.load(bid.id).bid.sections[0].body,'人工正文，始终保留')
})
const report={status:'BID_CANDIDATE_REVIEW_OK',checks,calls,realNetworkRequests:0,output}
writeFileSync(join(output,'result.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report))
