using System;
using System.Collections.Generic;
using System.Linq;
using BattleCities.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using Newtonsoft.Json;

namespace BattleCities
{
    [Serializable] public sealed class BrickSections { public BrickSection[] sections; }
    [Serializable] public sealed class BrickSection { public float[] center; public string[] bricks, mortar; }
    public sealed partial class BattleGame : MonoBehaviour
    {
        public GameObject[] TankModels;
        public GameObject SpeedEnemyModel;
        public GameObject BrickModel, SteelModel, BushModel, EagleModel, RuinedEagleModel, MineModel, DroneModel, GroundTurretPrefab;
        public GameObject[] BulletModels;
        public TextAsset BrickSectionData;
        public Texture2D[] GroundTextures;
        public Texture2D PowerupAtlas;
        [Range(1,35)] public int Stage=1;
        public bool DayCycle=true, Night, Dust=true, Headlights=true, AutomaticCamera=true, CameraShake=true;
        public bool ChaseCamera;
        [Range(.9f,2.4f)] public float ChaseCameraHeight=1.65f;
        [Range(1.2f,4f)] public float ChaseCameraDistance=2.1f;
        public bool EnemyFire=true;
        [Range(40,90)] public float CameraElevation=70;
        [Range(.7f,2f)] public float Zoom=1;
        public BattleSimulation Simulation { get; private set; }
        private Transform stageRoot, actorsRoot;
        private Camera gameCamera;
        private Light sun;
        private ReflectionProbe reflections;
        private readonly Dictionary<int, Actor> actors=new Dictionary<int,Actor>();
        private readonly Dictionary<int,GameObject> shots=new Dictionary<int,GameObject>();
        private readonly Dictionary<int,MineVisual> mines=new Dictionary<int,MineVisual>();
        private readonly Dictionary<int,DroneVisual> drones=new Dictionary<int,DroneVisual>();
        private readonly Dictionary<int,GroundTurret> turrets=new Dictionary<int,GroundTurret>();
        private sealed class DroneVisual { public GameObject Root; public Transform Body; public Transform[] Rotors; public Vector2 LastPosition; }
        private Material dronePaint,droneRange;
        private sealed class MineVisual { public GameObject Root; public Transform Disc,Indicator; public Transform[] Dirt; }
        private Material mineMetal,mineDirt,mineReady;
        private bool secondaryQueued;
        private readonly List<Batch> batches=new List<Batch>();
        private readonly List<Mesh> ownedMeshes=new List<Mesh>();
        private readonly List<Material> ownedMaterials=new List<Material>();
        private readonly Dictionary<string,Material> tankPaint=new Dictionary<string,Material>();
        private readonly Dictionary<string,List<Part>> brickParts=new Dictionary<string,List<Part>>();
        private List<Part> steelParts,bushParts;
        private Material street,pavement,paint,water,particleMaterial;
        private GameObject eagle;
        private EagleNightTint eagleTint;
        private InputActionMap input;
        private InputAction[] moveKeys,aimKeys;
        private InputAction fire,chaseFire,secondaryFire,secondarySelect,pause,restart,debug,overhead;
        private readonly long[] moveOrder=new long[4],aimOrder=new long[4];
        private long order;
        private float accumulator,trauma,dayTime;
        private bool paused,showDebug,terrainDirty;
        private Vector2 debugScroll;
        private string debugPowerupStatus="Free test use - inventory is unchanged.";
        private static readonly string[] debugPowerupTypes={"shield","defence","freeze","life","upgrade","wipeout","speed","zoomout"};
        private static readonly string[] debugPowerupNames={"Tank shield","Base steel shield","Freeze enemies","Extra life","Upgrade tank","Destroy enemies","Speed boost","Zoom out"};
        private int requestedStage=1;
        private Vector3 cameraTarget;
        private Facing? chaseAim;
        private int chaseAimTankId;
        private float fps;
        private EconomyClient economy;
        private BattleAnimations effects;
        private BattleDebris debris;
        private BattleWeather weather;
        private PickupSparkles pickupSparkles;
        private readonly BattleHud hud=new BattleHud();
        private bool consumePending;
        private InputAction[] slots;
        public bool Paused {get=>paused;set{if(paused==value)return;paused=value;BlockControllerTransition();}}
        private sealed class Actor { public GameObject Root,Model; public Transform Turret; public Transform[] Muzzles; public ParticleSystem Dust; public Light[] Lights; public int Tier; public bool Drop; public Color Color; public TankAnimation Animation; public TankShield Shield; }
        private sealed class Part { public Mesh Mesh;public Material Material;public int Submesh;public Matrix4x4 Local; }
        private sealed class Batch { public Part Part;public Matrix4x4[] Matrices; }

