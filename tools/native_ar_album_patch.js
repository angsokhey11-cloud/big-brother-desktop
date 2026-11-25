// Desktop bundle only; inserted into existing A/R album closure.
// Retain the existing mobile share engine and explicit folder fallback.
function nativeAlbumAvailable(){
 return location.origin==='https://app.bigbrother.test' &&
  !!window.chrome?.webview?.postMessage;
}
function nativeAlbumRequest(token,op,values={}){
 return new Promise((resolve,reject)=>{
  const bridge=window.chrome.webview,request=crypto.randomUUID();
  const clean=()=>{clearTimeout(timer);bridge.removeEventListener('message',receive)};
  const receive=e=>{
   const m=e.data;
   if(m?.type!=='bbNativeAlbumAckV1'||m.request!==request)return;
   clean();if(m.ok)resolve();else reject(Error(m.message||'Windows could not prepare this album.'));
  };
  const timer=setTimeout(()=>{clean();reject(Error('Windows album request timed out. Retry or use folder fallback.'))},15000);
  bridge.addEventListener('message',receive);
  try{bridge.postMessage({type:'bbNativeAlbumV1',request,token,op,...values})}
  catch(error){clean();reject(error)}
 });
}
async function prepareNativeAlbum(index){
 const pkg=valid(),plan=sharePlan;
 if(!pkg||!plan||plan.mode!=='desktop-bulk'||plan.signature!==pkg.signature||plan.busy)return;
 const files=plan.groups[index];
 if(!files?.length||files.length>ALBUM_SIZE)return;
 const token=crypto.randomUUID(),chunkSize=192*1024;
 let started=false,finished=false;
 plan.busy=true;showDesktopBatchPanel();
 try{
  const manifest=files.map((file,i)=>({
   size:file.size,
   extension:(file.name.match(/\.[a-z0-9]{2,5}$/i)||[''])[0].toLowerCase(),
   summary:index===0&&i===0
  }));
  await nativeAlbumRequest(token,'begin',{files:manifest});started=true;
  for(let i=0;i<files.length;i++){
   const file=files[i];
   indicator('Preparing Windows album · picture '+(i+1)+' of '+files.length+'…');
   for(let offset=0;offset<file.size;offset+=chunkSize){
    if(sharePlan!==plan||!visible())throw Error('Summary closed. Album canceled.');
    const bytes=new Uint8Array(await file.slice(offset,offset+chunkSize).arrayBuffer());
    let binary='';
    for(let p=0;p<bytes.length;p+=8192)binary+=String.fromCharCode(...bytes.subarray(p,p+8192));
    await nativeAlbumRequest(token,'chunk',{index:i,offset,data:btoa(binary)});
   }
  }
  if(sharePlan!==plan||!visible())throw Error('Summary closed. Album canceled.');
  await nativeAlbumRequest(token,'finish');finished=true;
  if(sharePlan===plan&&visible())indicator(files.length+' photos ready in our Browser. Select the correct chat, click Attach photos above Telegram, then Telegram’s Photo or Video. Check preview order before sending.');
 }catch(error){
  if(started&&!finished)try{await nativeAlbumRequest(token,'abort')}catch(_){}
  if(sharePlan===plan&&visible())indicator(error?.message||'Could not prepare Windows album. Use the folder fallback.',true);
 }finally{
  plan.busy=false;
  if(sharePlan===plan&&visible())showDesktopBatchPanel();
 }
}
