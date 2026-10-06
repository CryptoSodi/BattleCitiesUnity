using System;
using UnityEngine;

namespace BattleCities.UI
{
    public sealed class OperationsArt : ScriptableObject
    {
        public Sprite[] quarters = new Sprite[7];
        // Website, X follow, Instagram, Discord, X repost, X comment.
        public Sprite[] socials = new Sprite[6];
        public Sprite[] playerTanks = new Sprite[4];
        public Sprite[] enemies = new Sprite[4];
        public Sprite cannon;
        public TextAsset manual;
    }

    [Serializable] public sealed class FieldManualEntry
    {
        public string category, slug, name, role, lore, effect, source;
    }
    [Serializable] public sealed class FieldManualEntries
    {
        public FieldManualEntry[] entries = Array.Empty<FieldManualEntry>();
    }
}
