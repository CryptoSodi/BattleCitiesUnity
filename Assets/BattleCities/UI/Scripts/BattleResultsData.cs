using System;
using System.Linq;
using BattleCities.Core;
using UnityEngine;

namespace BattleCities.UI
{
    public sealed class BattleResultsData
    {
        public sealed class PlayerRow
        {
            public int Slot, Rank;
            public string Name;
            public bool Local;
            public BattleResultStats Stats;
        }
        public int Stage, Defeated, TotalEnemies, HighScore, Seconds, TeamKills;
        public bool Won, Pvp;
        public string Outcome;
        public PlayerRow[] Players;

        public static BattleResultsData Capture(BattleSimulation simulation, string localName, int highScore)
        {
            int local = simulation.IsMultiplayer ? simulation.LocalPlayerSlot : 0;
            var rows = Enumerable.Range(0, simulation.IsMultiplayer ? BattleSimulation.MaxPlayers : 1)
                .Where(i => !simulation.IsMultiplayer || simulation.ResultStats[i].Participated || simulation.Participants[i].Connected)
                .Select(i => new PlayerRow { Slot = i, Local = i == local, Name = i == local ? localName : "PLAYER " + (i + 1), Stats = simulation.ResultStats[i].Copy() })
                .OrderByDescending(r => simulation.IsPvp && simulation.IsWinningSlot(r.Slot))
                .ThenByDescending(r => r.Stats.Points).ThenByDescending(r => r.Stats.Kills).ThenBy(r => r.Slot).ToArray();
            for (int i = 0; i < rows.Length; i++) rows[i].Rank = i + 1;
            bool victory = simulation.IsPvp ? simulation.IsWinningSlot(local) : simulation.Won;
            return new BattleResultsData {
                Stage = simulation.Stage, Won = victory, Pvp = simulation.IsPvp,
                Outcome = simulation.IsPvp && simulation.WinnerSlot < 0 ? "DRAW" : victory ? "MISSION\nCOMPLETE" : "MISSION\nFAILED",
                Defeated = Math.Max(0, simulation.TotalEnemies - simulation.Remaining), TotalEnemies = simulation.TotalEnemies,
                HighScore = Math.Max(highScore, rows.FirstOrDefault(r => r.Local)?.Stats.Points ?? 0),
                Seconds = Mathf.FloorToInt(simulation.Tick * BattleSimulation.StepSeconds),
                TeamKills = rows.Sum(r => r.Stats.Kills), Players = rows
            };
        }

        public string TimeLabel => TimeSpan.FromSeconds(Seconds).ToString(Seconds >= 3600 ? @"h\:mm\:ss" : @"mm\:ss");
        public string ShareText => "Battle Cities — Stage " + Stage + "\n" + Outcome.Replace('\n', ' ') + " | " + TimeLabel + "\n" +
            string.Join("\n", Players.Select(p => "#" + p.Rank + " " + p.Name + " — " + p.Stats.Kills + " kills, " + p.Stats.Points.ToString("N0") + " points"));
    }
}