        private void Awake()
        {
            Debug.Log("[BattleCities] Runtime: "+RuntimePlatformInfo.Current+" ("+RuntimePlatformInfo.DeviceIdentity+")");
            pickupSparkles=new PickupSparkles();
            input=new InputActionMap("Gameplay");
            moveKeys=Keys("Drive",new[]{"w","d","s","a"},moveOrder);
            aimKeys=Keys("Turret",new[]{"upArrow","rightArrow","downArrow","leftArrow"},aimOrder);
            fire=input.AddAction("Fire",InputActionType.Button,"<Keyboard>/space");
            chaseFire=input.AddAction("ChaseFire",InputActionType.Button,"<Mouse>/leftButton");
            secondaryFire=input.AddAction("SecondaryFire",InputActionType.Button,"<Keyboard>/e");
            secondaryFire.AddBinding("<Mouse>/rightButton");
            secondarySelect=input.AddAction("SelectSecondary",InputActionType.Button,"<Keyboard>/q");
            pause=input.AddAction("Pause",InputActionType.Button,"<Keyboard>/p");
            restart=input.AddAction("Restart",InputActionType.Button,"<Keyboard>/r");
            debug=input.AddAction("Debug",InputActionType.Button,"<Keyboard>/f1");
            overhead=input.AddAction("Camera",InputActionType.Button,"<Keyboard>/c");
            ConfigureDayNightShortcuts();
            slots=Enumerable.Range(1,4).Select(i=>input.AddAction("Powerup"+i,InputActionType.Button,"<Keyboard>/digit"+i)).ToArray();
            economy=tvReplay||LevelEditor.LevelEditorPlaytest.IsActive?null:GetComponent<EconomyClient>();
            if(!tvReplay){ConfigureTouchBindings();ConfigureGamepadBindings();input.Enable();}
            street=Material(new Color(.24f,.28f,.29f));pavement=Material(new Color(.58f,.58f,.51f));paint=Material(new Color(.82f,.81f,.65f));
            var visualAssets=Resources.Load<BattleVisualAssets>("BattleVisualAssets");
            water=visualAssets&&visualAssets.WaterMaterial?visualAssets.WaterMaterial:Material(new Color(.03f,.40f,.56f));
            mineMetal=Material(new Color(.16f,.21f,.13f));mineDirt=Material(new Color(.32f,.23f,.12f));mineReady=Material(new Color(.95f,.57f,.04f));
            dronePaint=Material(new Color(.95f,.58f,.025f));
            droneRange=new Material(Shader.Find("Sprites/Default"));ownedMaterials.Add(droneRange);droneRange.color=new Color(1,1,1,.2f);
            var psShader=Shader.Find("Universal Render Pipeline/Particles/Unlit");
            particleMaterial=new Material(psShader);ownedMaterials.Add(particleMaterial);
            particleMaterial.SetColor("_BaseColor",Color.white);
            particleMaterial.SetFloat("_Surface",1);particleMaterial.SetFloat("_Blend",0);particleMaterial.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);particleMaterial.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);particleMaterial.SetFloat("_ZWrite",0);particleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");particleMaterial.renderQueue=3000;
            var tex=new Texture2D(32,32,TextureFormat.RGBA32,false);tex.name="Soft tank dust";
            for(int y=0;y<32;y++)for(int x=0;x<32;x++){float a=Mathf.Clamp01(1-Vector2.Distance(new Vector2(x,y),new Vector2(15.5f,15.5f))/15.5f);tex.SetPixel(x,y,new Color(1,1,1,a*a));}tex.Apply();particleMaterial.SetTexture("_BaseMap",tex);
            effects=gameObject.AddComponent<BattleAnimations>();debris=gameObject.AddComponent<BattleDebris>();weather=gameObject.AddComponent<BattleWeather>();weather.ApplyGlobalLighting=!tvReplay;BuildParts();SetupLighting();if(tvReplay)PlayReplay(tvReplayData);else LoadStage(!tvReplay&&BattlePreparation.Ready?BattlePreparation.StartStage:Stage);
        }
        private InputAction[] Keys(string name,string[] keys,long[] stamps)
        {
            var list=new InputAction[4];for(int i=0;i<4;i++){int index=i;list[i]=input.AddAction(name+i,InputActionType.Button,"<Keyboard>/"+keys[i]);list[i].performed+=_=>stamps[index]=++order;}return list;
        }
        private Facing? Latest(InputAction[] keys,long[] stamps)
        { long best=-1;Facing? d=null;for(int i=0;i<4;i++)if(keys[i].IsPressed()&&stamps[i]>best){best=stamps[i];d=(Facing)i;}return d; }
        private Material Material(Color color)
        {var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=color;m.SetFloat("_Smoothness",.2f);m.enableInstancing=true;ownedMaterials.Add(m);return m;}
        private List<Part> Parts(GameObject root,HashSet<string> allowed=null)
        {
            var parts=new List<Part>();
            foreach(var mesh in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(allowed!=null&&!allowed.Contains(mesh.name)&&!Ancestors(mesh.transform,allowed,root.transform))continue;
                var renderer=mesh.GetComponent<MeshRenderer>();if(renderer==null)continue;
                for(int i=0;i<mesh.sharedMesh.subMeshCount;i++)
                {
                    var source=renderer.sharedMaterials[Math.Min(i,renderer.sharedMaterials.Length-1)];
                    var mat=Resources.Load<Material>("RuntimeShaders/Terrain/"+source.name);
                    if(!mat)mat=source;
                    mat.enableInstancing=true;
                    parts.Add(new Part{Mesh=mesh.sharedMesh,Material=mat,Submesh=i,Local=root.transform.worldToLocalMatrix*mesh.transform.localToWorldMatrix});
                }
            }return parts;
        }
        private bool Ancestors(Transform t,HashSet<string> names,Transform stop)
        {while(t!=null&&t!=stop){if(names.Contains(t.name))return true;t=t.parent;}return false;}
        private void BuildParts()
        {
            var data=JsonUtility.FromJson<BrickSections>(BrickSectionData.text);
            foreach(var s in data.sections)
            {
                int x=Mathf.RoundToInt((s.center[0]+.375f)*4),z=Mathf.RoundToInt((s.center[2]+.375f)*4);
                brickParts[x+","+z]=Parts(BrickModel,new HashSet<string>(s.bricks.Concat(s.mortar)));
            }
            if(brickParts.Count!=16||brickParts.Values.Any(p=>p.Count==0))throw new InvalidOperationException("Approved brick section names did not import correctly");
            steelParts=Parts(SteelModel);bushParts=Parts(BushModel);
        }
        public void LoadStage(int stage)=>LoadStageMap(LevelEditor.LevelEditorPlaytest.IsActive?LevelEditor.LevelEditorPlaytest.PreviewStage:stage,LevelEditor.LevelEditorPlaytest.Draft);
        private void LoadStageMap(int stage,MapData mapOverride)
        {
            if(IsOnline&&!loadingOnline)return;
            FinishRecording();
            if(!loadingReplay){replayPlayer=null;if(touchControls)touchControls.enabled=true;}
            if(stage<1||stage>35)throw new ArgumentOutOfRangeException(nameof(stage),"Select a stage from 01 to 35.");
            var asset=Resources.Load<TextAsset>("Maps/"+stage.ToString("00"));if(asset==null&&mapOverride==null)throw new InvalidOperationException("Missing stage "+stage);
            var shootingSettings=SelectedTankShootingSettings;
            var map=mapOverride??JsonConvert.DeserializeObject<MapData>(asset.text);
            ResetPrimaryFire();chaseAim=null;chaseAimTankId=0;Stage=requestedStage=stage;Simulation=replayLoadingData!=null?ReplayPlayer.CreateSimulation(replayLoadingData):new BattleSimulation(map,stage,shootingSettings?shootingSettings.NormalReloadSeconds:.12f,shootingSettings?shootingSettings.UpgradedNormalReloadSeconds:.08f,BattlePreparation.TankTier);accumulator=0;paused=false;
            if(!GroundTurretPrefab)throw new InvalidOperationException("GroundTurret prefab is not assigned");
            var turretSettings=GroundTurretPrefab.GetComponent<GroundTurret>();
            if(!turretSettings)throw new InvalidOperationException("GroundTurret prefab is missing its runtime component");
            Simulation.ConfigureTurrets(turretSettings.AttackRange*64,turretSettings.Damage,turretSettings.FireCooldown,turretSettings.ReloadDuration,turretSettings.CardinalTurnDuration,turretSettings.MuzzleDistance*64,turretSettings.Health,turretSettings.DeployDuration);
            Simulation.LandDroneSettings=LandDroneSettings;
            if(replayLoadingData!=null)Simulation.ApplyReplayConfiguration(replayLoadingData.config);
            landDroneViews.Clear();
            Simulation.LandDroneExploded+=d=>{PlayLandDroneExplosion();effects.Burst(World(d.X,d.Y,.2f),1.6f);if(CameraShake)trauma=Mathf.Max(trauma,.35f);};
            if(stageRoot)Destroy(stageRoot.gameObject);if(actorsRoot)Destroy(actorsRoot.gameObject);
            stageRoot=new GameObject("Stage "+stage).transform;stageRoot.SetParent(transform);
            actorsRoot=new GameObject("Actors").transform;actorsRoot.SetParent(transform);
            actors.Clear();shots.Clear();shotPresentation.Clear();mines.Clear();drones.Clear();turrets.Clear();environmentViews.Clear();secondaryQueued=false;batches.Clear();effects.Clear();debris.Clear();weather.ClearPrecipitation();
            Simulation.DroneDetonated+=d=>{effects.Burst(World(d.X,d.Y,.4f),1.6f);if(CameraShake)trauma=Mathf.Max(trauma,.35f);};
            Simulation.MineDetonated+=m=>{effects.Burst(World(m.X,m.Y,.08f),1.5f);if(CameraShake)trauma=Mathf.Max(trauma,.35f);};
            Simulation.TurretFired+=t=>{if(turrets.TryGetValue(t.Id,out var view))effects.MuzzleFlash(view.MuzzlePosition,Quaternion.Euler(0,(int)t.Heading*90,0)*Vector3.forward,new Color(.95f,.75f,.2f));};
            Simulation.TurretHit+=t=>{effects.Impact(World(t.X,t.Y,.5f),Vector3.up,false);if(CameraShake)trauma=Mathf.Max(trauma,.08f);};
            Simulation.TurretDestroyed+=t=>{effects.Burst(World(t.X,t.Y,.5f),1.6f);if(CameraShake)trauma=Mathf.Max(trauma,.4f);};
            Simulation.WallDestroyed+=w=>{var position=World(w.Bounds.X+w.Bounds.W/2,w.Bounds.Y+w.Bounds.H/2,.25f);if(w.IsBuildingSection)debris.Building(position);else debris.Wall(position,!w.Brick);if(w.PropKey!=null){if(environmentViews.TryGetValue(w.Id,out var piece))piece.RefreshVisual();}else terrainDirty=true;if(CameraShake)trauma=Mathf.Max(trauma,.08f);};
            Simulation.WallDamaged+=w=>{if(environmentViews.TryGetValue(w.Id,out var piece))piece.RefreshVisual();};
            Simulation.TerrainChanged+=()=>terrainDirty=true;
            var current=Simulation;
            Simulation.DropRequested+=()=>RollDrop(current);
            Simulation.CurrencyClaimed+=id=>{if(!IsReplaying&&!IsOnline&&economy) _=economy.Claim(id);};
            Simulation.ShotImpact+=OnShotImpact;
            Simulation.ShotFired+=OnShotFired;
            Simulation.TankDestroyed+=t=>{if(actors.TryGetValue(t.Id,out var dead))debris.Tank(dead.Model,World(t.X,t.Y,.3f));effects.Burst(World(t.X,t.Y,.35f),1.3f);if(CameraShake)trauma=.3f;};
            Simulation.BaseDestroyed+=()=>{effects.Burst(World(Simulation.BaseBounds.X+32,Simulation.BaseBounds.Y+32,.35f),1.7f);if(CameraShake)trauma=.6f;Destroy(eagle);eagle=Instantiate(RuinedEagleModel,World(Simulation.BaseBounds.X+32,Simulation.BaseBounds.Y+32,.1f),Quaternion.Euler(0,180,0),stageRoot);eagleTint=eagle.AddComponent<EagleNightTint>();eagleTint.SetDarkness(weather.Darkness);Shadows(eagle);};
            BuildGround(map);BuildEnvironment(map);BuildStageLights(map);RebuildTerrain();
            eagle=Instantiate(EagleModel,World(Simulation.BaseBounds.X+32,Simulation.BaseBounds.Y+32,.1f),Quaternion.Euler(0,180,0),stageRoot);
            eagleTint=eagle.AddComponent<EagleNightTint>();eagleTint.SetDarkness(weather.Darkness);
            Shadows(eagle);cameraTarget=World(Simulation.Width/2,Simulation.Height/2);SyncActors(0);UpdateCamera(1);weather.Tick(0,Simulation.Width/64f,Simulation.Height/64f,sun,reflections);if(tvReplay)IsolateTvReplayVisuals();reflections.RenderProbe();
            BeginRecording(map);
        }
        public static Vector3 World(float x,float y,float height=0)=>new Vector3(x/64,height,-y/64);
        private void Cube(string name,Vector3 p,Vector3 scale,Material m)
        {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(stageRoot);g.transform.position=p;g.transform.localScale=scale;BattleVisualLifetime.Release(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=m;}
        private void BuildGround(MapData map)
        {
            float w=Simulation.Width/64f,h=Simulation.Height/64f;
            Material groundMat=street;
            if(Stage!=1){groundMat=Material(Color.white);groundMat.mainTexture=GroundTextures[(Stage-1)%4];groundMat.mainTextureScale=new Vector2(w/2,h/2);}
            BattleGroundSurface.Create(stageRoot,"Ground",new Rect(0,-h,w,h),-.025f,groundMat,Simulation.Terrain);
            Cube("North curb",new Vector3(w/2,.02f,.14f),new Vector3(w+.56f,.13f,.28f),pavement);
            Cube("South curb",new Vector3(w/2,.02f,-h-.14f),new Vector3(w+.56f,.13f,.28f),pavement);
            Cube("West curb",new Vector3(-.14f,.02f,-h/2),new Vector3(.28f,.13f,h),pavement);
            Cube("East curb",new Vector3(w+.14f,.02f,-h/2),new Vector3(.28f,.13f,h),pavement);
            BuildGroundSurfaces(map);
            if(Stage==1)
            {
                for(int x=0;x<w;x++)for(int y=0;y<h;y++)
                {
                    var cell=new Box(x*64,y*64,64,64);
                    bool occupied=Simulation.Terrain.Any(t=>(t.StopsBullet||BattleTerrain.IsSurface(t.Type))&&t.Bounds.Overlaps(cell))||Simulation.BaseBounds.Overlaps(cell);
                    if(!occupied&&x%2==0)Cube("Road marking",new Vector3(x+.5f,-.009f,-y-.5f),new Vector3(.035f,.015f,.28f),paint);
                }
                foreach(var region in map.terrain.regions.Where(r=>r.type.Contains("brick")||r.type=="steel"))Cube("Wall pavement",World(region.x+region.width/2,region.y+region.height/2,-.013f),new Vector3(region.width/64+.12f,.018f,region.height/64+.12f),pavement);
            }
            BuildTerrainSurfaces();
        }
        private void RebuildTerrain()
        {
            batches.Clear();var grouped=new Dictionary<Part,List<Matrix4x4>>();
            foreach(var wall in Simulation.Terrain)
            {
                if(!wall.Alive)continue;var b=wall.Bounds;List<Part> parts;Matrix4x4 placement;
                if(wall.Brick){int x=Mathf.FloorToInt(b.X/16)%4,z=Mathf.FloorToInt(b.Y/16)%4;parts=brickParts[x+","+z];placement=Matrix4x4.TRS(World(Mathf.Floor(b.X/64)*64+32,Mathf.Floor(b.Y/64)*64+32,.415f-.79094963f),Quaternion.Euler(0,180,0),Vector3.one);}
                else if(wall.Type=="steel"){parts=steelParts;placement=Matrix4x4.TRS(World(b.X+b.W/2,b.Y+b.H/2),Quaternion.Euler(0,180,0),new Vector3(b.W/32,1,b.H/32));}
                else if(wall.Type=="jungle"){parts=bushParts;placement=Matrix4x4.TRS(World(b.X+b.W/2,b.Y+b.H/2,-.015f),Quaternion.Euler(0,180,0),new Vector3(b.W/64,1.1f,b.H/64));}
                else continue;
                foreach(var part in parts){if(!grouped.TryGetValue(part,out var matrices)){matrices=new List<Matrix4x4>();grouped.Add(part,matrices);}matrices.Add(placement*part.Local);}
            }
            foreach(var kv in grouped)for(int i=0;i<kv.Value.Count;i+=1023)batches.Add(new Batch{Part=kv.Key,Matrices=kv.Value.Skip(i).Take(1023).ToArray()});
            weather?.ConfigureSurfaces(Simulation.Terrain,Simulation.Width/64f,Simulation.Height/64f);
            terrainDirty=false;
        }
        private void SetupLighting()
        {
            gameCamera=GetComponentInChildren<Camera>();if(!gameCamera){var g=tvReplay?new GameObject("TV replay camera",typeof(Camera)):new GameObject("Battle camera",typeof(Camera),typeof(AudioListener));g.transform.SetParent(transform);gameCamera=g.GetComponent<Camera>();if(!tvReplay)g.tag="MainCamera";}
            gameCamera.orthographic=true;gameCamera.nearClipPlane=.1f;gameCamera.farClipPlane=120;gameCamera.backgroundColor=new Color(.08f,.1f,.12f);gameCamera.clearFlags=CameraClearFlags.SolidColor;
            sun=new GameObject("Sun",typeof(Light)).GetComponent<Light>();sun.transform.SetParent(transform);sun.type=LightType.Directional;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(55,-35,0);sun.color=new Color(1,.94f,.82f);
            if(!tvReplay){RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.65f,.75f,.85f);RenderSettings.ambientEquatorColor=new Color(.32f,.4f,.46f);RenderSettings.ambientGroundColor=new Color(.22f,.19f,.14f);}
            reflections=new GameObject("Stage reflections",typeof(ReflectionProbe)).GetComponent<ReflectionProbe>();reflections.transform.SetParent(transform);reflections.transform.position=new Vector3(6.5f,4,-6.5f);reflections.size=new Vector3(50,20,50);reflections.mode=ReflectionProbeMode.Realtime;reflections.refreshMode=ReflectionProbeRefreshMode.ViaScripting;reflections.resolution=128;reflections.clearFlags=ReflectionProbeClearFlags.SolidColor;reflections.backgroundColor=new Color(.4f,.5f,.6f);
            if(tvReplay){gameCamera.targetTexture=tvReplayTarget;gameCamera.aspect=(float)tvReplayTarget.width/tvReplayTarget.height;gameCamera.cullingMask=1<<TvReplayLayer;sun.cullingMask=reflections.cullingMask=1<<TvReplayLayer;}SetupBattlePostProcessing();
        }
        private void Update()
        {
            if(Simulation==null)return;float dt=Mathf.Min(Time.unscaledDeltaTime,.1f);fps=Mathf.Lerp(fps,1/Mathf.Max(.0001f,Time.unscaledDeltaTime),.05f);
            if(!tvReplay){UpdateGamepadControls();
            if(debug.WasPressedThisFrame()&&!IsReplaying)showDebug=!showDebug;if(restart.WasPressedThisFrame()&&!IsOnline)RequestBattleRestart();if(overhead.WasPressedThisFrame())CameraElevation=CameraElevation>85?70:90;
            if(Multiplayer.BattleSession.Instance&&Multiplayer.BattleSession.Instance.Lobby.Visible)paused=true;
            UpdateDayNightShortcuts(dt);
            for(int i=0;i<slots.Length;i++)if(slots[i].WasPressedThisFrame()&&!paused)UsePowerupSlot(i);
            SamplePrimaryFire(dt);}
            if(IsReplaying)TickReplay(dt);
            else if(!paused&&!consumePending&&!BlockCombat&&ReplayReady)
            {
                secondaryQueued|=secondaryFire.WasPressedThisFrame();
                if(secondarySelect.WasPressedThisFrame())CycleSecondary();
                accumulator+=dt;var cmd=new Command{Move=PlayerMove(),Aim=PlayerAim()};
                Simulation.DisableEnemyFire=!EnemyFire;
                if(IsOnline)accumulator=0;
                while(accumulator>=BattleSimulation.StepSeconds){QueuePrimaryCommand(ref cmd);cmd.SecondaryFire=secondaryQueued;secondaryQueued=false;StepRecorded(cmd);accumulator-=BattleSimulation.StepSeconds;}
            }
            else secondaryQueued=false;
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
            SyncMines();
            SyncTurrets(!IsOnline&&(paused||consumePending)?0:dt);
            SyncDrones();
            SyncLandDrones();
            if(terrainDirty)RebuildTerrain();
            foreach(var batch in batches)Graphics.DrawMeshInstanced(batch.Part.Mesh,batch.Part.Submesh,batch.Part.Material,batch.Matrices,batch.Matrices.Length,null,ShadowCastingMode.On,true,tvReplay?TvReplayLayer:0,tvReplay?gameCamera:null);
            float presentationDt=!IsOnline&&(paused||consumePending)?0:dt;
            foreach(var surface in terrainSurfaces)surface.Tick(presentationDt);
            SyncActors(presentationDt);effects.Tick(presentationDt,gameCamera);debris.Tick(presentationDt,tvReplay?gameCamera:null,tvReplay?TvReplayLayer:0);UpdateLighting(dt);UpdateCamera(dt);if(tvReplay)IsolateTvReplayVisuals();
        }
        private void UpdateLighting(float dt)
        {
            weather.Tick(paused?0:dt,Simulation.Width/64f,Simulation.Height/64f,sun,reflections);
            if(eagleTint)eagleTint.SetDarkness(weather.Darkness);
            foreach(var actor in actors.Values)foreach(var light in actor.Lights)light.intensity=Headlights?weather.Darkness*9:0;
        }
        private void UpdateCamera(float dt)
        {
            float screenHeight=Mathf.Max(1,Screen.height);
            float gameplayHeight=Mathf.Max(1,screenHeight-BattleHud.TopHeightPixels);
            gameCamera.rect=tvReplay?new Rect(0,0,1,1):new Rect(0,0,1,gameplayHeight/screenHeight);
            var player=Simulation.Player;
            if(ChaseCamera&&!IsOnline&&player!=null)
            {
                var forward=Quaternion.Euler(0,(int)player.Aim*90,0)*Vector3.forward;
                var position=World(player.X,player.Y,ChaseCameraHeight)-forward*ChaseCameraDistance;
                var sight=World(player.X,player.Y,.48f)+forward*3.5f;
                gameCamera.orthographic=false;
                gameCamera.fieldOfView=65;
                gameCamera.nearClipPlane=.05f;
                gameCamera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(sight-position,Vector3.up));
                return;
            }
            gameCamera.orthographic=true;
            gameCamera.nearClipPlane=.1f;
            if(AutomaticCamera)
            {
                float w=Simulation.Width/64f,h=Simulation.Height/64f;
                float span=Mathf.Min(w+.9f,(h+.9f)*gameCamera.aspect*Mathf.Sin(CameraElevation*Mathf.Deg2Rad));
                gameCamera.orthographicSize=span/gameCamera.aspect/2/(Simulation.ZoomOut>0?.75f:Zoom);
                float halfX=gameCamera.orthographicSize*gameCamera.aspect,halfZ=gameCamera.orthographicSize/Mathf.Sin(CameraElevation*Mathf.Deg2Rad);
                var p=Simulation.Player;Vector3 desired=p!=null?World(p.X,p.Y):World(Simulation.Width/2,Simulation.Height/2);
                desired.x=halfX*2>=w?w/2:Mathf.Clamp(desired.x,halfX-.3f,w-halfX+.3f);desired.z=halfZ*2>=h?-h/2:Mathf.Clamp(desired.z,-h+halfZ-.3f,-halfZ+.3f);
                cameraTarget=Vector3.Lerp(cameraTarget,desired,1-Mathf.Exp(-dt*5));
            }
            else if(Mouse.current!=null)
            {
                if(Mouse.current.middleButton.isPressed){var d=Mouse.current.delta.ReadValue();cameraTarget+=new Vector3(-d.x,0,-d.y)*.012f;}
                Zoom=Mathf.Clamp(Zoom+Mouse.current.scroll.ReadValue().y*.001f,.7f,2);gameCamera.orthographicSize=7/Zoom;
            }
            trauma=Mathf.Max(0,trauma-dt);Vector3 shake=CameraShake?new Vector3(Mathf.Sin(Time.unscaledTime*71),0,Mathf.Sin(Time.unscaledTime*83))*trauma*.2f:Vector3.zero;
            var angle=CameraElevation*Mathf.Deg2Rad;var target=cameraTarget+shake;
            gameCamera.transform.position=target+new Vector3(0,Mathf.Sin(angle)*22,-Mathf.Cos(angle)*22);gameCamera.transform.LookAt(target,Vector3.up);
        }
        private void Shadows(GameObject g)
        {foreach(var r in g.GetComponentsInChildren<Renderer>()){r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;}}
        private Actor CreateTank(TankState t)
        {
            var a=new Actor{Root=new GameObject((t.Player?"Player":"Enemy")+" "+t.Id),Tier=t.Tier,Drop=t.Drop};a.Root.transform.SetParent(actorsRoot);
            var model=!t.Player&&t.Tier==1&&SpeedEnemyModel?SpeedEnemyModel:TankModels[t.Tier];
            a.Model=Instantiate(model,a.Root.transform);a.Model.transform.localScale=Vector3.one*.86f;Shadows(a.Model);
            string tint=TankTint(t);
            ColorUtility.TryParseHtmlString(tint,out a.Color);
            foreach(var renderer in a.Model.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterials=renderer.sharedMaterials.Select(source=>{
                    if(source.name!="PrimaryPaint"&&source.name!="SecondaryArmor")return source;
                    string key=source.GetEntityId()+tint;
                    if(!tankPaint.TryGetValue(key,out var material)){material=new Material(source);ColorUtility.TryParseHtmlString(tint,out var color);if(source.name=="SecondaryArmor")color*=.68f;color.a=1;material.color=color;tankPaint.Add(key,material);ownedMaterials.Add(material);}
                    return material;
                }).ToArray();
            }
            a.Turret=a.Model.GetComponentsInChildren<Transform>(true).FirstOrDefault(n=>n.name=="TurretPivot");
            var muzzleNodes=a.Model.GetComponentsInChildren<Transform>(true);
            a.Muzzles=muzzleNodes.Where(n=>n.name=="MuzzleLeft"||n.name=="MuzzleRight").ToArray();
            if(a.Muzzles.Length==0)a.Muzzles=muzzleNodes.Where(n=>n.name=="Muzzle").ToArray();
            a.Lights=new Light[2];for(int i=0;i<2;i++){var l=new GameObject("Headlight",typeof(Light)).GetComponent<Light>();l.transform.SetParent(a.Model.transform);l.transform.localPosition=new Vector3(i==0?-.27f:.27f,.3f,.62f);l.transform.localRotation=Quaternion.Euler(12,0,0);l.type=LightType.Spot;l.range=6;l.spotAngle=55;l.innerSpotAngle=28;l.color=new Color(1,.92f,.65f);l.shadows=LightShadows.Soft;l.shadowStrength=1;l.shadowBias=.02f;l.shadowNormalBias=.1f;l.shadowNearPlane=.05f;a.Lights[i]=l;}
            var dustGo=new GameObject("Tank trail dust",typeof(ParticleSystem));dustGo.transform.SetParent(a.Root.transform);dustGo.transform.localPosition=new Vector3(0,.06f,-.52f);a.Dust=dustGo.GetComponent<ParticleSystem>();a.Dust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=a.Dust.main;main.duration=2.5f;main.loop=true;main.startLifetime=new ParticleSystem.MinMaxCurve(.8f,1.4f);main.startSpeed=new ParticleSystem.MinMaxCurve(.03f,.12f);main.startSize=new ParticleSystem.MinMaxCurve(.24f,.5f);main.startRotation=new ParticleSystem.MinMaxCurve(-Mathf.PI,Mathf.PI);main.startColor=new ParticleSystem.MinMaxGradient(new Color(.67f,.53f,.34f,.46f),new Color(.82f,.72f,.52f,.34f));main.maxParticles=140;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var shape=a.Dust.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(.78f,.05f,.24f);shape.randomDirectionAmount=.8f;
            var velocity=a.Dust.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=new ParticleSystem.MinMaxCurve(-.16f,.16f);velocity.y=new ParticleSystem.MinMaxCurve(.035f,.14f);velocity.z=new ParticleSystem.MinMaxCurve(-.16f,.16f);
            var size=a.Dust.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.5f),new Keyframe(.22f,1.05f),new Keyframe(1,1.35f)));
            var noise=a.Dust.noise;noise.enabled=true;noise.strength=.16f;noise.frequency=.45f;noise.scrollSpeed=.18f;noise.damping=true;
            var color=a.Dust.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(.88f,.78f,.58f),0),new GradientColorKey(new Color(.54f,.45f,.34f),1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.58f,.08f),new GradientAlphaKey(.3f,.48f),new GradientAlphaKey(0,1)});color.color=gradient;
            var dustRenderer=a.Dust.GetComponent<ParticleSystemRenderer>();dustRenderer.sharedMaterial=particleMaterial;dustRenderer.renderMode=ParticleSystemRenderMode.Billboard;dustRenderer.sortingFudge=2;dustRenderer.maxParticleSize=.35f;
            a.Dust.Play();a.Animation=a.Model.AddComponent<TankAnimation>();a.Animation.Initialize();a.Shield=a.Root.AddComponent<TankShield>();return a;
        }
        private void SyncActors(float dt)
        {
            var living=new HashSet<int>();
            foreach(var t in Simulation.Tanks.Where(t=>t.Alive))
            {
                living.Add(t.Id);if(actors.TryGetValue(t.Id,out var old)&&(old.Tier!=t.Tier||old.Drop!=t.Drop)){Destroy(old.Root);actors.Remove(t.Id);}if(!actors.TryGetValue(t.Id,out var a)){a=CreateTank(t);actors.Add(t.Id,a);}
                var targetPosition=World(t.X,t.Y,-t.SinkDepth*.43f);
                a.Root.transform.position=IsOnline&&!NetworkMatch.Object.HasStateAuthority&&Vector3.Distance(a.Root.transform.position,targetPosition)<2?Vector3.Lerp(a.Root.transform.position,targetPosition,1-Mathf.Exp(-dt*24)):targetPosition;a.Model.transform.rotation=Quaternion.Slerp(a.Model.transform.rotation,Quaternion.Euler(0,(int)t.Direction*90,0),1-Mathf.Exp(-dt*22));
                if(a.Turret)a.Turret.rotation=Quaternion.Slerp(a.Turret.rotation,Quaternion.Euler(0,(int)t.Aim*90,0),1-Mathf.Exp(-dt*16));
                bool local=t==Simulation.Player;
                a.Animation.Tick(t,dt);a.Shield.Tick(t.Shield,dt);
                effects.HealthBar(World(t.X,t.Y,.4f)+gameCamera.transform.up*.64f,t.Health,t.MaxHealth,a.Color,gameCamera);
                float charge=IsOnline&&t.Player&&t.Slot>=0&&t.Slot!=NetworkMatch.LocalSlot?
                    NetworkMatch.ChargeProgress[t.Slot]:primaryCharge.IsCharging&&local?primaryCharge.Progress:0;
                if(local)effects.CooldownBar(World(t.X,t.Y,.4f)+gameCamera.transform.up*.575f,charge>0?1-charge:t.ReloadProgress,gameCamera,charge>0);
                if(t.Player&&charge>0)foreach(var muzzle in a.Muzzles)effects.ProjectileGlow(muzzle.position,a.Color,true,gameCamera,.35f+charge*.65f);
                var travel=Quaternion.Euler(0,(int)t.Direction*90,0)*Vector3.forward;a.Dust.transform.localPosition=-travel*.52f+Vector3.up*.06f;a.Dust.transform.localRotation=Quaternion.Euler(0,(int)t.Direction*90,0);var emission=a.Dust.emission;emission.rateOverTime=Dust&&t.Moving&&!t.InQuicksand?45:0;
            }
            foreach(var id in actors.Keys.Where(id=>!living.Contains(id)).ToArray()){Destroy(actors[id].Root);actors.Remove(id);}
            SyncProjectiles(dt);
        }
        private Transform MinePart(Transform parent,PrimitiveType primitive,string label,Vector3 position,Vector3 scale,Material material)
        {
            var part=GameObject.CreatePrimitive(primitive);part.name=label;part.transform.SetParent(parent,false);part.transform.localPosition=position;part.transform.localScale=scale;
            Destroy(part.GetComponent<Collider>());part.GetComponent<Renderer>().sharedMaterial=material;return part.transform;
        }
        private void SyncMines()
        {
            foreach(var mine in Simulation.Mines)
            {
                if(!mines.TryGetValue(mine.Id,out var visual))
                {
                    visual=new MineVisual{Root=new GameObject("Buried mine "+mine.Id),Dirt=new Transform[8]};visual.Root.transform.SetParent(actorsRoot,false);visual.Root.transform.position=World(mine.X,mine.Y);
                    if(!MineModel)throw new InvalidOperationException("Arcade mine GLB is not assigned");
                    visual.Disc=new GameObject("Arcade mine visual").transform;visual.Disc.SetParent(visual.Root.transform,false);
                    var model=Instantiate(MineModel,visual.Disc);model.name="Arcade mine";model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;model.transform.localScale=Vector3.one*.62f;Shadows(model);TintDeployable(model,mine.OwnerSlot);
                    visual.Indicator=model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Red blinking indicator");
                    for(int i=0;i<8;i++)visual.Dirt[i]=MinePart(visual.Root.transform,PrimitiveType.Cube,"Excavated earth",Vector3.zero,new Vector3(.07f,.035f,.06f),mineDirt);
                    mines.Add(mine.Id,visual);
                }
                float bury=Mathf.Clamp01(mine.Age/BattleSimulation.MineArmSeconds);
                visual.Disc.localPosition=new Vector3(0,Mathf.Lerp(.16f,-.07f,bury),0);
                visual.Disc.localRotation=Quaternion.Euler(0,bury*240,0);
                if(visual.Indicator)
                {
                    visual.Indicator.gameObject.SetActive(mine.Armed);
                    visual.Indicator.localScale=Vector3.one*(.82f+.18f*(.5f+.5f*Mathf.Sin(mine.Age*10)));
                }
                for(int i=0;i<8;i++)
                {
                    float angle=i*Mathf.PI/4,radius=Mathf.Lerp(.09f,.24f,bury);
                    visual.Dirt[i].localPosition=new Vector3(Mathf.Cos(angle)*radius,.012f+Mathf.Sin(bury*Mathf.PI)*.15f,Mathf.Sin(angle)*radius);
                    visual.Dirt[i].localRotation=Quaternion.Euler(0,i*47+bury*90,0);
                }
            }
            foreach(var id in mines.Keys.Where(id=>!Simulation.Mines.Any(m=>m.Id==id)).ToArray()){Destroy(mines[id].Root);mines.Remove(id);}
        }
        private void SyncTurrets(float dt)
        {
            foreach(var state in Simulation.Turrets)
            {
                if(!turrets.TryGetValue(state.Id,out var view))
                {
                    var root=Instantiate(GroundTurretPrefab,World(state.X,state.Y),Quaternion.identity,actorsRoot);
                    root.name="Ground turret "+state.Id;view=root.GetComponent<GroundTurret>();view.Initialize();TintDeployable(root,state.OwnerSlot);turrets.Add(state.Id,view);
                }
                view.transform.position=World(state.X,state.Y);
                view.Tick(state,dt);
            }
            foreach(var id in turrets.Keys.Where(id=>!Simulation.Turrets.Any(t=>t.Id==id)).ToArray()){Destroy(turrets[id].gameObject);turrets.Remove(id);}
        }
        private void CycleSecondary()
        {
            if(IsOnline){onlineSecondary=(SecondaryAttack)((int)onlineSecondary%(int)SecondaryAttack.LandDrone+1);return;}
            switch(Simulation.EquippedSecondary)
            {
                case SecondaryAttack.Mine:Simulation.EquippedSecondary=SecondaryAttack.PatrolDrone;break;
                case SecondaryAttack.PatrolDrone:Simulation.EquippedSecondary=SecondaryAttack.GroundTurret;break;
                case SecondaryAttack.GroundTurret:Simulation.EquippedSecondary=SecondaryAttack.LandDrone;break;
                default:Simulation.EquippedSecondary=SecondaryAttack.Mine;break;
            }
        }
        private void SyncDrones()
        {
            foreach(var drone in Simulation.Drones)
            {
                if(!drones.TryGetValue(drone.Id,out var visual))
                {
                    visual=new DroneVisual{Root=new GameObject("Patrol drone "+drone.Id),LastPosition=new Vector2(drone.X,drone.Y)};
                    visual.Root.transform.SetParent(actorsRoot,false);
                    if(!DroneModel)throw new InvalidOperationException("Wing drone GLB is not assigned");
                    visual.Body=new GameObject("Wing drone visual").transform;visual.Body.SetParent(visual.Root.transform,false);
                    var model=Instantiate(DroneModel,visual.Body);model.name="Wing drone";model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;model.transform.localScale=Vector3.one*.48f;Shadows(model);TintDeployable(model,drone.OwnerSlot);
                    visual.Rotors=model.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Rotor_")&&t.parent&&t.parent.name=="HoverBody").OrderBy(t=>t.name).ToArray();
                    if(visual.Rotors.Length!=4)throw new InvalidOperationException("Wing drone must import four HoverBody rotor pivots");
                    var boundary=new GameObject("Patrol boundary",typeof(LineRenderer));boundary.transform.SetParent(visual.Root.transform,false);
                    var line=boundary.GetComponent<LineRenderer>();line.sharedMaterial=droneRange;line.startColor=line.endColor=PlayerColor(drone.OwnerSlot);line.useWorldSpace=true;line.loop=true;line.positionCount=64;line.startWidth=line.endWidth=.012f;line.shadowCastingMode=ShadowCastingMode.Off;
                    for(int i=0;i<64;i++){float angle=i*Mathf.PI*2/64;line.SetPosition(i,World(drone.AnchorX+Mathf.Cos(angle)*BattleSimulation.DronePatrolRadius,drone.AnchorY+Mathf.Sin(angle)*BattleSimulation.DronePatrolRadius,.035f));}
                    drones.Add(drone.Id,visual);
                }
                float height=Mathf.Lerp(.08f,.65f,Mathf.Clamp01(drone.Age))+.035f*Mathf.Sin(drone.Age*4);
                if(drone.TargetId!=0){var target=Simulation.Tanks.Find(t=>t.Id==drone.TargetId);if(target!=null){float distance=Vector2.Distance(new Vector2(drone.X,drone.Y),new Vector2(target.X,target.Y));height=Mathf.Lerp(.18f,.65f,Mathf.Clamp01(distance/60));}}
                var currentPosition=new Vector2(drone.X,drone.Y);
                var travel=currentPosition-visual.LastPosition;
                if(travel.sqrMagnitude>.0001f)
                {
                    var forward=World(travel.x,travel.y);
                    forward.y=0;
                    visual.Body.rotation=Quaternion.LookRotation(forward);
                    visual.LastPosition=currentPosition;
                }
                visual.Body.position=World(drone.X,drone.Y,height);
                for(int i=0;i<4;i++)visual.Rotors[i].localRotation=Quaternion.Euler(0,drone.Age*1700*(i%2==0?1:-1),0);
            }
            foreach(var id in drones.Keys.Where(id=>!Simulation.Drones.Any(d=>d.Id==id)).ToArray()){Destroy(drones[id].Root);drones.Remove(id);}
        }
        private void OnGUI()
        {
            if(Simulation==null||tvReplay)return;
            hud.SetVisible(true);
            hud.Draw(Simulation,IsOnline||IsReplaying?null:economy,PowerupAtlas,consumePending,selectedPowerup,!IsReplaying);
            if(IsReplaying)return;
            if(Multiplayer.BattleSession.Instance&&Multiplayer.BattleSession.Instance.Lobby.Visible)return;
            if(ChaseCamera&&!IsOnline&&Simulation.Player!=null&&!paused&&!showDebug)
            {
                float centerX=Screen.width*.5f;
                float centerY=Screen.height-(Screen.height-BattleHud.TopHeightPixels)*.5f;
                var oldColor=GUI.color;GUI.color=new Color(1,.95f,.72f,.9f);
                GUI.DrawTexture(new Rect(centerX-9,centerY-1,6,2),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(centerX+3,centerY-1,6,2),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(centerX-1,centerY-9,2,6),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(centerX-1,centerY+3,2,6),Texture2D.whiteTexture);
                GUI.color=oldColor;
            }
            if(!HasTouchControls)
            {
            float secondaryBottom=Psg1?132:96;
            var secondaryRect=new Rect(12,Screen.height-secondaryBottom,156,58);
            string secondaryStatus=Simulation.SecondaryCooldown>0?Simulation.SecondaryCooldown.ToString("0.0")+"s":Simulation.SecondaryCount>=Simulation.SecondaryLimit?"LIMIT":"READY";
            string secondaryName=Simulation.EquippedSecondary==SecondaryAttack.LandDrone?"LAND DRONE":Simulation.EquippedSecondary==SecondaryAttack.PatrolDrone?"DRONE":Simulation.EquippedSecondary==SecondaryAttack.GroundTurret?"TURRET":"MINE";
            if(GUI.Button(new Rect(12,Screen.height-secondaryBottom-30,156,26),(Psg1?"[Y] Switch: ":"[Q] Switch: ")+secondaryName))CycleSecondary();
            bool previousEnabled=GUI.enabled;GUI.enabled=!paused&&!consumePending&&Simulation.CanUseSecondary;
            if(GUI.Button(secondaryRect,secondaryName+(Psg1?"  [B]\n":"  [E / RMB]\n")+secondaryStatus+"   "+Simulation.SecondaryCount+"/"+Simulation.SecondaryLimit+" deployed"))secondaryQueued=true;
            GUI.enabled=previousEnabled;
            if(!string.IsNullOrEmpty(Simulation.SecondaryStatus))GUI.Label(new Rect(180,Screen.height-secondaryBottom+2,Screen.width-200,44),Simulation.SecondaryStatus);
            }
            if(economy&&showDebug)GUI.Label(new Rect(12,Screen.height-84,650,24),consumePending?"Confirming power-upÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦":economy.Status);
            if(Simulation.PickupType!=null){var screen=gameCamera.WorldToScreenPoint(World(Simulation.PickupX,Simulation.PickupY,.7f));float size=Mathf.Clamp(Vector3.Distance(gameCamera.WorldToScreenPoint(World(Simulation.PickupX+67,Simulation.PickupY,.7f)),screen),96,144);var r=new Rect(screen.x-size/2,Screen.height-screen.y-size/2,size,size);if(PowerupAtlas&&BattleHud.TryPowerupUv(Simulation.PickupType,out var uv))GUI.DrawTextureWithTexCoords(r,PowerupAtlas,uv);else GUI.Box(r,Simulation.PickupType);pickupSparkles.Draw(r,Simulation.Tick*BattleSimulation.StepSeconds);}
            if(!HasTouchControls&&!Psg1)GUI.Label(new Rect(12,Screen.height-28,Screen.width-24,24),"WASD drive   ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¢   Arrows aim   ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¢   Space: tap / hold + release   ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¢   P pause   ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¢   R restart   ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¢   C camera");
            DrawPsg1Controls();
            if(!HasTouchControls&&!Psg1&&!IsOnline&&(paused||Simulation.Lost||Simulation.Won)){GUI.Box(new Rect(Screen.width/2-130,Screen.height/2-35,260,70),(paused?"PAUSED":Simulation.Won?"STAGE CLEAR":"GAME OVER")+"\nR: Restart");if(Simulation.Won&&Stage<35&&GUI.Button(new Rect(Screen.width/2-65,Screen.height/2+45,130,30),"Next stage"))LoadStage(Stage+1);}
            if(IsOnline||!showDebug||RuntimePlatformInfo.IsPsg1)return;
            GUILayout.BeginArea(new Rect(Mathf.Max(8,Screen.width-282),92,270,Mathf.Max(120,Screen.height-132)),"Battle Cities ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¢ Debug",GUI.skin.window);
            debugScroll=GUILayout.BeginScrollView(debugScroll);
            ChaseCamera=GUILayout.Toggle(ChaseCamera,"Chase shooting camera (arrows aim, LMB / Space fire)");
            if(ChaseCamera)
            {
                GUILayout.Label("Camera height "+ChaseCameraHeight.ToString("F2"));ChaseCameraHeight=GUILayout.HorizontalSlider(ChaseCameraHeight,.9f,2.4f);
                GUILayout.Label("Follow distance "+ChaseCameraDistance.ToString("F2"));ChaseCameraDistance=GUILayout.HorizontalSlider(ChaseCameraDistance,1.2f,4f);
            }
            GUILayout.Label("Stage "+requestedStage);requestedStage=Mathf.RoundToInt(GUILayout.HorizontalSlider(requestedStage,1,35));if(GUILayout.Button("Load stage "+requestedStage))LoadStage(requestedStage);
            weather.Cycle=GUILayout.Toggle(weather.Cycle,"Day/night cycle");weather.Rain=GUILayout.Toggle(weather.Rain,"Rain");weather.Clouds=GUILayout.Toggle(weather.Clouds,"Drifting clouds");GUILayout.Label("Time of day");weather.TimeOfDay=GUILayout.HorizontalSlider(weather.TimeOfDay,0,1);GUILayout.Label("Rain intensity");weather.RainIntensity=GUILayout.HorizontalSlider(weather.RainIntensity,0,1);Headlights=GUILayout.Toggle(Headlights,"Tank headlights");Dust=GUILayout.Toggle(Dust,"Tank trail dust");EnemyFire=GUILayout.Toggle(EnemyFire,"Enemy shooting");
            AutomaticCamera=GUILayout.Toggle(AutomaticCamera,"Automatic camera");CameraShake=GUILayout.Toggle(CameraShake,"Camera shake");GUILayout.Label("Elevation "+CameraElevation.ToString("F0")+"Ãƒâ€šÃ‚Â°");CameraElevation=GUILayout.HorizontalSlider(CameraElevation,40,89.9f);GUILayout.Label("Zoom");Zoom=GUILayout.HorizontalSlider(Zoom,.7f,2);
            GUILayout.Label("FPS "+fps.ToString("F0")+" ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¢ Terrain batches "+batches.Count);GUILayout.Label("Manual camera: middle-drag / wheel");DrawDebugPowerups();GUILayout.EndScrollView();GUILayout.EndArea();
        }
        private void DrawDebugPowerups()
        {
            GUILayout.Space(10);GUILayout.Label("SECONDARY ATTACK");
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Equip mine"))Simulation.EquippedSecondary=SecondaryAttack.Mine;
            if(GUILayout.Button("Equip drone"))Simulation.EquippedSecondary=SecondaryAttack.PatrolDrone;
            if(GUILayout.Button("Equip turret"))Simulation.EquippedSecondary=SecondaryAttack.GroundTurret;
            GUILayout.EndHorizontal();
            if(GUILayout.Button("Equip land attack drone"))Simulation.EquippedSecondary=SecondaryAttack.LandDrone;
            if(GUILayout.Button("Deploy land drone for testing")){Simulation.EquippedSecondary=SecondaryAttack.LandDrone;secondaryQueued=true;}
            GUILayout.Label("Land drone: 1 active / 9 tiles / 60 seconds");
            GUILayout.Label("Drone: 2-tile radius / 45 seconds");
            GUILayout.Label("Turret: mine-sized / move away to deploy");
            if(GUILayout.Button("Place turret for testing")){Simulation.EquippedSecondary=SecondaryAttack.GroundTurret;secondaryQueued=true;}
            GUILayout.Space(10);
            GUILayout.Label("TEST POWER-UPS");
            GUILayout.Label("Click to consume instantly (free)");
            bool enabled=GUI.enabled;
            GUI.enabled=enabled&&Simulation.Player!=null&&!Simulation.Lost&&!Simulation.Won&&!consumePending;
            for(int row=0;row<debugPowerupTypes.Length;row+=2)
            {
                GUILayout.BeginHorizontal();
                for(int i=row;i<Math.Min(row+2,debugPowerupTypes.Length);i++)
                    if(GUILayout.Button(debugPowerupNames[i],GUILayout.Height(30)))DebugConsumePowerup(debugPowerupTypes[i]);
                GUILayout.EndHorizontal();
            }
            GUI.enabled=enabled;
            GUILayout.Label(debugPowerupStatus);
            if(Simulation.Player!=null)GUILayout.Label("Tank tier: "+(Simulation.Player.Tier+1)+" / 4");
        }
        public void DebugConsumePowerup(string type)
        {
            if(IsReplaying||IsOnline||Simulation==null||Simulation.Player==null||Simulation.Lost||Simulation.Won||consumePending)return;
            int index=Array.IndexOf(debugPowerupTypes,type);if(index<0)return;
            if(replayRecorder!=null)replayRecorder.Data.debugUsed=true;
            QueueReplayEvent(new ReplayEvent{kind="powerup",value=type});
            debugPowerupStatus="Applied: "+debugPowerupNames[index]+(paused?" (paused)":"");
        }
        private void OnApplicationFocus(bool focus){if(!focus){if(!IsOnline)paused=true;CancelTouchGameplay();}}
        private async void RollDrop(BattleSimulation current)
        {
            if(IsReplaying)return;
            if(IsOnline){if(NetworkMatch.Object.HasStateAuthority)QueueReplayEvent(new ReplayEvent{kind="pickup"});return;}
            var drop=economy?await economy.Roll(Stage):null;
            if(this&&Simulation==current&&!IsReplaying)
            {var e=new ReplayEvent{kind="pickup",value=drop?.Type};if(drop?.ClaimId!=null)pendingReplayClaims[e]=drop.ClaimId;QueueReplayEvent(e);}
        }
        public void UsePowerupSlot(int index)
        {
            if(!ReplayReady||IsReplaying||IsOnline||index<0||index>=4||paused||showDebug||consumePending||!economy||!economy.Authenticated||economy.SlotCount(index)<=0||Simulation==null||!Simulation.CanAcceptPlayerFire)return;
            consumePending=true;_ = ConsumePowerupSlot(index);
        }
        private async System.Threading.Tasks.Task ConsumePowerupSlot(int index)
        {
            var current=Simulation;try{var type=await economy.ConsumeSlot(index);if(this&&Simulation==current&&!IsReplaying&&type!=null)QueueReplayEvent(new ReplayEvent{kind="powerup",value=type});}finally{consumePending=false;}
        }
        private void OnDestroy(){
#if UNITY_EDITOR
            if(authoringPreview)return;
#endif
            RestoreTvReplayMasks();FinishRecording();hud.Dispose();pickupSparkles?.Dispose();input?.Dispose();if(particleMaterial&&particleMaterial.mainTexture)Destroy(particleMaterial.mainTexture);foreach(var m in ownedMaterials)if(m)Destroy(m);foreach(var m in ownedMeshes)if(m)Destroy(m);}
    }
}
