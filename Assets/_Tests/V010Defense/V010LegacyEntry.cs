using System;
using System.Reflection;
using UnityEngine;
using ZombieGame.FrontierTests;

namespace ZombieGame.Tests.V010Defense
{
    /// <summary>Isolated legacy-check player selector, never attached to the playable defense scene.</summary>
    public sealed class V010LegacyEntry : MonoBehaviour
    {
        private void Awake()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-unitUiSmoke") >= 0)
            {
                GetComponent<CoastalRegressionChecks>().enabled = false;
                return;
            }
            try
            {
                var edges = typeof(ZombieRosterSmoke).GetMethod("check_camera_edges",BindingFlags.NonPublic|BindingFlags.Static);
                if(edges == null)throw new MissingMethodException("Existing edge-pan regression entry is unavailable.");
                edges.Invoke(null,null);
                Debug.Log("[V010Legacy] PASS unchanged camera edge maths including 300 sustained top/right samples; not physical desktop input.");
            }
            catch(Exception exception)
            {Debug.LogException(exception);Application.Quit(1);}
        }
    }
}
