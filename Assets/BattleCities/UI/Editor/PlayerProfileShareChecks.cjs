// Runs the actual WebGL sharing bridge without opening a share sheet or changing the clipboard.
const fs=require('fs'),vm=require('vm'),assert=require('assert/strict'),path=require('path');
const source=fs.readFileSync(path.resolve(__dirname,'../../../Plugins/WebGL/BattleCitiesProfile.jslib'),'utf8');
async function scenario(navigator,expected){
  const messages=[],library={};
  const context={navigator,LibraryManager:{library},mergeInto:Object.assign,UTF8ToString:value=>value,SendMessage:(...args)=>messages.push(args)};
  vm.runInNewContext(source,context);
  library.BattleCitiesProfileShare('Main Menu','Commander','https://battlecities.com/?playerProfile=ply-test');
  await new Promise(resolve=>setImmediate(resolve));
  assert.equal(messages.length,1);assert.deepEqual(messages[0],['Main Menu','OnProfileShareResult',expected]);
}
(async()=>{
  let copied;await scenario({clipboard:{writeText:async text=>{copied=text;}}},'copied');assert.equal(copied,'https://battlecities.com/?playerProfile=ply-test');
  let shared;await scenario({share:async payload=>{shared=payload;}},'shared');assert.equal(shared.url,copied);assert.equal(shared.title,'Commander | Battle Cities');
  await scenario({share:async()=>{const e=new Error();e.name='AbortError';throw e;}},'cancelled');
  await scenario({clipboard:{writeText:async()=>{throw new Error('denied');}}},'error');
  await scenario({},'error');
  console.log('Profile WebGL sharing: 5/5 scenarios passed.');
})().catch(error=>{console.error(error);process.exitCode=1;});
