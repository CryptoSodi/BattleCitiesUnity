#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using BattleCities.Core;
using UnityEngine;

namespace BattleCities
{
    public sealed class PowerupGameplayPreviewCapture : MonoBehaviour
    {
        const string Output="Builds/PowerupGameplayPreview";
        IEnumerator Start()
        {
            Directory.CreateDirectory(Output+"/Frames");
            var game=FindFirstObjectByType<BattleGame>();
            while(!game||game.Simulation==null){yield return null;game=FindFirstObjectByType<BattleGame>();}
            while(game.Simulation.Tick<190)yield return null;
            Time.captureFramerate=12;
            string[] kinds={"healmax","freeze","wipeout","shield"};
            int frame=0;
            foreach(var type in kinds){
                var sim=game.Simulation;sim.SpawnPickup(type);
                var player=sim.Player;
                if(player!=null){
                    // Test spawn near the player, in an actual valid open map tile.
                    var candidates=from dy in new[]{-128f,-192f,-64f,64f} from dx in new[]{64f,-64f,128f,-128f,0f}
                        let x=Mathf.Floor((player.X+dx)/64)*64+32 let y=Mathf.Floor((player.Y+dy)/64)*64+32
                        let bounds=new Box(x-32,y-32,64,64)
                        where x>32&&x<sim.Width-32&&y>32&&y<sim.Height-32&&
                            sim.Terrain.All(w=>!w.Alive||(!w.Brick&&w.Type!="steel"&&!BattleTerrain.BlocksTank(w.Type))||!bounds.Overlaps(w.Bounds))
                        select new Vector2(x,y);
                    foreach(var spot in candidates){sim.PickupX=spot.x;sim.PickupY=spot.y;break;}
                }
                for(int i=0;i<12;i++){
                    yield return new WaitForEndOfFrame();
                    var image=ScreenCapture.CaptureScreenshotAsTexture();
                    if(!image)throw new System.Exception("Game View capture failed");
                    var png=image.EncodeToPNG();
                    File.WriteAllBytes(Output+"/Frames/frame-"+(frame++).ToString("D3")+".png",png);
                    if(i==5)File.WriteAllBytes(Output+"/gameplay-"+type+".png",png);
                    Destroy(image);
                }
            }
            Time.captureFramerate=0;
            File.WriteAllText(Output+"/capture-notes.txt","Actual latest BattleCity scene in Unity Editor Play Mode, 1920x1080 Game View. Temporary developer-spawned power-ups: Max Healer, Freeze, Wipeout, Shield. Normal simulation, camera, tanks and HUD; no account inventory changes, scene saves, player builds or publications.");
            Debug.Log("POWERUP PLAY MODE CAPTURE READY: "+Path.GetFullPath(Output));
            UnityEditor.EditorApplication.isPlaying=false;
        }
    }
}
#endif
