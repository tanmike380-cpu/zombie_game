using UnityEngine;
using ZombieGame.Noise;

namespace ZombieGame.AI
{
    /// <summary>New audible emissions replace old memory. Late older waves cannot restore stale goals.</summary>
    public struct SoundMemory
    {
        public long sequence { get; private set; }
        public Vector3 origin { get; private set; }
        public bool has_memory => sequence > 0;
        public bool pending {get;private set;}
        public void finish_investigation(){pending=false;}

        public bool accept(NoiseSignal signal)
        {
            if (signal.sequence <= sequence) return false;
            sequence = signal.sequence; origin = signal.origin;pending=true;
            return true;
        }
    }
}
