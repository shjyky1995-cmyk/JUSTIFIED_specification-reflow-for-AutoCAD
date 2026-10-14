// 两个Electron版本使用稳定JSON传输；保留DOCX/图片预览的二进制和undefined。
function encode(value,seen=new WeakSet()) {
  if(value===undefined)return ['u'];
  if(value===null||['string','boolean'].includes(typeof value))return ['p',value];
  if(typeof value==='number')return Number.isFinite(value)?['p',value]:['n',String(value)];
  if(typeof value==='bigint')return ['i',String(value)];
  if(value instanceof ArrayBuffer)return ['b','ArrayBuffer',Buffer.from(value).toString('base64')];
  if(ArrayBuffer.isView(value))return ['b',Buffer.isBuffer(value)?'Buffer':value.constructor.name,Buffer.from(value.buffer,value.byteOffset,value.byteLength).toString('base64')];
  if(value instanceof Date)return ['d',value.toISOString()];
  if(typeof value!=='object')throw Error('投标通信不接受函数或执行对象。');
  if(seen.has(value))throw Error('投标通信数据存在循环引用。');seen.add(value);
  try{return Array.isArray(value)?['a',value.map(item=>encode(item,seen))]:['o',Object.entries(value).map(([key,item])=>[key,encode(item,seen)])];}finally{seen.delete(value);}
}
function decode(value) {
  if(!Array.isArray(value))throw Error('投标通信格式无效。');
  const [tag,payload,bytes]=value;
  if(tag==='u')return undefined;
  if(tag==='p')return payload;
  if(tag==='n')return Number(payload);
  if(tag==='i')return BigInt(payload);
  if(tag==='d')return new Date(payload);
  if(tag==='a')return payload.map(decode);
  if(tag==='o')return Object.fromEntries(payload.map(([key,item])=>[key,decode(item)]));
  if(tag==='b'){
    const buffer=Buffer.from(bytes,'base64');
    if(payload==='Buffer')return buffer;
    const array=buffer.buffer.slice(buffer.byteOffset,buffer.byteOffset+buffer.byteLength);
    if(payload==='ArrayBuffer')return array;
    const types={Uint8Array,Int8Array,Uint16Array,Int16Array,Uint32Array,Int32Array,Float32Array,Float64Array,Uint8ClampedArray,DataView};
    if(!Object.hasOwn(types,payload))throw Error('不支持的投标二进制类型。');return new types[payload](array);
  }
  throw Error('不支持的投标通信类型。');
}
module.exports={encode,decode};
