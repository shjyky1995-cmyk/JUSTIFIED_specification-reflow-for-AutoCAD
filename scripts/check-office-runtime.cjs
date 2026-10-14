// 从全新解压成品调用包内模型、资料库、独立DOCX进程；无需开发SDK。
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const {spawnSync} = require('node:child_process');
const {pathToFileURL} = require('node:url');
async function check() {
  assert.ok(process.versions.electron);
  const client = path.resolve(process.argv[2]), output = path.resolve(process.argv[3]);
  fs.mkdirSync(output, {recursive:true});
  const modulePath = name => pathToFileURL(path.join(client,'resources/app/dist-electron/src/shared',name+'.js'));
  const model = await import(modulePath('model'));
  const {createStore} = await import(modulePath('store'));
  const {assembleNote} = await import(modulePath('assembly'));
  const worker = path.join(client,'resources/worker/DocxWorkbench.Worker.exe');
  const call = request => {
    const result = spawnSync(worker, [], {input:JSON.stringify(request),encoding:'utf8',windowsHide:true,timeout:30000});
    assert.ifError(result.error);
    assert.equal(result.status,0,result.stderr||result.stdout);
    const data = JSON.parse(result.stdout); assert.equal(data.success,true); return data;
  };
  const store = createStore(path.join(output,'data'),path.join(client,'resources/content-library/catalog.json'));
  const catalog = store.loadCatalog(); assert.equal(catalog.layouts.length,7);
  let exported=0;
  for(const discipline of model.DISCIPLINES) {
    const note=model.createNote(discipline.code,model.customTemplateId(discipline.code),{name:'跨电脑脱敏测试',number:'TEST',owner:'测试单位',location:'测试地点'});
    note.sections=[{id:'custom',title:'说明',body:'跨电脑运行检查正文，可在Word编辑。',custom:true}];
    assert.equal(store.saveNote(note).ok,true);
    const docx=path.join(output,discipline.code+'.docx');
    call(model.toExportRequest(store.loadNote(note.id),docx));
    call({operation:'inspect',path:docx}); exported++;
  }
  for(const layout of catalog.layouts) {
    const discipline=layout.templateId.includes('struct')?'structural':layout.templateId.includes('arch')?'architecture':layout.templateId.includes('plumb')?'plumbing':layout.templateId.includes('elec')?'electrical':'hvac';
    const note=assembleNote(model.createNote(discipline,layout.templateId,{name:'跨电脑模板核对',number:'TEST',owner:'测试单位',location:'测试地点'}),layout.templateId,catalog).note;
    const docx=path.join(output,layout.templateId+'.docx');
    // 旧通用inspect仍拒绝表格；generate的TableDocumentResult已独立读取OpenXML验证。
    const result=call(model.toExportRequest(note,docx));
    assert.equal(result.headings.length,layout.sections.length+1);
    assert.ok(result.blocks>layout.sections.length); exported++;
  }
  const report={status:'OFFICE_PACKAGED_RUNTIME_OK',electron:process.versions.electron,disciplines:6,templates:7,docxGenerated:exported,plainDocxInspected:6,templateOpenXmlChecked:7};
  fs.writeFileSync(path.join(output,'runtime-result.json'),JSON.stringify(report,null,2)); console.log(JSON.stringify(report));
}
check().catch(error=>{console.error(error);process.exitCode=1});
