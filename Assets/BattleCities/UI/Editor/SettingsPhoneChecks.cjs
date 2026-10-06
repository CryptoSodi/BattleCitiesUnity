// Run with Node and Playwright. PLAYWRIGHT_MODULE may point to a bundled installation.
const fs=require('fs'),http=require('http'),path=require('path');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const base=path.resolve(__dirname,'../../..')+'/';
const bridge=fs.readFileSync(base+'Plugins/WebGL/BattleCitiesPhone.jslib','utf8');
const client=fs.readFileSync(base+'StreamingAssets/BattleCitiesPhone/index.html','utf8');
(async()=>{
 const server=http.createServer((req,res)=>{
  if(req.url==='/game/StreamingAssets/BattleCitiesPhone/BarlowCondensed-Bold.ttf'){res.setHeader('Content-Type','font/ttf');res.end(fs.readFileSync(base+'StreamingAssets/BattleCitiesPhone/BarlowCondensed-Bold.ttf'));return;}
  res.setHeader('Content-Type','text/html');res.end(req.url.startsWith('/game/StreamingAssets/BattleCitiesPhone/index.html')?client:'<!doctype html><title>WebGL bridge check</title>');
 });
 await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));let browser;
 try{
  browser=await chromium.launch({headless:true,channel:'chrome'});
  const host=await browser.newPage();await host.goto('http://127.0.0.1:'+server.address().port+'/game/index.html');
  await host.evaluate(code=>{
   window.events=[];window.LibraryManager={library:{}};window.mergeInto=(a,b)=>Object.assign(a,b);window.UTF8ToString=x=>x;
   window.SendMessage=(target,method,json)=>events.push(JSON.parse(json));(0,eval)(code);
   LibraryManager.library.BattleCitiesPhoneStart('test','StreamingAssets/BattleCitiesPhone/index.html');
  },bridge);
  await host.waitForFunction(()=>events.some(e=>e.type==='ready'||e.type==='error'),null,{timeout:25000});
  const ready=await host.evaluate(()=>events.find(e=>e.type==='ready'));if(!ready)throw new Error('Pairing did not start');
  console.log('PASS: bundled controller page and real pairing QR under /game/ subdirectory');
  const phone=await browser.newPage({viewport:{width:844,height:390},hasTouch:true});
  await phone.goto('http://127.0.0.1:'+server.address().port+'/game/StreamingAssets/BattleCitiesPhone/index.html?rc='+ready.room);
  await phone.locator('#status.connected').waitFor({timeout:20000});
  await host.waitForFunction(()=>events.some(e=>e.type==='state'),null,{timeout:5000});
  console.log('PASS: bundled phone page pairs over real WebRTC');
  const a=await phone.locator('[data-button="0"]').boundingBox();await phone.mouse.move(a.x+a.width/2,a.y+a.height/2);await phone.mouse.down();
  await host.waitForFunction(()=>events.some(e=>e.type==='state'&&e.buttons[0]),null,{timeout:5000});
  await phone.mouse.up();await host.waitForFunction(()=>{const s=events.filter(e=>e.type==='state');return s.length>0&&!s[s.length-1].buttons[0];});
  console.log('PASS: real A button press and release reach Unity callback');
  const stick=await phone.locator('#stick').boundingBox();await phone.mouse.move(stick.x+stick.width/2,stick.y+stick.height/2);await phone.mouse.down();await phone.mouse.move(stick.x+stick.width*.8,stick.y+stick.height/2);
  await host.waitForFunction(()=>events.some(e=>e.type==='state'&&e.axes[0]>.5),null,{timeout:5000});await phone.mouse.up();
  await host.waitForFunction(()=>{const s=events.filter(e=>e.type==='state');return s.length>0&&s[s.length-1].axes.every(x=>x===0);});
  console.log('PASS: joystick moves and returns to neutral');
  if(process.env.SETTINGS_PHONE_SCREENSHOT)await phone.screenshot({path:process.env.SETTINGS_PHONE_SCREENSHOT});
  await phone.close();await host.waitForFunction(()=>events.some(e=>e.type==='closed'),null,{timeout:5000});
  await host.evaluate(()=>LibraryManager.library.BattleCitiesPhoneStop());console.log('PASS: page close releases peer and host cleanup');
 }finally{if(browser)await browser.close();await new Promise(resolve=>server.close(resolve));}
})().catch(e=>{console.error(e.message);process.exitCode=1;});
