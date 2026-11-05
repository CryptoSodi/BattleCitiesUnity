const fs=require('fs'),path=require('path');
const root=process.argv[2];if(!root)throw new Error('Expected the test data folder');
for(const role of ['host','client']){
  const identity=JSON.parse(fs.readFileSync(path.join(root,role+'-account.json'),'utf8'));
  const account=JSON.parse(fs.readFileSync(path.join(root,'ECONOMY',identity.playerId+'.json'),'utf8'));
  const ledger=JSON.parse(fs.readFileSync(path.join(root,'LEDGER',identity.playerId+'.json'),'utf8'));
  const debits=ledger.filter(x=>x.sourceType==='powerup-consumption'&&x.currency==='item:extra-life');
  const activations=ledger.filter(x=>x.sourceType==='multiplayer-powerup-activation');
  const refunds=ledger.filter(x=>x.sourceType==='multiplayer-powerup-refund');
  const result={role,remaining:account.inventory['extra-life'],debits:debits.length,activations:activations.length,refunds:refunds.length};
  const refunded=process.argv.includes('--refund-client')&&role==='client';
  if(result.remaining!==(refunded?1:0)||result.debits!==1||result.activations!==(refunded?0:1)||result.refunds!==(refunded?1:0))throw new Error(JSON.stringify(result));
  console.log(JSON.stringify(result));
}
