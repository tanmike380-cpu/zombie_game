using System;
using UnityEngine;

namespace ZombieGame.Tests.V010PresentationReview
{
    /// <summary>Disposable before/after art roots inside the independent frozen environment.</summary>
    public sealed class GroundingReviewBindings : MonoBehaviour
    {
        public string source_record;
        public PresentationCapture.GroundingPair[] pairs = Array.Empty<PresentationCapture.GroundingPair>();
    }
}
