using UnityEngine;

namespace ZombieGame.Tests.EnvironmentShowcase
{
    /// <summary>Saved Editor reference marker. Runtime hides this group before the existing controller builds its actual preview.</summary>
    public sealed class VisionEditorReference : MonoBehaviour
    {
        public EnvironmentShowcaseConfig config;
        public Terrain terrain;
        public Camera comparison_camera;
    }
}
