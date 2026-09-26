// Local-only contract fixture; never connects to production or handles credentials.
const http = require('node:http');
const initial = () => ({fuelBalance: 10,tokenBalance: 0,solBalance: 0,inventory: {shield:2,freeze:1,upgrade:1},loadout:{}});
let account = initial();
let puts = 0;
let failAfterCommit = true;
http.createServer(async (req,res) => {
  const send=(code,data)=>{res.writeHead(code,{'Content-Type':'application/json'});res.end(JSON.stringify(data));};
  if(req.url==='/fixture/status')return send(200,{account,puts});
  if(req.url==='/fixture/reset'){account=initial();puts=0;failAfterCommit=true;return send(200,{ok:true});}
  if(req.url==='/fixture/empty'){account.fuelBalance=0;return send(200,{ok:true});}
  if(req.url!=='/api/economy/account')return send(404,{});
  if(req.method==='PUT'){
    let raw='';for await(const chunk of req)raw+=chunk;
    const update=JSON.parse(raw).account;puts++;
    if(update.fuelBalance!==undefined)account.fuelBalance=Math.min(account.fuelBalance,update.fuelBalance);
    if(update.loadout)account.loadout=update.loadout;
    // Commit, then report an uncertain server result. Unlike a dropped socket,
    // this cannot be silently retried by Unity's underlying HTTP transport.
    if(update.fuelBalance!==undefined&&failAfterCommit){failAfterCommit=false;return send(503,{error:'simulated response failure after commit'});}
  }
  send(200,{authenticated:true,account});
}).listen(18765,'127.0.0.1',()=>console.log('Pre-battle fixture ready on 127.0.0.1:18765'));
