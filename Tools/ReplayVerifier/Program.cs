using BattleCities.Core;
using BattleCities.Tests;
using Newtonsoft.Json.Linq;

try
{
    if(args.Length==1&&args[0]=="--self-test")
    {ReplayChecks.Run();Console.WriteLine("Replay verification tests passed.");return 0;}
    if(args.Length==2&&args[0]=="--fixture")
    {File.WriteAllText(args[1],ReplayJson.Write(ReplayChecks.Fixture()));Console.WriteLine("Fixture written.");return 0;}
    if(args.Length!=1){Console.Error.WriteLine("Usage: ReplayVerifier <replay.json> | --self-test | --fixture <file>");return 2;}
    var info=new FileInfo(args[0]);
    if(info.Length>BattleReplay.MaxBytes+16384)throw new FormatException("Replay file too large.");
    var token=JObject.Parse(File.ReadAllText(args[0]));
    var data=ReplayJson.Read((token["replay"]??token).ToString());
    var player=new ReplayPlayer(data);
    while(!player.Complete&&player.Error==null)player.Step();
    Console.WriteLine(ReplayJson.Write(new {reproduced=player.Complete,error=player.Error,simulationVersion=data.simulationVersion,
        result=ReplayResult.From(player.Simulation),competitiveApproval=false,
        reason="Reproduction only. Independently validate approved content, session, input legitimacy, and economy event receipts."}));
    return player.Complete?0:1;
}
catch(Exception error){Console.WriteLine(ReplayJson.Write(new{reproduced=false,error=error.Message}));return 1;}
