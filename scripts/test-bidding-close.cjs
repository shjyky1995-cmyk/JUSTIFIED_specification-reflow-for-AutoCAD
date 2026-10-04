// 使用成品中的真实主进程/页面；测试窗口保持隐藏，验证关窗保存链。
const {app}=require('electron')
const {join}=require('node:path')
const {mkdtempSync,readFileSync,readdirSync,writeFileSync}=require('node:fs')
const {pathToFileURL}=require('node:url')
const assert=require('node:assert/strict')
const base=process.env.DSS_TEST_ROOT, bundle=process.env.DSS_BID_BUNDLE
assert.ok(base&&!/^c:/i.test(base)&&bundle)
const output=mkdtempSync(join(base,'bid-close-'))
process.env.DSS_USER_DATA_ROOT=join(output,'user')
process.env.DSS_WORKER_EXE=join(bundle,'client/resources/worker/DocxWorkbench.Worker.exe')
const pause=ms=>new Promise(r=>setTimeout(r,ms))
let finished=false
app.on('browser-window-created',(_event,win)=>{
  win.show=()=>{} // 在主进程注册 ready-to-show 之前禁止测试窗口显示。
  win.webContents.once('did-finish-load',async()=>{
    try{
      await pause(250)
      const js=s=>win.webContents.executeJavaScript(s)
      await js(`[...document.querySelectorAll('.entry-card')].find(b=>b.textContent.includes('勘察设计投标')).click()`);await pause(150)
      await js(`document.querySelector('.bid-type-card').click()`);await pause(200)
      assert.equal(app.getPath('userData'),process.env.DSS_USER_DATA_ROOT)
      assert.ok(app.getPath('temp').startsWith(output))
      await js(`(()=>{const input=[...document.querySelectorAll('.bid-field')].find(e=>e.querySelector('span')?.textContent==='项目名称').querySelector('input');Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(input,'关窗前最后一次输入');input.dispatchEvent(new Event('input',{bubbles:true}))})()`)
      // 不等待600ms自动保存，立即由真实主进程执行关闭握手。
      win.once('closed',()=>{
        try{
          const dir=join(output,'user/data/bids'), name=readdirSync(dir).find(n=>n.endsWith('.json'))
          const bid=JSON.parse(readFileSync(join(dir,name),'utf8'))
          assert.equal(bid.project.name,'关窗前最后一次输入')
          const report={status:'BIDDING_CLOSE_OK',revision:bid.revision,output};writeFileSync(join(output,'result.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report));finished=true
        }catch(e){console.error(e);process.exitCode=1}
      })
      win.close()
    }catch(e){console.error(e);app.exit(1)}
  })
})
app.on('will-quit',()=>{if(!finished)process.exitCode=1})
import(pathToFileURL(join(bundle,'client/resources/app/dist-electron/electron/main.js')).href).catch(e=>{console.error(e);app.exit(1)})
