using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace ZombieGame.Presentation
{
    /// <summary>Explicit non-combat art preview for equipment whose shared balance is not authored yet.</summary>
    public sealed class ImportedAnimationDisplay:MonoBehaviour
    {
        public AnimationClip clip;
        private PlayableGraph graph;
        private AnimationClipPlayable motion;
        private void Start()
        {
            if(clip==null)return;
            var animator=GetComponent<Animator>()??gameObject.AddComponent<Animator>();animator.enabled=true;animator.applyRootMotion=false;
            graph=PlayableGraph.Create("Imported equipment preview");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            motion=AnimationClipPlayable.Create(graph,clip);
            AnimationPlayableOutput.Create(graph,"Original authored action",animator).SetSourcePlayable(motion);graph.Play();
        }
        private void Update(){if(graph.IsValid()){motion.SetTime(Mathf.Repeat(Time.time,clip.length));graph.Evaluate(0);}}
        private void OnDestroy(){if(graph.IsValid())graph.Destroy();}
    }
}
