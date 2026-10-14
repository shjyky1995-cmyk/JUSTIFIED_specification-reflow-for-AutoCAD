import { rebuild } from '@electron/rebuild'
import { execFileSync } from 'node:child_process'
import { resolve } from 'node:path'
const runtime=resolve(process.argv[2])
const cache=resolve(process.argv[3])
const info=JSON.parse(execFileSync(runtime,['-e','console.log(JSON.stringify({version:process.versions.electron,abi:Number(process.versions.modules)}))'],{encoding:'utf8',windowsHide:true,env:{...process.env,ELECTRON_RUN_AS_NODE:'1'}}).trim())
if(!info.version||!Number.isInteger(info.abi))throw Error('无法取得实际Electron ABI')
await rebuild({buildPath:process.cwd(),electronVersion:info.version,forceABI:info.abi,arch:'x64',onlyModules:['better-sqlite3'],force:true,cachePath:cache})
console.log(JSON.stringify({status:'BID_FRAMEWORK_NATIVE_READY',...info}))
