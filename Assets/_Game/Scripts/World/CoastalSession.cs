using System;
using UnityEngine;

namespace ZombieGame.World
{
    [Serializable] public sealed class CoastalSessionConfig
    {
        public int initial_humans,zombies;
        public float preparation_seconds,wave_interval_seconds;
        public int[] wave_counts;
        public static CoastalSessionConfig load()
        {
            var asset=Resources.Load<TextAsset>("CoastalSkirmish");
            if(asset==null)throw new InvalidOperationException("Missing CoastalSkirmish scenario");
            var config=JsonUtility.FromJson<CoastalSessionConfig>(asset.text);
            config.validate();return config;
        }
        public void validate()
        {
            int total=0;if(wave_counts!=null)foreach(int count in wave_counts)
            {if(count<=0)throw new InvalidOperationException("Coastal waves must have positive populations");total+=count;}
            if(initial_humans<4||initial_humans>FrontierMap.HUMAN_CAPACITY||zombies<=0||total!=zombies||
                preparation_seconds<0||wave_interval_seconds<=0)
                throw new InvalidOperationException("Invalid CoastalSkirmish population/pacing");
        }
    }
    public enum CoastalOutcome { Preparing, Playing, Won, Lost }
    /// <summary>Small-session lifecycle only; combat, orders and balance stay in existing production systems.</summary>
    public sealed class CoastalSession
    {
        public CoastalOutcome outcome {get;private set;}=CoastalOutcome.Preparing;
        public bool finished=>outcome==CoastalOutcome.Won||outcome==CoastalOutcome.Lost;
        public void begin(){if(outcome==CoastalOutcome.Preparing)outcome=CoastalOutcome.Playing;}
        public void evaluate(bool headquarters_alive,bool waves_complete,int living_enemies,bool pending_infection)
        {
            if(outcome!=CoastalOutcome.Playing)return;
            if(!headquarters_alive)outcome=CoastalOutcome.Lost;
            else if(waves_complete&&living_enemies==0&&!pending_infection)outcome=CoastalOutcome.Won;
        }
    }
}
