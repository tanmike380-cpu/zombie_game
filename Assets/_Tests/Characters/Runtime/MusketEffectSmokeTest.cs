using System;
using System.Collections;
using UnityEngine;
using ZombieGame.Presentation;

namespace ZombieGame.CharacterTests
{
    public sealed class MusketEffectSmokeTest : MonoBehaviour
    {
        private IEnumerator Start()
        {
            using(var effects = new MusketEffects())
            {
                for(int i=0;i<400;i++)effects.fire(new Vector3(0,-100,0),Vector3.forward);
                if(effects.shot_count!=400 || effects.live_particles==0 || effects.live_particles>3000)
                    throw new InvalidOperationException("Musket particle burst/capacity regression");
                yield return new WaitForSeconds(2);
                if(effects.live_particles!=0)throw new InvalidOperationException("Musket smoke particles failed to expire");
                effects.clear();
                if(effects.shot_count!=0)throw new InvalidOperationException("Musket reset failed");
            }
            Debug.Log("[MusketEffectSmoke] PASS 400 shots share bounded emitters, smoke/flash visible particles spawn and expire, reset clears counters");
        }
    }
}
