// Isolated local API for the opt-in two-client power-up smoke test.
const fs=require('fs/promises'),path=require('path'),http=require('http');
const root=process.argv[2],apiRoot=process.env.BC_SMOKE_API_ROOT;
if(!root||!apiRoot)throw new Error('Expected a test folder and BC_SMOKE_API_ROOT');
process.env.NODE_ENV='test';process.env.BATTLECITY_STORAGE_MODE='local';
for(const key of ['ECONOMY','LEDGER','SEASON','PLAYER','SESSION'])process.env[`BATTLECITY_${key}_DIR`]=path.join(root,key);
process.env.BATTLECITY_COMPETITIONS_FILE=path.join(root,'competitions.json');
const economy=require(path.join(apiRoot,'dist/src/stores/economyStore'));
const sessions=require(path.join(apiRoot,'dist/src/stores/sessionStore'));
const players=require(path.join(apiRoot,'dist/src/stores/playerStore'));
const app=require(path.join(apiRoot,'dist/src/app')).default;
(async()=>{
  await fs.mkdir(root,{recursive:true});
  const clients={};
  for(const role of ['host','client']){
    const session=await sessions.createGuestSession(),player=await players.readPlayer(session.playerId);
    await economy.grantOnChainPurchaseForPlayer(player,'extra-life','sol',`smoke-${role}`,null);
    await economy.upsertAccountForPlayer(player,{loadout:{'active-one':'extra-life'}});
    clients[role]={playerId:player.id,cookie:`battlecity_session=${session.id}`};
    await fs.writeFile(path.join(root,role+'-account.json'),JSON.stringify(clients[role]));
  }
  let dropped=false;
  const server=http.createServer(async(req,res)=>{
    try{
      const chunks=[];for await(const chunk of req)chunks.push(chunk);
      const body=Buffer.concat(chunks);const method=req.method;
      const request=new Request(`http://127.0.0.1:${server.address().port}${req.url}`,{method,headers:req.headers,...(body.length?{body}: {})});
      const response=await app.fetch(request);const bytes=Buffer.from(await response.arrayBuffer());
      if(!dropped&&req.url==='/api/economy/powerups/consume'&&req.headers.cookie===clients.client.cookie&&response.status===200){
        dropped=true;await fs.writeFile(path.join(root,'dropped-response.txt'),'first client consumption response dropped after API commit');req.socket.destroy();return;
      }
      if(process.env.BC_SMOKE_HOLD_REDEEM==='1'&&req.method==='POST'&&req.url==='/api/economy/powerups/redeem'&&response.status===200&&JSON.parse(body.toString()).playerId===clients.client.playerId&&!require('fs').existsSync(path.join(root,'redemption-inflight.txt'))){
        await fs.writeFile(path.join(root,'redemption-inflight.txt'),'client receipt committed; host confirmation response intentionally delayed');
        await new Promise(resolve=>setTimeout(resolve,12000));
      }
      res.writeHead(response.status,Object.fromEntries(response.headers));res.end(bytes);
    }catch(e){console.error(e);if(!res.headersSent)res.writeHead(500);res.end('test server failure');}
  });
  server.listen(0,'127.0.0.1',async()=>{
    const url=`http://127.0.0.1:${server.address().port}`;
    await fs.writeFile(path.join(root,'api-url.txt'),url);console.log(`SMOKE_API_READY ${url}`);
  });
})().catch(e=>{console.error(e);process.exit(1);});
