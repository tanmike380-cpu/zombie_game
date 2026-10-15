using UnityEngine;

namespace ZombieGame.Tests.V010Defense
{
    /// <summary>Editor-visible frozen environment and serialized Terrain resource retention; never a second runtime map.</summary>
    public sealed class V010SceneReference : MonoBehaviour
    {
        public GameObject reference;
        private void Awake(){if(reference)reference.SetActive(false);}
    }
}
