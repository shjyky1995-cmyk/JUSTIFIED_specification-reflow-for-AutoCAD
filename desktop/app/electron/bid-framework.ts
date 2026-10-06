import { app, BrowserWindow, ipcMain } from 'electron'
import { spawn, type ChildProcess } from 'node:child_process'
import { existsSync, mkdirSync } from 'node:fs'
import { join, resolve } from 'node:path'

export function registerBidFramework(appRoot:string,dataRoot:string){
  let child:ChildProcess|null=null
  ipcMain.handle('bid-framework-open',async event=>{
    if(child&&!child.killed)return {success:true,message:'投标模块已经打开，请切换到投标窗口。'}
    const bundled=join(process.resourcesPath,'bidding-framework'),local=resolve(appRoot,'../bidding-framework/client')
    const root=app.isPackaged&&existsSync(bundled)?bundled:local
    const command=join(root,'EngiSpace-Bidding.exe'),entry=existsSync(command)?join(root,'resources/app/electron/engispace-entry.cjs'):join(root,'electron/engispace-entry.cjs')
    const dev=join(root,'node_modules/electron/dist/electron.exe')
    if(!existsSync(entry)||(!existsSync(command)&&!existsSync(dev)))throw new Error('未找到投标框架程序，请使用完整的框架试用包。')
    const parent=BrowserWindow.fromWebContents(event.sender),directory=join(dataRoot,'bidding-framework')
    mkdirSync(directory,{recursive:true})
    const env:NodeJS.ProcessEnv={...process.env,ENGISPACE_BIDDING_DATA_ROOT:directory};delete env.ELECTRON_RUN_AS_NODE
    const launched=spawn(existsSync(command)?command:dev,existsSync(command)?[]:[root],{cwd:root,windowsHide:true,env,stdio:['ignore','pipe','pipe']})
    child=launched
    launched.stderr?.resume()
    launched.once('error',()=>{if(child===launched)child=null;parent?.show()})
    launched.once('exit',()=>{if(child===launched)child=null;if(parent&&!parent.isDestroyed()){parent.show();parent.focus()}})
    return await new Promise<{success:boolean;message:string}>(resolveResult=>{
      let settled=false,output=''
      const finish=(success:boolean,message:string)=>{if(settled)return;settled=true;clearTimeout(timer);resolveResult({success,message})}
      const timer=setTimeout(()=>finish(false,'投标模块启动较慢，请查看投标窗口；原工作台仍保留。'),30_000)
      launched.stdout?.on('data',bytes=>{output=(output+bytes.toString()).slice(-4000);if(output.includes('ENGISPACE_BIDDING_READY')){if(parent&&!parent.isDestroyed())parent.hide();finish(true,'投标工作台已打开。')}})
      launched.once('error',()=>finish(false,'投标模块启动失败，请检查完整程序目录。'))
      launched.once('exit',code=>finish(false,`投标模块已退出（${code??'未知'}），旧稿仍保留。`))
    })
  })
}
