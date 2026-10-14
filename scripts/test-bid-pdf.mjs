import assert from 'node:assert/strict'
import { mkdtempSync, writeFileSync, readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { spawnSync } from 'node:child_process'
import { createBidStore } from '../desktop/app/src/features/bidding/store.ts'
import { newBid, newRequirement, newEvidence, updateBidSource, parseBid, bidId } from '../desktop/app/src/features/bidding/model.ts'
import { extractBidSource, extractBidText } from '../desktop/app/electron/bid-source.ts'
import { buildAiInput, validateAiResult, applyAiResult } from '../desktop/app/src/features/bidding/ai-model.ts'
import { requirementCandidates } from '../desktop/app/src/features/bidding/assistance.ts'
import { createBidAiService } from '../desktop/app/electron/bid-ai.ts'

const base=process.env.DSS_TEST_ROOT, fixtures=process.env.DSS_PDF_FIXTURES
assert.ok(base && !/^c:/i.test(base) && fixtures)
const output=mkdtempSync(join(base,'pdf-')),store=createBidStore(join(output,'data'))
const expected=JSON.parse(readFileSync(join(fixtures,'fixtures.json'),'utf8'))
const worker=process.env.DSS_WORKER_EXE
const dotnet=process.env.DSS_DOTNET_EXE
const invoke=request=>{
  const r=spawnSync(worker||dotnet,worker?[]:[resolve('desktop/worker/bin/Debug/net10.0/DocxWorkbench.Worker.dll')],{input:JSON.stringify(request),encoding:'utf8',windowsHide:true,maxBuffer:8_000_000,timeout:35_000})
  assert.ifError(r.error)
  return JSON.parse(r.stdout)
}
let checks=0
const check=async(name,fn)=>{await fn();checks++;console.log('PASS '+name)}
const importPdf=async name=>{const source=store.importAsset(join(fixtures,name));return extractBidSource(source,store.assetPath(source.asset),async request=>invoke(request))}
let source
await check('中文文字型PDF逐物理页提取、表格文字与否定完整',async()=>{
  source=await importPdf('text.pdf')
  for(let i=0;i<3;i++){
    const blocks=source.blocks.filter(b=>b.location.startsWith(`PDF物理页 ${i+1} /`))
    assert.ok(blocks.length)
    const text=blocks.map(b=>b.text).join('')
    for(const phrase of expected.expected[i])assert.ok(text.includes(phrase),phrase+' missing: '+text)
  }
  assert.equal(source.confirmed,false);assert.ok(source.warnings.some(w=>w.includes('不等于印刷页码')))
  assert.deepEqual(store.assetBytes(source),readFileSync(join(fixtures,'text.pdf')))
})
await check('纯扫描/混合/空白页明确报告，页序不压缩',async()=>{
  const scan=await importPdf('scan.pdf');assert.equal(scan.blocks.length,0);assert.ok(scan.warnings.some(w=>w.includes('未执行OCR')))
  const mixed=await importPdf('mixed.pdf');assert.ok(mixed.blocks.every(b=>b.location.startsWith('PDF物理页 1 /')));assert.ok(mixed.warnings.some(w=>w.includes('2、3')));assert.ok(mixed.warnings.some(w=>w.includes('图片内文字未识别')&&w.includes('1')))
})
await check('密码/损坏/超页数失败不保留部分摘录，原件可恢复',async()=>{
  for(const name of ['encrypted.pdf','damaged.pdf','overpages.pdf']){const s=await importPdf(name);assert.equal(s.blocks.length,0);assert.ok(s.warnings.some(w=>w.includes('失败')));assert.deepEqual(store.assetBytes(s),readFileSync(join(fixtures,name)))}
})
await check('长PDF页完整分块且末尾保留，不超过单项摘录上限',async()=>{
  const s=await importPdf('long-page.pdf');assert.ok(s.blocks.length>1);assert.ok(s.blocks.every(b=>b.text.length<=20_000));const text=s.blocks.map(b=>b.text).join('');for(const phrase of expected.longLines)assert.ok(text.includes(phrase));assert.ok(text.includes('FINAL_REQUIRED_END'))
})
await check('全文超限明确失败，不伪装前60万字符为完整资料',async()=>{
  const s=await importPdf('overtext.pdf');assert.equal(s.blocks.length,0);assert.ok(s.warnings.some(w=>w.includes('60万字符')))
})
await check('TXT超过旧5万限制后末尾仍存在，UTF16及坏编码明确处理',()=>{
  const text='开头要求\n'+'甲'.repeat(51_000)+'\n最后必须提供证书。'
  const report=extractBidText(Buffer.from(text));assert.ok(report.blocks.some(b=>b.text.includes('最后必须提供证书')));assert.ok(report.blocks.every(b=>b.text.length<=20_000));assert.ok(report.blocks.some(b=>b.location.startsWith('文本行 3 /')))
  const utf16=extractBidText(Buffer.concat([Buffer.from([255,254]),Buffer.from('证书应有效。','utf16le')]));assert.equal(utf16.blocks[0].text,'证书应有效。')
  assert.equal(extractBidText(Buffer.from([0x81,0x82])).blocks.length,0);assert.match(extractBidText(Buffer.from([0x81,0x82])).warnings[0],/编码/)
  assert.equal(extractBidText(Buffer.alloc(500_001)).blocks.length,0)
  assert.equal(extractBidText(Buffer.from('行\n'.repeat(3001))).blocks.length,0)
})
let bid=newBid('design');bid.sources=[source]
await check('PDF人工确认与要求、保存备份恢复、原件哈希保持',()=>{
  const block=source.blocks[0];bid.requirements=[{...newRequirement(),sourceId:source.id,location:block.location,excerpt:block.text}]
  assert.equal(store.save(bid).ok,true);bid=store.load(bid.id).bid
  const copy=store.restore(store.backup(bid.id));assert.equal(copy.requirements[0].location,block.location);assert.deepEqual(store.assetBytes(copy.sources[0]),store.assetBytes(source))
  assert.ok(requirementCandidates(bid,source.id).candidates.length)
})
await check('AI只选指定物理页、原段索引保留、跨页伪造引用拒绝',()=>{
  const selection={bidId:bid.id,kind:'requirements',sourceIds:[source.id],sectionId:'',pageRanges:{[source.id]:{from:2,to:2}}}
  const input=buildAiInput(bid,selection);assert.ok(input.sources[0].blocks.every(b=>b.location.startsWith('PDF物理页 2 /')));assert.ok(input.sources[0].blocks[0].index>0)
  const reference={sourceId:source.id,index:input.sources[0].blocks[0].index,quote:'设计方案评分最高10分'}
  validateAiResult({requirements:[{title:'方案评分',kind:'评分',reference}]},'requirements',input)
  assert.throws(()=>validateAiResult({requirements:[{title:'未发送内容',kind:'资格',reference:{sourceId:source.id,index:0,quote:'项目负责人'}}]},'requirements',input))
  for(const range of [{from:0,to:2},{from:2,to:1},{from:2.5,to:3},{from:1,to:501},null])assert.throws(()=>buildAiInput(bid,{...selection,pageRanges:{[source.id]:range}}))
  assert.throws(()=>buildAiInput(bid,{...selection,pageRanges:{unknown:{from:1,to:1}}}))
  assert.throws(()=>buildAiInput(bid,{...selection,pageRanges:{[source.id]:{from:4,to:4}}}),/没有可提取文字/)
})
await check('页范围任务重启/人工采纳保持范围且不改原正文',async()=>{
  let calls=0
  const vault={available:()=>true,encrypt:s=>Buffer.from(s),decrypt:b=>b.toString()}
  const mock=async(_url,options)=>{
    calls++;const input=JSON.parse(JSON.parse(options.body).messages[1].content);assert.equal(input.sources[0].blocks.length,source.blocks.filter(b=>b.location.startsWith('PDF物理页 2 /')).length)
    const b=input.sources[0].blocks[0];return new Response(JSON.stringify({choices:[{finish_reason:'stop',message:{content:JSON.stringify({requirements:[{title:'方案评分',kind:'评分',reference:{sourceId:source.id,index:b.index,quote:'设计方案评分最高10分'}}]})}}]}))
  }
  let service=createBidAiService(join(output,'data'),vault,mock);service.configure('deepseek-flash','TEST-ONLY')
  const p=service.prepare({bidId:bid.id,kind:'requirements',sourceIds:[source.id],sectionId:'',pageRanges:{[source.id]:{from:2,to:2}}});assert.ok(!p.preview.includes('注册证书'));assert.equal(calls,0)
  const job=service.start(p.token,true);let finished
  for(let i=0;i<100;i++){finished=service.list(bid.id).find(j=>j.id===job.id);if(finished.state!=='running')break;await new Promise(r=>setTimeout(r,10))}
  assert.equal(finished.state,'succeeded');assert.deepEqual(finished.pageRanges,{[source.id]:{from:2,to:2}})
  service=createBidAiService(join(output,'data'),vault,mock);bid=service.apply(bid.id,job.id,[0]);assert.equal(bid.requirements.at(-1).location,source.blocks.find(b=>b.location.startsWith('PDF物理页 2 /')).location);assert.equal(calls,1)
})
await check('资料版本/补遗目标更改，关联要求资信章节复核失效，其他资料保留',()=>{
  const b=structuredClone(bid),other={...source,id:bidId(),name:'独立资料.pdf'};b.sources.push(other)
  const a=b.sections[0].id,z=b.sections[1].id
  b.requirements=[{...newRequirement(),sourceId:source.id,confirmed:true,responseReviewed:true,sectionIds:[a]},{...newRequirement(),sourceId:other.id,confirmed:true,responseReviewed:true,sectionIds:[z]}]
  b.evidence=[{...newEvidence(),sourceId:source.id,verified:true},{...newEvidence(),sourceId:other.id,verified:true}];b.sections.forEach(s=>s.reviewed=true)
  const next=updateBidSource(b,source.id,{version:'2'});assert.equal(next.requirements[0].confirmed,false);assert.equal(next.requirements[0].responseReviewed,false);assert.equal(next.evidence[0].verified,false);assert.equal(next.sections[0].reviewed,false);assert.equal(next.sections[1].reviewed,true);assert.equal(next.requirements[1].confirmed,true);assert.equal(next.evidence[1].verified,true)
  assert.equal(updateBidSource(b,source.id,{confirmed:true}).requirements[0].confirmed,true)
  const supplement={...source,id:bidId(),kind:'补遗答疑',supersedes:source.id};b.sources.push(supplement);const changed=updateBidSource(b,supplement.id,{supersedes:other.id});assert.ok(changed.requirements.every(r=>!r.confirmed));parseBid(next)
})
await check('提取超时/失败不生成部分摘录，导入原件保留',async()=>{
  const s=await extractBidSource(source,store.assetPath(source.asset),async()=>{throw Error('simulated timeout')});assert.equal(s.blocks.length,0);assert.ok(s.warnings[0].includes('时限'));assert.deepEqual(store.assetBytes(s),store.assetBytes(source))
})
const report={status:'BID_PDF_OK',checks,output,realNetworkRequests:0};writeFileSync(join(output,'result.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report))
