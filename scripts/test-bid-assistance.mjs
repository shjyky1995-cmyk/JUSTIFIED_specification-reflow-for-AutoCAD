import assert from 'node:assert/strict'
import {mkdtempSync,writeFileSync,readFileSync,readdirSync} from 'node:fs'
import {join} from 'node:path'
import {createBidStore} from '../desktop/app/src/features/bidding/store.ts'
import {newBid,bidId} from '../desktop/app/src/features/bidding/model.ts'
import {requirementCandidates,adoptCandidates,suggestResponses} from '../desktop/app/src/features/bidding/assistance.ts'
import {createBidAiService} from '../desktop/app/electron/bid-ai.ts'
const base=process.env.DSS_TEST_ROOT;assert.ok(base&&!/^c:/i.test(base))
const root=mkdtempSync(join(base,'assistance-')),store=createBidStore(root)
const fixture=join(root,'脱敏条款.txt');writeFileSync(fixture,'脱敏测试原件，不联网')
const src=store.importAsset(fixture);src.blocks=[{location:'第1段',text:'项目负责人须提供注册证书；如联合体投标，应明确分工。'},{location:'第2段',text:'不要求提供类似业绩，评分办法另列。'},{location:'第3段',text:'设计成果应包括图纸，提交期限为30日。'},{location:'第4段',text:'普通说明文字。'}]
let b=newBid('design');b.sources=[src];b.sections[0].body='人工正文必须保留'
function save(bid){const r=store.save(bid);assert.equal(r.ok,true,r.error);return store.load(bid.id).bid}
b=save(b)
let checks=0
function check(name,fn){fn();checks++;console.log('PASS '+name)}
check('本地候选保留条件与否定、人工状态和去重',()=>{
  const report=requirementCandidates(b,src.id);assert.equal(report.candidates.length,3)
  let next=adoptCandidates(b,src.id,src.hash,[0,1]);assert.equal(next.requirements.length,2);assert.ok(next.requirements[1].excerpt.startsWith('不要求'));assert.ok(next.requirements.every(r=>!r.confirmed&&!r.responseReviewed&&!r.mandatory&&!r.score));next=adoptCandidates(next,src.id,src.hash,[0,1]);assert.equal(next.requirements.length,2);assert.ok(suggestResponses(next,next.requirements[0]).some(s=>s.title.includes('人员')))
  assert.throws(()=>adoptCandidates(next,src.id,'wrong',[0]));const proof=structuredClone(b);proof.sources[0].kind='证明材料';assert.equal(requirementCandidates(proof,src.id).candidates.length,0)
})
// 测试专用可逆vault；成品使用Electron safeStorage，不使用此实现。
const vault={available:()=>true,encrypt:s=>Buffer.from([...s].reverse().join('')),decrypt:b=>[...b.toString()].reverse().join('')}
let calls=0,mode='success',sent=null
const fetcher=async(url,options)=>{
  calls++;sent=JSON.parse(options.body);assert.equal(url,'https://api.deepseek.com/chat/completions');assert.equal(options.redirect,'error');assert.equal(options.headers.Authorization,'Bearer TEST-ONLY-NOT-A-REAL-KEY')
  if(mode==='network')throw new Error('secret-network-details')
  if(mode==='wait')return new Promise((_r,reject)=>{options.signal.addEventListener('abort',()=>reject(new Error('abort')),{once:true})})
  if(mode==='limit')return new Response('',{status:429})
  const reference={sourceId:src.id,index:0,quote:mode==='bad-reference'?'编造引用':src.blocks[0].text}
  const system=sent.messages[0].content
  const result=system.includes('提取招标要求')?{requirements:[{title:'负责人资格',kind:'资格',reference}]}:system.includes('章节建议')?{outline:[{title:'人员安排',reference}]}:{chapter:{body:'依据资料安排人员，具体配置【待核定】。',references:[reference]}}
  return new Response(JSON.stringify({choices:[{finish_reason:mode==='truncated'?'length':'stop',message:{content:mode==='empty'?'':JSON.stringify(result)}}],usage:{prompt_tokens:101,completion_tokens:25,total_tokens:126}}))
}
const ai=createBidAiService(root,vault,fetcher,60)
const selection={bidId:b.id,kind:'requirements',sourceIds:[src.id],sectionId:''}
check('密钥设置及逐次发送确认',()=>{
  assert.throws(()=>ai.prepare(selection));ai.configure('deepseek-flash','TEST-ONLY-NOT-A-REAL-KEY');assert.equal(ai.settings().hasKey,true)
  assert.ok(!readFileSync(join(root,'bid-ai/settings.json'),'utf8').includes('TEST-ONLY-NOT-A-REAL-KEY'))
  const p=ai.prepare(selection);assert.equal(calls,0);assert.throws(()=>ai.start(p.token,false));assert.equal(calls,0)
})
async function finish(job){for(let i=0;i<100;i++){const j=ai.list(b.id).find(j=>j.id===job.id);if(j.state!=='running')return j;await new Promise(r=>setTimeout(r,10))}throw Error('test timeout')}
async function run(kind='requirements'){const p=ai.prepare({...selection,kind,sectionId:kind==='chapter'?b.sections[0].id:''});return finish(ai.start(p.token,true))}
let job=await run()
check('API请求字段/用量/候选不自动改正文',()=>{assert.equal(job.state,'succeeded');assert.deepEqual(job.usage,{input:101,output:25,total:126});assert.equal(sent.response_format.type,'json_object');assert.equal(sent.thinking.type,'disabled');assert.equal(store.load(b.id).bid.requirements.length,0);assert.equal(store.load(b.id).bid.sections[0].body,'人工正文必须保留')})
check('采纳后仍待人工确认、重复采纳幂等及跨标段拒绝',()=>{b=ai.apply(b.id,job.id,[0]);assert.equal(b.requirements.length,1);assert.equal(b.requirements[0].confirmed,false);assert.equal(ai.apply(b.id,job.id,[0]).requirements.length,1);const other=save(newBid('survey'));assert.throws(()=>ai.apply(other.id,job.id,[0]))})
job=await run('chapter')
check('章节新增副本不覆盖人工正文',()=>{assert.equal(job.state,'succeeded');const old=b.sections.length;b=ai.apply(b.id,job.id,[0]);assert.equal(b.sections.length,old+1);assert.equal(b.sections[0].body,'人工正文必须保留');assert.ok(b.sections.at(-1).body.includes('待核定：AI建议'));assert.equal(b.sections.at(-1).reviewed,false)})
job=await run('outline');check('目录候选有来源且可采纳',()=>{assert.equal(job.state,'succeeded');b=ai.apply(b.id,job.id,[0]);assert.equal(b.sections.at(-1).title,'人员安排')})
check('发送前内容变化需要重新确认',()=>{const p=ai.prepare(selection);b=save({...b,title:'变更标题'});assert.throws(()=>ai.start(p.token,true),/变化/)} )
for(const scenario of ['bad-reference','truncated','empty','limit','network']){mode=scenario;job=await run();check('异常不采纳 '+scenario,()=>{assert.equal(job.state,'failed');assert.equal(job.result,null);assert.ok(!job.error.includes('secret-network-details'))})}
mode='wait';let p=ai.prepare(selection);job=ai.start(p.token,true);ai.cancel(b.id,job.id);job=await finish(job);check('取消不自动重试',()=>assert.equal(job.state,'canceled'))
p=ai.prepare(selection);job=await finish(ai.start(p.token,true));check('超时可重试且不重复请求',()=>{assert.equal(job.state,'failed');assert.match(job.error,/超时/)} )
mode='success';job=await run();b.sources[0].blocks[0].text+='补遗变化';b=save(b);check('采纳前资料变化拒绝',()=>assert.throws(()=>ai.apply(b.id,job.id,[0]),/变化/))
const fake={...job,id:bidId(),state:'running',result:null};writeFileSync(join(root,'bid-ai/jobs',fake.id+'.json'),JSON.stringify(fake));const previousCalls=calls
const restarted=createBidAiService(root,vault,fetcher)
check('重启恢复成功结果并标记中断、不自动发送',()=>{assert.equal(restarted.list(b.id).find(j=>j.id===fake.id).state,'interrupted');assert.equal(calls,previousCalls);assert.ok(restarted.list(b.id).some(j=>j.state==='succeeded'));restarted.remove(b.id,fake.id);assert.ok(!restarted.list(b.id).some(j=>j.id===fake.id))})
check('备份不含密钥或AI任务日志',()=>{const backup=store.backup(b.id);assert.ok(!backup.includes('TEST-ONLY-NOT-A-REAL-KEY'));assert.ok(!backup.includes('prompt_tokens'))})
const report={status:'BID_ASSISTANCE_OK',checks,calls,realNetworkRequests:0,root};writeFileSync(join(root,'result.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report))
