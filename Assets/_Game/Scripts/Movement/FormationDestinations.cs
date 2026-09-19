using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZombieGame.Balance;

namespace ZombieGame.Movement
{
    /// <summary>Assigns compact destination slots, not paths. Native NavMesh still handles all routing/avoidance.</summary>
    public sealed class FormationDestinations
    {
        private readonly List<Vector3> slots = new List<Vector3>(400);
        private readonly List<bool> occupied = new List<bool>(400);

        public bool build(Vector3 center, List<Vector3> origins, List<Vector3> destinations)
        {
            destinations.Clear(); slots.Clear(); occupied.Clear();
            if (origins.Count == 0 || Mathf.Abs(center.x) > 127.5f || Mathf.Abs(center.z) > 127.5f
                || !NavMesh.SamplePosition(center,out var anchor,.6f,NavMesh.AllAreas)) return false;
            float spacing = UnitBalance.config.formation_spacing;
            // Expand from the click; never retain empty space between separated squads.
            for (int ring = 0; ring <= Mathf.CeilToInt(32 / spacing) && slots.Count < origins.Count; ring++)
                for (int z = -ring; z <= ring && slots.Count < origins.Count; z++)
                    for (int x = -ring; x <= ring && slots.Count < origins.Count; x++)
                    {
                        if (Mathf.Max(Mathf.Abs(x),Mathf.Abs(z)) != ring) continue;
                        Vector3 candidate = anchor.position + new Vector3(x*spacing,0,z*spacing);
                        if (Mathf.Abs(candidate.x)>127.5f || Mathf.Abs(candidate.z)>127.5f) continue;
                        if (!NavMesh.SamplePosition(candidate,out var hit,.1f,NavMesh.AllAreas)) continue;
                        slots.Add(hit.position); occupied.Add(false);
                    }
            if (slots.Count < origins.Count) return false;
            foreach (Vector3 origin in origins)
            {
                int best = -1; float distance = float.PositiveInfinity;
                for (int i = 0; i < slots.Count; i++)
                {
                    if (occupied[i]) continue;
                    float candidate = (slots[i]-origin).sqrMagnitude;
                    if (candidate < distance) { best = i; distance = candidate; }
                }
                occupied[best] = true; destinations.Add(slots[best]);
            }
            return true;
        }
    }
}
