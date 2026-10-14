import assert from 'node:assert/strict'
import { mkdtempSync, writeFileSync, readFileSync, unlinkSync, existsSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { spawnSync } from 'node:child_process'
import { newBid, newRequirement, newEvidence, parseBid, reviewBid, bidDocument, addSource, invalidateResponses, bidId } from '../desktop/app/src/features/bidding/model.ts'
import { createBidStore } from '../desktop/app/src/features/bidding/store.ts'

const base = process.env.DSS_TEST_ROOT
assert.ok(base && !/^c:/i.test(base), '测试输出必须指定到非 C 盘')
const root = mkdtempSync(join(base, 'bid-'))
const store = createBidStore(root)
let checks = 0
function check(name, fn) { fn(); checks++; console.log('PASS ' + name) }
function persist(bid) { const result = store.save(bid); assert.equal(result.ok, true, result.error); return store.load(bid.id).bid }
const worker = process.env.DSS_WORKER_EXE
const dotnet = process.env.DSS_DOTNET_EXE
function call(request) {
  const args = worker ? [] : [resolve('desktop/worker/bin/Debug/net10.0/DocxWorkbench.Worker.dll')]
  const result = spawnSync(worker || dotnet, args, { input: JSON.stringify(request), encoding:'utf8', windowsHide:true, maxBuffer:8_000_000 })
  assert.ifError(result.error)
  try { return JSON.parse(result.stdout) } catch { throw new Error(result.stderr || result.stdout) }
}
for (const [type,count] of [['survey',11],['design',11],['combined',17]]) check(type+'空模板和独立保存',()=>{
  const b = newBid(type); assert.equal(b.sections.length,count); assert.ok(b.sections.every(s=>!s.body&&!s.reviewed)); const saved=persist(b); assert.equal(saved.revision,1); assert.ok(reviewBid(saved).length>0); assert.throws(()=>bidDocument(saved,'reviewed'))
})
let bid = newBid('design','脱敏投标流程验证')
const sourcePath = join(root,'招标资料.txt'); writeFileSync(sourcePath,'项目负责人应提供资格证明。按约定提交成果。')
const source = store.importAsset(sourcePath); source.confirmed=true
bid=addSource(bid,source)
bid.project={name:'脱敏工程',number:'TEST-01',owner:'测试招标人',location:'测试地点',linkedId:''}
Object.assign(bid,{lot:'一标段',bidder:'测试单位',deadline:'2099-12-20T09:00',period:'30日',lead:'测试负责人',quote:'12345.67',taxNote:'测试含税口径'})
bid.sections.forEach(s=>{s.body='脱敏测试正文，不作为工程结论。';s.reviewed=true})
bid.sections[0].tables=[{id:bidId(),rows:[['成果','份数'],['测试成果','2']]}]
const evidence={...newEvidence(),title:'测试证明',holder:'测试负责人',scope:'测试标段',noExpiry:true,sourceId:source.id,verified:true}
bid.evidence=[evidence]
const requirement={...newRequirement(),title:'负责人资格',kind:'资格',sourceId:source.id,location:'文本第1句',excerpt:'项目负责人应提供资格证明。',confirmed:true,sectionIds:[bid.sections[0].id],evidenceIds:[evidence.id],responseReviewed:true}
bid.requirements=[requirement];bid.requirementsReviewed=true
check('已核对稿准入与失效触发',()=>{
  assert.equal(reviewBid(bid).length,0)
  assert.equal(bidDocument(bid,'reviewed').sections.length,11)
  const expired=structuredClone(bid);expired.evidence[0].validUntil='2000-01-01';expired.evidence[0].noExpiry=false;assert.ok(reviewBid(expired).some(i=>i.message.includes('过期')))
  assert.ok(reviewBid(invalidateResponses(bid,bid.sections[0].id)).length)
  const supplemented=addSource(bid,{...source,id:bidId(),kind:'补遗答疑',supersedes:source.id});assert.equal(supplemented.sources.length,2);assert.ok(supplemented.requirements.every(r=>!r.confirmed&&!r.responseReviewed));assert.ok(supplemented.sections.every(s=>!s.reviewed))
})
check('拒绝坏日期/未知版本/空关联/越界路径/不齐表格',()=>{
  for(const mutate of [b=>b.schemaVersion=2,b=>b.deadline='2099-02-30',b=>b.requirements[0].sectionIds=[''],b=>b.requirements[0].sectionIds=['missing'],b=>b.sources[0].asset='../secret.txt',b=>b.sections[0].tables[0].rows.push(['单列'])]){const b=structuredClone(bid);mutate(b);assert.throws(()=>parseBid(b))}
  assert.throws(()=>store.load('../outside'))
})
bid=persist(bid)
check('版本冲突拒绝覆盖',()=>{const old=structuredClone(bid);bid.title='最新保存';bid=persist(bid);assert.equal(store.save(old).ok,false);assert.equal(store.load(bid.id).bid.title,'最新保存')})
check('坏稿与缺失稿回退上一版本',()=>{
  const b=persist(newBid('survey'));const changed=persist({...b,title:'第二版'});const path=join(store.root,changed.id+'.json');writeFileSync(path,'{坏JSON');assert.equal(store.load(b.id).recovered,true);assert.equal(store.load(b.id).bid.title,b.title);unlinkSync(path);assert.equal(store.load(b.id).recovered,true)
})
check('备份含附件/恢复新增副本/坏哈希拒绝',()=>{
  const json=store.backup(bid.id);const restored=store.restore(json);assert.notEqual(restored.id,bid.id);assert.equal(restored.sources[0].hash,source.hash);assert.equal(store.assetBytes(restored.sources[0]).toString(),readFileSync(sourcePath,'utf8'))
  const bad=JSON.parse(json);bad.files[source.asset]=Buffer.from('被篡改').toString('base64');assert.throws(()=>store.restore(JSON.stringify(bad)))
  const copy=store.duplicate(bid.id);assert.notEqual(copy.id,bid.id);assert.equal(copy.sections[0].body,bid.sections[0].body)
})
check('真实DOCX导出/读回文字与表格/禁止覆盖',()=>{
  const path=join(root,'已核对稿.docx');const doc=bidDocument(bid,'reviewed');const exported=call({operation:'bid-export',path,bidDocument:doc});assert.equal(exported.success,true,exported.message);assert.equal(exported.blocks,11);assert.ok(existsSync(path))
  const before=readFileSync(path);assert.equal(call({operation:'bid-export',path,bidDocument:doc}).success,false);assert.deepEqual(readFileSync(path),before)
  const read=call({operation:'bid-extract',path});assert.equal(read.success,true,read.message);assert.ok(read.sourceBlocks.some(b=>b.text==='测试成果'&&b.location.startsWith('表 ')));assert.ok(read.sourceBlocks.some(b=>b.text.includes('12345.67')))
  const tableOnly=structuredClone(bid);tableOnly.sections[0].body='';assert.equal(reviewBid(tableOnly).length,0);assert.ok(!JSON.stringify(bidDocument(tableOnly,'reviewed').sections[0]).includes('待填写'))
  const draft=call({operation:'bid-export',path:join(root,'空草稿.docx'),bidDocument:bidDocument(newBid('combined'),'draft')});assert.equal(draft.success,true,draft.message)
  writeFileSync(join(root,'坏.docx'),'invalid');assert.equal(call({operation:'bid-extract',path:join(root,'坏.docx')}).success,false)
})
check('缺失或被篡改附件禁止备份',()=>{const path=store.assetPath(source.asset);const bytes=readFileSync(path);writeFileSync(path,'bad');assert.throws(()=>store.backup(bid.id));unlinkSync(path);assert.throws(()=>store.backup(bid.id));writeFileSync(path,bytes)})
writeFileSync(join(root,'result.json'),JSON.stringify({status:'BIDDING_OK',checks,root},null,2))
console.log(JSON.stringify({status:'BIDDING_OK',checks,root}))
