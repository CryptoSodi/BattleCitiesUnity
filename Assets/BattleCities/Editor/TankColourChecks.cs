using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BattleCities.Core;
using BattleCities.Multiplayer;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace BattleCities.Editor
{
    /// <summary>Verifies delivered model surfaces and replica identities, and captures the real meshes at the game camera angle.</summary>
    public static class TankColourChecks
    {
        static int checks;
        static void Check(bool value,string label){checks++;if(!value)throw new Exception("TANK COLOUR: "+label);}
        static bool Same(Color a,Color b)=>Mathf.Abs(a.r-b.r)<.0001f&&Mathf.Abs(a.g-b.g)<.0001f&&Mathf.Abs(a.b-b.b)<.0001f;
        static readonly string[] Names={"vanguard","striker","twin-fang","siegebreaker"};
        static readonly Dictionary<string,Material> Cache=new Dictionary<string,Material>();
        static readonly List<Material> Owned=new List<Material>();
        static readonly Dictionary<Material,string> Originals=new Dictionary<Material,string>();
        static readonly List<GameObject> Models=new List<GameObject>();
        static string Folder;
        static Camera camera;
        static BattleGame rig;
        static string SurfaceSnapshot(Material material)
        {
            // Unity refreshes keyword bookkeeping when a shader first renders. Compare authored
            // surface properties instead of transient m_ValidKeywords/m_InvalidKeywords lists.
            var snapshot=new JObject{{"shader",material.shader.GetEntityId().ToString()}};
            for(int i=0;i<ShaderUtil.GetPropertyCount(material.shader);i++)
            {
                string key=ShaderUtil.GetPropertyName(material.shader,i);
                switch(ShaderUtil.GetPropertyType(material.shader,i))
                {
                    case ShaderUtil.ShaderPropertyType.Color:var c=material.GetColor(key);snapshot[key]=new JArray(c.r,c.g,c.b,c.a);break;
                    case ShaderUtil.ShaderPropertyType.Vector:var v=material.GetVector(key);snapshot[key]=new JArray(v.x,v.y,v.z,v.w);break;
                    case ShaderUtil.ShaderPropertyType.TexEnv:
                        var texture=material.GetTexture(key);var scale=material.GetTextureScale(key);var offset=material.GetTextureOffset(key);
                        snapshot[key]=new JArray(texture?texture.GetEntityId().ToString():"none",scale.x,scale.y,offset.x,offset.y);break;
                    default:snapshot[key]=material.GetFloat(key);break;
                }
            }
            return snapshot.ToString(Newtonsoft.Json.Formatting.None);
        }
        static GameObject Model(TankState state,BattleSimulation context=null)
        {
            var prefab=!state.Player&&state.Tier==1?rig.SpeedEnemyModel:rig.TankModels[state.Tier];
            Check(prefab,"existing tank prefab");
            var model=Object.Instantiate(prefab);model.transform.localScale=Vector3.one*.86f;
            foreach(var b in model.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
            var renderers=model.GetComponentsInChildren<Renderer>(true);
            var original=renderers.Select(r=>r.sharedMaterials).ToArray();
            foreach(var source in original.SelectMany(m=>m).Where(m=>m).Distinct())
                if(!Originals.ContainsKey(source))Originals[source]=SurfaceSnapshot(source);
            BattleTankAppearance.Apply(model,state,Cache,Owned,context);
            for(int r=0;r<renderers.Length;r++)
            {
                var current=renderers[r].sharedMaterials;Check(current.Length==original[r].Length,"material slots preserved");
                for(int m=0;m<current.Length;m++)
                {
                    var source=original[r][m];var applied=current[m];
                    Check(applied&&applied.shader&&applied.shader.name!="Hidden/InternalErrorShader","valid shader");
                    bool painted=BattleTankAppearance.TrySurface(state,BattleTankAppearance.Part(renderers[r]),source.name,out var color,out var lens,context);
                    Check(painted?applied!=source&&Same(applied.color,color):applied==source,"only approved surfaces tinted: "+renderers[r].name+"/"+source.name);
                    var expectedKeywords=source.shaderKeywords.ToList();
                    if(BattleTankAppearance.GlowingArmour(state,source.name))expectedKeywords.Add(BattleTankAppearance.EmissionKeyword(applied));
                    Check(applied.shader==source.shader&&applied.shaderKeywords.OrderBy(k=>k).SequenceEqual(expectedKeywords.Distinct().OrderBy(k=>k)),"authored shader retained with dropper-only emission variant");
                    foreach(string texture in source.GetTexturePropertyNames())
                        Check(applied.GetTexture(texture)==source.GetTexture(texture),"texture retained: "+texture);
                    foreach(string property in new[]{"_Metallic","_Smoothness","_MetallicFactor","_RoughnessFactor","metallicFactor","roughnessFactor"})
                        if(source.HasProperty(property))Check(applied.GetFloat(property)==source.GetFloat(property),"surface response retained: "+property);
                    if(lens)foreach(string property in new[]{"_EmissionColor","_EmissiveFactor","emissiveFactor"})
                        if(source.HasProperty(property))Check(Same(applied.GetColor(property),color*source.GetColor(property).maxColorComponent),"authored lens emission uses accent hue");
                }
            }
            if(!state.Player)
            {
                Check(renderers.SelectMany(r=>r.sharedMaterials).Any(m=>m.name=="HeadlightLens"&&Same(m.color,BattleTankAppearance.Accent(state))),"NPC warning lens exists");
                if(state.Tier==1||state.Tier==2||state.Drop)
                    Check(renderers.Where(r=>BattleTankAppearance.Part(r)==(state.Drop?"Hatch":"Barrel")).SelectMany(r=>r.sharedMaterials)
                        .Any(m=>m.name=="BareSteel"&&Same(m.color,BattleTankAppearance.Accent(state))),"localised NPC trim accent exists");
            }
            Models.Add(model);return model;
        }
        static void Capture(string name,IList<GameObject> models,int columns,int rows,int cell=512,float elevation=70)
        {
            foreach(var item in Models)item.SetActive(false);
            float spacing=2.3f;
            for(int i=0;i<models.Count;i++)
            {
                models[i].SetActive(true);
                // The game's forward direction is +Z; row separation follows the projected ground.
                models[i].transform.position=new Vector3((i%columns-(columns-1)*.5f)*spacing,0,((rows-1)*.5f-i/columns)*spacing/Mathf.Sin(elevation*Mathf.Deg2Rad));
            }
            var center=new Vector3(0,.25f,0);float angle=elevation*Mathf.Deg2Rad;
            camera.transform.position=center+new Vector3(0,Mathf.Sin(angle)*22,-Mathf.Cos(angle)*22);camera.transform.LookAt(center,Vector3.up);
            camera.orthographicSize=rows*spacing*.5f;camera.aspect=(float)columns/rows;
            var target=new RenderTexture(cell*columns,cell*rows,24,RenderTextureFormat.ARGB32){antiAliasing=4};target.Create();camera.targetTexture=target;
            try
            {
                camera.Render();camera.Render();RenderTexture.active=target;
                var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
                try{image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Folder,name+".png"),image.EncodeToPNG());}
                finally{Object.DestroyImmediate(image);}
            }
            finally{RenderTexture.active=null;camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);}
        }
        public static void CaptureNpcSides()
        {
            checks=0;Cache.Clear();Owned.Clear();Originals.Clear();Models.Clear();
            Folder=Path.GetFullPath("artifacts/tank-colours/pvp-npc-sides");Directory.CreateDirectory(Folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            rig=Resources.Load<GameObject>("TvReplayRig").GetComponent<BattleGame>();
            camera=new GameObject("NPC comparison camera").AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.nearClipPlane=.01f;camera.farClipPlane=100;
            camera.allowHDR=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            var volume=new GameObject("Existing battle bloom").AddComponent<Volume>();volume.isGlobal=true;volume.priority=10;volume.sharedProfile=Resources.Load<VolumeProfile>("BattleLook");
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.65f,.75f,.85f);
            RenderSettings.ambientEquatorColor=new Color(.32f,.4f,.46f);RenderSettings.ambientGroundColor=new Color(.22f,.19f,.14f);
            var light=new GameObject("Game sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1;light.color=new Color(1,.94f,.82f);light.transform.rotation=Quaternion.Euler(55,-35,0);
            try
            {
                var context=new BattleSimulation(new MapData{field=new FieldData{widthTiles=20,heightTiles=20},rivalBase=new BaseData{x=576,y=0}});
                context.ConfigureMultiplayer(BattleMode.TeamBattle);context.LocalPlayerSlot=0;
                var all=new List<GameObject>();var droppers=new List<GameObject>();var blinkers=new List<BattleTankDropperBlink>();
                string[] names={"01-normal","02-speed","03-fast-shell","04-armoured","05-power-dropper"};
                for(int row=0;row<names.Length;row++)
                {
                    var pair=new List<GameObject>();
                    for(int owner=0;owner<2;owner++)
                    {
                        var state=new TankState{Slot=owner,Tier=row<4?row:0,Drop=row==4};var model=Model(state,context);pair.Add(model);all.Add(model);
                        if(state.Drop){var blink=new BattleTankDropperBlink(model,state,context);blink.Tick(BattleTankDropperBlink.Period*.5f);blinkers.Add(blink);droppers.Add(model);}
                    }
                    Capture(names[row],pair,2,1,768,rig.CameraElevation);
                }
                Capture("all-npc-types-allied-left-enemy-right",all,2,5,512,rig.CameraElevation);
                foreach(var blink in blinkers)blink.Tick(0);
                Capture("06-power-dropper-red-pulse",droppers,2,1,768,rig.CameraElevation);
                // Actual PvP reinforcement roster, grouped beneath each human player.
                // Team reinforcements carry team slots 0/1, not individual player ownership.
                var factions=new List<GameObject>();
                for(int row=0;row<3;row++)for(int player=0;player<4;player++)
                {
                    int team=BattleSimulation.TeamForSlot(player);
                    var state=new TankState{Player=row==0,Slot=row==0?player:team,Tier=row==0?0:row-1};
                    var model=Model(state,context);model.transform.rotation=Quaternion.Euler(0,team==0?0:180,0);factions.Add(model);
                }
                Capture("player-factions-P1-P2-P3-P4",factions,4,3,640,rig.CameraElevation);
                File.WriteAllText(Path.Combine(Folder,"README.md"),"Actual delivered NPC models rendered with BattleTankAppearance and the game's camera elevation/BattleLook bloom. Left: allied to local Player 1; right: hostile. Rows: Normal, Speed, Fast-shell, Armoured, Power Dropper. The Power Dropper is an existing flag shown on the Normal chassis, not a separate tank class. Its base-colour comparison is captured at the dim phase; 06-power-dropper-red-pulse.png shows the red pulse peak. Model previews, not live match screenshots. No artwork or gameplay was modified for these captures.\n");
                Debug.Log("NPC SIDES CAPTURE PASS: "+checks+" surface checks. Captures: "+Folder);
            }
            finally
            {
                foreach(var model in Models)if(model)Object.DestroyImmediate(model);
                foreach(var material in Owned)if(material)Object.DestroyImmediate(material);
                Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(light.gameObject);Object.DestroyImmediate(volume.gameObject);
            }
        }
        public static void Run()
        {
            checks=0;Cache.Clear();Owned.Clear();Originals.Clear();Models.Clear();
            Folder=Path.GetFullPath("artifacts/tank-colours");Directory.CreateDirectory(Folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            DropperGlowBuild.Ensure();
            rig=Resources.Load<GameObject>("TvReplayRig").GetComponent<BattleGame>();
            Check(rig&&rig.TankModels.Length==4&&rig.SpeedEnemyModel,"all current gameplay models assigned");
            camera=new GameObject("Tank colour evidence camera").AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.nearClipPlane=.01f;camera.farClipPlane=100;
            camera.allowHDR=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            var volume=new GameObject("Existing battle bloom").AddComponent<Volume>();volume.isGlobal=true;volume.priority=10;volume.sharedProfile=Resources.Load<VolumeProfile>("BattleLook");
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.65f,.75f,.85f);
            RenderSettings.ambientEquatorColor=new Color(.32f,.4f,.46f);RenderSettings.ambientGroundColor=new Color(.22f,.19f,.14f);
            var light=new GameObject("Game sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1;light.color=new Color(1,.94f,.82f);light.transform.rotation=Quaternion.Euler(55,-35,0);
            try
            {
                var players=new List<GameObject>();string[] hex={"35C759","F5B82E","168BFF","A855F7"};
                for(int tier=0;tier<4;tier++)for(int slot=0;slot<4;slot++)
                {
                    var state=new TankState{Player=true,Slot=slot,Tier=tier};
                    Check(ColorUtility.ToHtmlStringRGB(BattleTankAppearance.Primary(state))==hex[slot],"player slot colour "+slot);
                    var replica=NetTankState.From(state).ToState();Check(BattleTankAppearance.Identity(replica)==BattleTankAppearance.Identity(state),"replica player identity");
                    var model=Model(replica);players.Add(model);Capture("player-"+(slot+1)+"-"+Names[tier],new[]{model},1,1,768,rig.CameraElevation);
                }
                Check(Same(BattleTankAppearance.Primary(new TankState{Player=true}),BattleTankAppearance.PlayerPrimary(0)),"offline player is P1 green");
                Capture("players-all-chassis",players,4,4,512,rig.CameraElevation);
                var enemies=new List<GameObject>();
                for(int tier=0;tier<4;tier++)
                {
                    for(int slot=-1;slot<4;slot++)foreach(bool drop in new[]{false,true})
                    {
                        var state=new TankState{Tier=tier,Slot=slot,Drop=drop};var replica=NetTankState.From(state).ToState();
                        Check(ColorUtility.ToHtmlStringRGB(BattleTankAppearance.Primary(replica))==(tier==3?"1C222C":"343A46"),"NPC neutral with darker bunker armour");
                        Check(ColorUtility.ToHtmlStringRGB(BattleTankAppearance.Secondary(replica))==(tier==3?"3E4857":"687384"),"NPC steel panels with darker bunker panels");
                        Check(BattleTankAppearance.Identity(replica)==BattleTankAppearance.Identity(new TankState{Tier=tier,Drop=drop}),"support NPC slots ignored");
                        if(drop)Check(ColorUtility.ToHtmlStringRGB(BattleTankAppearance.Accent(replica))=="FF4B32","dropper red blink colour");
                    }
                    var model=Model(new TankState{Tier=tier});model.transform.rotation=Quaternion.Euler(0,180,0);enemies.Add(model);
                    Capture(new[]{"npc-normal","npc-speed","npc-fast-shell","npc-armoured"}[tier],new[]{model},1,1,768,rig.CameraElevation);
                }
                var dropper=Model(new TankState{Drop=true,Tier=0});dropper.transform.rotation=Quaternion.Euler(0,180,0);enemies.Add(dropper);Capture("npc-power-dropper",new[]{dropper},1,1,768,rig.CameraElevation);
                Capture("npcs-implemented",enemies,5,1,512,rig.CameraElevation);
                var droppers=new List<GameObject>();var blinkers=new List<BattleTankDropperBlink>();
                for(int tier=0;tier<4;tier++)
                {
                    var state=new TankState{Tier=tier,Drop=true};var model=Model(state);
                    model.transform.rotation=Quaternion.Euler(0,180,0);droppers.Add(model);blinkers.Add(new BattleTankDropperBlink(model,state));
                }
                int blinkMaterials=Owned.Count;var propertyBlock=new MaterialPropertyBlock();
                float pulseQuarter=BattleTankDropperBlink.Period*.25f,pulseHalf=BattleTankDropperBlink.Period*.5f;
                foreach(float time in new[]{0f,pulseQuarter,pulseHalf,pulseQuarter*3,BattleTankDropperBlink.Period})
                {
                    for(int tier=0;tier<4;tier++)
                    {
                        blinkers[tier].Tick(time);var state=new TankState{Tier=tier,Drop=true};int accents=0;
                        foreach(var renderer in droppers[tier].GetComponentsInChildren<Renderer>(true))
                        {
                            var materials=renderer.sharedMaterials;
                            for(int i=0;i<materials.Length;i++)
                            {
                                var material=materials[i];renderer.GetPropertyBlock(propertyBlock,i);
                                bool accent=BattleTankAppearance.TrySurface(state,BattleTankAppearance.Part(renderer),material.name,out var colour,out _)
                                    &&colour==BattleTankAppearance.Accent(state);
                                bool armour=material.name=="PrimaryPaint"||material.name=="SecondaryArmor";
                                if(!accent&&!armour){Check(propertyBlock.isEmpty,"blink leaves mechanics/markings untouched");continue;}
                                accents++;
                                foreach(string property in new[]{"_BaseColor","baseColorFactor","_Color"})
                                    if(material.HasProperty(property))
                                    {
                                        Color bright=armour?BattleTankAppearance.Accent(state)*(material.name=="SecondaryArmor"?.68f:1):material.GetColor(property);
                                        Color dim=armour?(material.name=="SecondaryArmor"?BattleTankAppearance.Secondary(state):BattleTankAppearance.Primary(state)):material.GetColor(property)*.15f;
                                        Color expected=Color.Lerp(dim,bright,BattleTankDropperBlink.Brightness(time));
                                        Check(Same(propertyBlock.GetColor(property),expected),"whole-armour blink phase via per-renderer override");
                                    }
                                if(armour)foreach(string property in new[]{"emissiveFactor","_EmissionColor","_EmissiveFactor"})
                                    if(material.HasProperty(property))
                                    {
                                        var bright=BattleTankDropperBlink.GlowColour*(material.name=="SecondaryArmor"?.68f:1);
                                        Check(Same(propertyBlock.GetColor(property),Color.Lerp(Color.black,bright,BattleTankDropperBlink.Brightness(time))),"smooth HDR armour glow");
                                    }
                                Check(Same(material.color,colour),"blink never modifies cached material");
                            }
                        }
                        Check(accents>0,"each NPC chassis has blinking accents");
                    }
                    if(time==0||time==pulseQuarter||time==pulseHalf)Capture(time==0?"dropper-blink-bright":time==pulseQuarter?"dropper-blink-mid":"dropper-blink-dim",droppers,4,1,512,rig.CameraElevation);
                }
                Check(Mathf.Abs(BattleTankDropperBlink.Brightness(pulseQuarter)-.5f)<.0001f,"gradual halfway fade");
                Check(Mathf.Abs(BattleTankDropperBlink.Brightness(pulseHalf))<.0001f,"pulse returns to neutral armour");
                foreach(float edge in new[]{0f,pulseHalf,BattleTankDropperBlink.Period})Check(Mathf.Abs(BattleTankDropperBlink.Brightness(edge-.001f)-BattleTankDropperBlink.Brightness(edge+.001f))<.001f,"no abrupt transition at pulse edge");
                Check(Owned.Count==blinkMaterials,"blink allocates no duplicate materials");
                foreach(var model in new[]{players[0],enemies[0]})
                {
                    var state=new TankState{Player=model==players[0],Drop=false};new BattleTankDropperBlink(model,state).Tick(.5f);
                    foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))for(int i=0;i<renderer.sharedMaterials.Length;i++)
                    {renderer.GetPropertyBlock(propertyBlock,i);Check(propertyBlock.isEmpty,"players/non-droppers never blink");}
                }
                var directions=new List<GameObject>();
                for(int direction=0;direction<4;direction++)
                {
                    var state=new TankState{Player=true,Tier=1,Slot=direction,Moving=true};var model=Model(state);
                    var animation=model.AddComponent<TankAnimation>();animation.Initialize();animation.Fire();animation.Tick(state,1f/60);
                    model.transform.rotation=Quaternion.Euler(0,direction*90,0);directions.Add(model);
                }
                Capture("directions-game-camera",directions,4,1,512,rig.CameraElevation);
                Capture("directions-low-camera",directions,4,1,512,45);
                int count=Owned.Count;var duplicate=Model(new TankState{Player=true,Tier=0,Slot=0});
                Check(Owned.Count==count,"identical profile reuses cached materials");
                foreach(var original in Originals)Check(SurfaceSnapshot(original.Key)==original.Value,"shared source material unchanged: "+original.Key.name);
                // Verify appearance assignment never mutates the authoritative state.
                foreach(BattleMode mode in Enum.GetValues(typeof(BattleMode)))
                {
                    var context=new BattleSimulation(new MapData{field=new FieldData{widthTiles=20,heightTiles=20},rivalBase=new BaseData{x=576,y=0}});
                    if(mode!=BattleMode.Offline)context.ConfigureMultiplayer(mode);
                    for(int local=0;local<BattleModeRules.PlayerLimit(mode);local++)for(int owner=-1;owner<4;owner++)for(int tier=0;tier<4;tier++)
                    {
                        context.LocalPlayerSlot=local;
                        var npc=new TankState{Player=false,Slot=owner,Tier=tier};
                        bool ally=owner>=0&&(context.IsTeamBattle?owner%2==local%2:owner==local);
                        string primary=tier==3?(context.IsPvp&&!ally?"7F302A":"1C222C"):(context.IsPvp&&!ally?"C55343":"343A46");
                        string secondary=tier==3?(context.IsPvp&&!ally?"A85243":"3E4857"):(context.IsPvp&&!ally?"E88970":"687384");
                        var replica=NetTankState.From(npc).ToState();
                        Check(ColorUtility.ToHtmlStringRGB(BattleTankAppearance.Primary(replica,context))==primary,"local NPC relationship: "+mode+" "+local+"/"+owner);
                        Check(ColorUtility.ToHtmlStringRGB(BattleTankAppearance.Secondary(replica,context))==secondary,"NPC relation panels");
                        Check(ColorUtility.ToHtmlStringRGB(BattleTankAppearance.Primary(new TankState{Player=true,Slot=Math.Max(0,owner)},context))==hex[Math.Max(0,owner)],"player palette independent of relation");
                        if(mode==BattleMode.TeamBattle&&local==0&&owner==0&&tier==0)
                        {
                            var allied=Model(replica,context);var hostile=Model(new TankState{Slot=1,Tier=0},context);
                            Capture("pvp-allied-gunmetal-enemy-red",new[]{allied,hostile},2,1,768,rig.CameraElevation);
                            Capture("bunker-npc-dark-gunmetal-and-dark-red",new[]{Model(new TankState{Slot=0,Tier=3},context),Model(new TankState{Slot=1,Tier=3},context)},2,1,768,rig.CameraElevation);
                            var pulseViews=new List<GameObject>();
                            foreach(float phase in new[]{0f,BattleTankDropperBlink.Period*.25f,BattleTankDropperBlink.Period*.5f})
                            {
                                var carrier=new TankState{Slot=1,Tier=0,Drop=true};var pulseModel=Model(carrier,context);
                                new BattleTankDropperBlink(pulseModel,carrier,context).Tick(phase);pulseViews.Add(pulseModel);
                                foreach(var renderer in pulseModel.GetComponentsInChildren<Renderer>(true))
                                {
                                    var materials=renderer.sharedMaterials;
                                    for(int i=0;i<materials.Length;i++)
                                    {
                                        var material=materials[i];bool panel=material.name=="SecondaryArmor";
                                        if(!panel&&material.name!="PrimaryPaint")continue;
                                        var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,i);
                                        Color neutral=panel?new Color32(0x68,0x73,0x84,255):new Color32(0x34,0x3A,0x46,255);
                                        foreach(string property in new[]{"_BaseColor","baseColorFactor","_Color"})if(material.HasProperty(property))
                                            Check(Same(block.GetColor(property),Color.Lerp(neutral,BattleTankAppearance.Accent(carrier)*(panel?.68f:1),BattleTankDropperBlink.Brightness(phase))),"red PvP carrier fades through gunmetal/steel-grey");
                                        foreach(string property in new[]{"emissiveFactor","_EmissionColor","_EmissiveFactor"})if(material.HasProperty(property))
                                            Check(Same(block.GetColor(property),Color.Lerp(Color.black,BattleTankDropperBlink.GlowColour*(panel?.68f:1),BattleTankDropperBlink.Brightness(phase))),"red PvP carrier glow fades to zero");
                                        Check(Same(material.color,panel?BattleTankAppearance.Secondary(carrier,context):BattleTankAppearance.Primary(carrier,context)),"carrier pulse retains cached red faction paint");
                                    }
                                }
                            }
                            Capture("pvp-red-carrier-bright-mid-gunmetal",pulseViews,3,1,640,rig.CameraElevation);
                            int identity=BattleTankAppearance.Identity(replica,context);context.LocalPlayerSlot=1;
                            Check(BattleTankAppearance.Identity(replica,context)!=identity,"local-team change refreshes NPC model");context.LocalPlayerSlot=local;
                        }
                    }
                }
                var sim=new BattleSimulation(new MapData());string before=sim.ReplayStateHash();
                var temporary=Model(sim.Player);Check(before==sim.ReplayStateHash(),"appearance leaves simulation state unchanged");
                foreach(BattleMode mode in Enum.GetValues(typeof(BattleMode)))
                {
                    if(mode==BattleMode.Offline)continue;
                    var online=new BattleSimulation(new MapData{field=new FieldData{widthTiles=20,heightTiles=20},rivalBase=new BaseData{x=576,y=0}}){DisableEnemyFire=true};online.ConfigureMultiplayer(mode);
                    for(int slot=0;slot<BattleModeRules.PlayerLimit(mode);slot++)online.SetParticipant(slot,true);
                    online.BeginMatch();
                    for(int tick=0;tick<150;tick++)online.StepMultiplayer(new Dictionary<int,Command>());
                    foreach(var tank in online.Tanks)
                    {
                        var replica=NetTankState.From(tank).ToState();
                        bool hostile=online.IsPvp&&!online.AreAllies(tank.Slot,online.LocalPlayerSlot);
                        string expected=tank.Player?hex[Mathf.Clamp(tank.Slot,0,3)]:tank.Tier==3?(hostile?"7F302A":"1C222C"):(hostile?"C55343":"343A46");
                        Check(ColorUtility.ToHtmlStringRGB(BattleTankAppearance.Primary(replica,online))==expected,"spawn/replica colour in "+mode);
                    }
                    if(online.IsTeamBattle)Check(online.Tanks.Any(t=>!t.Player),"support NPCs present in "+mode);
                }
                BattleCities.Tests.TankStatsChecks.RunEditor();
                File.WriteAllText(Path.Combine(Folder,"validation.txt"),"PASS: "+checks+" appearance checks; existing TankStats/ShootingHealth/Multiplayer suites completed.\nEditor model captures; not device or live network tests.\nColumns: Player 1 green, Player 2 yellow, Player 3 blue, Player 4 purple. Rows: Vanguard, Striker, Twin Fang, Siegebreaker.\nNPCs: Normal, Speed, Fast-shell (existing tier 2), Armoured, Power Dropper. No standalone Bunker NPC exists.\n");
                Debug.Log("TANK COLOUR PASS: "+checks+" checks. Captures: "+Folder);
            }
            finally
            {
                foreach(var model in Models)if(model)Object.DestroyImmediate(model);
                foreach(var material in Owned)if(material)Object.DestroyImmediate(material);
                Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(light.gameObject);Object.DestroyImmediate(volume.gameObject);
            }
        }
    }
}
