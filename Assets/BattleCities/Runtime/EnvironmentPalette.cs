using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleCities
{
    public sealed class EnvironmentPalette : ScriptableObject
    {
        public EnvironmentPiece[] Prefabs;

        public Dictionary<string, EnvironmentPiece> Lookup()
        {
            var result = new Dictionary<string, EnvironmentPiece>(StringComparer.Ordinal);
            foreach (var prefab in Prefabs ?? Array.Empty<EnvironmentPiece>())
            {
                if (!prefab || string.IsNullOrEmpty(prefab.Key) || result.ContainsKey(prefab.Key))
                    throw new InvalidOperationException("Invalid environment palette entry");
                result.Add(prefab.Key, prefab);
            }
            return result;
        }
    }
}
