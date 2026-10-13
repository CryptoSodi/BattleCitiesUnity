using System;
using System.Globalization;
namespace BattleCities.Core
{
    // Explicit outcomes do not consume the simulation PRNG or contact a network during playback.
    public static class MagicDropEvent
    {
        public static bool Valid(string value,int width,int height)
        {
            try { Parse(value,width,height,out _,out _,out _);return true; }catch(FormatException){return false;}
        }
        static void Parse(string value,int width,int height,out string type,out int x,out int y)
        {
            x=0;y=0;type=null;
            var parts=(value??"").Split('|');
            if(parts.Length!=4||Array.IndexOf(ReplayJson.Powerups,parts[0])<0||parts[0].StartsWith("batc")||
                !int.TryParse(parts[1],NumberStyles.None,CultureInfo.InvariantCulture,out x)||
                !int.TryParse(parts[2],NumberStyles.None,CultureInfo.InvariantCulture,out y)||
                x<32||y<32||x>=width-32||y>=height-32||x%32!=0||y%32!=0||
                !System.Text.RegularExpressions.Regex.IsMatch(parts[3],"^[1-9A-HJ-NP-Za-km-z]{32,44}$"))throw new FormatException("Invalid MagicBlock drop.");
            type=parts[0];
        }
        public static void Apply(BattleSimulation sim,string value)
        {
            Parse(value,sim.Width,sim.Height,out string type,out int x,out int y);
            sim.PickupType=type;sim.PickupX=x;sim.PickupY=y;sim.PickupTime=30;sim.PickupClaim=null;
        }
    }
}
