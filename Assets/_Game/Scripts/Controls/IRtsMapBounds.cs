using UnityEngine;

namespace ZombieGame.Controls
{
    /// <summary>Optional map extent. Legacy views keep the established 256m square.</summary>
    public interface IRtsMapBounds
    {
        Rect playable_bounds {get;}
    }
}
