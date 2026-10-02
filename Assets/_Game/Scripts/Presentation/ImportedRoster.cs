using System;
using UnityEngine;

namespace ZombieGame.Presentation
{
    [Serializable] public sealed class ImportedUnitArt { public string unit_id; public CharacterFrames frames; }
    /// <summary>Appearance only. Unit statistics and AI stay in their shared production systems.</summary>
    public sealed class ImportedRoster : ScriptableObject
    {
        public ImportedUnitArt[] units;
    }
}
