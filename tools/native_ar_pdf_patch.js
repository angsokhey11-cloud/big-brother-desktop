// Installed Desktop only. PDF preparation and permission checks stay in the
// original A/R export; transfer only its finished File through the existing bridge.
function nativePdfAvailable(){
 return location.origin==='https://app.bigbrother.test'&&!!window.chrome?.webview?.postMessage;
}
function nativePdfRequest(token,op,values={}){
 return new Promise((resolve,reject)=>{
  const bridge=window.chrome.webview,request=crypto.randomUUID();
  const clean=()=>{clearTimeout(timer);bridge.removeEventListener('message',receive)};
  const receive=e=>{
   const m=e.data;
   if(m?.type!=='bbNativeAlbumAckV1'||m.request!==request)return;
   clean();if(m.ok)resolve();else reject(Error(m.message||'Windows could not prepare this PDF.'));
  };
  const timer=setTimeout(()=>{clean();reject(Error('Windows PDF request timed out. Retry or use Save PDF + Invoices.'))},15000);
  bridge.addEventListener('message',receive);
  try{bridge.postMessage({type:'bbNativeAlbumV1',request,token,op,...values})}
  catch(error){clean();reject(error)}
 });
}
async function transferNativePdf(file,isCurrent){
 if(file.type!=='application/pdf'||!file.size)throw Error('The PDF is not ready.');
 if(file.size>65*1024*1024)throw Error('PDF exceeds the 65 MB native transfer limit. Use Save PDF + Invoices instead.');
 const token=crypto.randomUUID(),chunkSize=192*1024;
 let started=false,finished=false;
 const check=()=>{if(!isCurrent())throw Error('Summary changed or closed. PDF sharing canceled.')};
 try{
  check();
  await nativePdfRequest(token,'begin',{files:[{size:file.size,extension:'.pdf',name:file.name}]});started=true;
  for(let offset=0;offset<file.size;offset+=chunkSize){
   check();
   const bytes=new Uint8Array(await file.slice(offset,offset+chunkSize).arrayBuffer());
   let binary='';
   for(let p=0;p<bytes.length;p+=8192)binary+=String.fromCharCode(...bytes.subarray(p,p+8192));
   await nativePdfRequest(token,'chunk',{index:0,offset,data:btoa(binary)});
  }
  check();
  await nativePdfRequest(token,'finish');finished=true;
 }finally{
  if(started&&!finished)try{await nativePdfRequest(token,'abort')}catch(_){}
 }
}
