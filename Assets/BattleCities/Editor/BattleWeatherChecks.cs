using System;
using System.Linq;
using BattleCities.Core;
using UnityEditor;
using UnityEngine;

namespace BattleCities
{
    public static class BattleWeatherChecks
    {
        static void Check(bool value,string text){if(!value)throw new Exception("WEATHER: "+text);}
        [MenuItem("Battle Cities/Validate Weather Presentation")]
        public static void Run()
        {
            Check(EditorApplication.isPlaying,"Run in gameplay Play mode");
            var game=UnityEngine.Object.FindFirstObjectByType<BattleGame>();
            var weather=game.GetComponent<BattleWeather>();
            var sun=game.GetComponentsInChildren<Light>().First(l=>l.type==LightType.Directional);
            var probe=game.GetComponentInChildren<ReflectionProbe>();var camera=game.GetComponentInChildren<Camera>();
            float time=weather.TimeOfDay,intensity=weather.RainIntensity;
            bool cycle=weather.Cycle,rain=weather.Rain,clouds=weather.Clouds;
            try
            {
                var morning=BattleWeather.DirectionToSun(.26f);var evening=BattleWeather.DirectionToSun(.74f);
                Check(Vector3.Dot(morning,camera.transform.right)<-.4f&&Vector3.Dot(morning,camera.transform.up)>.15f,"Sun rises at the upper left in the game camera");
                Check(Vector3.Dot(evening,camera.transform.right)>.4f&&Vector3.Dot(evening,camera.transform.up)>.15f,"Sun sets at the upper right in the game camera");
                Check(BattleWeather.DirectionToSun(.5f).y>.94f,"Noon sun is overhead");
                Check(Vector3.Distance(BattleWeather.DirectionToSun(0),BattleWeather.DirectionToSun(1))<.0001f,"Sun path wraps without a jump");
                Check(Vector3.Angle(BattleWeather.DirectionToSun(.7499f),BattleWeather.DirectionToSun(.7501f))<.2f,"Dusk handoff stays continuous");
                var sim=new BattleSimulation(new MapData());sim.Terrain.Clear();sim.AddRegion("water",0,0,128,256);
                weather.ConfigureSurfaces(sim.Terrain,4,4);weather.Cycle=false;weather.TimeOfDay=.4f;weather.Rain=true;weather.RainIntensity=.8f;weather.Clouds=true;
                weather.Tick(1,4,4,sun,probe);
                Check(weather.RainCount==720&&weather.SplashCount>0&&weather.SplashCount<=BattleWeather.MaxSplashes,"Rain scales with intensity and landing pool stays bounded");
                Check(weather.WaterRippleCount>0,"Rain landing on recessed water creates ripples");
                var root=game.transform.Find("Weather visuals");
                var drops=root.Find("Falling rain").GetComponent<MeshFilter>().sharedMesh;
                var cloud=root.Find("Drifting cloud 0");var position=cloud.position;
                var vertices=drops.vertices;float age=weather.PresentationAge;int count=weather.SplashCount;
                weather.Tick(0,4,4,sun,probe);
                Check(age==weather.PresentationAge&&position==cloud.position&&count==weather.SplashCount&&vertices.SequenceEqual(drops.vertices),"Pause freezes clouds, drops and splashes");
                weather.Tick(.2f,4,4,sun,probe);Check(cloud.position!=position,"Cloud layers drift on the presentation clock");
                weather.RainIntensity=1;weather.Tick(3,4,4,sun,probe);
                Check(weather.RainCount==BattleWeather.MaxDrops&&weather.SplashCount<=BattleWeather.MaxSplashes,"Heavy rain remains capped after a large time step");
                weather.RainIntensity=0;weather.Tick(.6f,4,4,sun,probe);
                Check(weather.RainCount==0&&weather.SplashCount==0,"Rain and remaining splashes expire at zero intensity");
                weather.RainIntensity=1;weather.Tick(.3f,4,4,sun,probe);weather.Rain=false;weather.Clouds=false;weather.Tick(0,4,4,sun,probe);
                Check(weather.RainCount==0&&weather.SplashCount==0&&root.GetComponentsInChildren<MeshRenderer>().All(r=>!r.enabled),"Weather toggles clear rain and hide all cloud/shadow layers");
                weather.ClearPrecipitation();Check(weather.SplashCount==0,"Stage cleanup clears rain landings");
                foreach(var name in new[]{"BattleClouds","BattleWeatherParticles"})Check(!ShaderUtil.ShaderHasError(Resources.Load<Shader>(name)),name+" compiles");
                Debug.Log("WEATHER PASS: upper-left sunrise/upper-right sunset, overhead noon, continuous wrap, drifting clouds, rain density, water ripples, pause, caps, expiry and toggles.");
            }
            finally
            {
                weather.TimeOfDay=time;weather.RainIntensity=intensity;weather.Cycle=cycle;weather.Rain=rain;weather.Clouds=clouds;weather.ClearPrecipitation();
                weather.ConfigureSurfaces(game.Simulation.Terrain,game.Simulation.Width/64f,game.Simulation.Height/64f);
                weather.Tick(0,game.Simulation.Width/64f,game.Simulation.Height/64f,sun,probe);
            }
        }
    }
}
