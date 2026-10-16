using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieGame.Tests.V010Defense;
using ZombieGame.Tests.V010Defense.EditorTools;
using ZombieGame.World;

namespace ZombieGame.Tests.V010PresentationReview.EditorTools
{
    /// <summary>Independent presentation review; frozen V0.10 assets remain reference-only.</summary>
    public static class PresentationReviewTools
    {
        public const string ROOT = "Assets/_Tests/V010PresentationReview";
        public const string SCENE_PATH = ROOT+"/Scenes/CameraGroundingReview.unity";
        public const string SCENARIO_PATH = ROOT+"/Art/PresentationScenario.asset";
        public const string PREFAB_PATH = ROOT+"/Art/PresentationEnvironment.prefab";
        public const string EVIDENCE_ROOT = "docs/versions/V0.10/evidence/camera-grounding";
        private static readonly string[] FROZEN_PATHS = {
            V010DefenseTools.SCENE_PATH,V010DefenseTools.SCENARIO_PATH,V010DefenseTools.PREFAB_PATH,
            "balance/unit_balance.json","art/models/fortified-city/FortifiedCityWalls.fbx",
            "art/models/grid-footprints/Pasture14_GoldenCorner/Pasture14_GoldenCorner.fbx"};

        [Serializable] private sealed class IntegrityRecord {public string path,sha256_before,sha256_after;}
        [Serializable] private sealed class IntegrityReport {public string status,source;public List<IntegrityRecord> files = new List<IntegrityRecord>();}

        [MenuItem("Zombie Game/V0.10/Presentation review/Prepare independent camera review")]
        public static void prepare_camera_review_scene()
        {prepare_review_scene(null,"Original frozen baseline; grounding source pending");}

        /// <summary>Grounding importer supplies only a newly owned derived prefab, never mutates frozen baseline.</summary>
        public static void prepare_review_scene(GameObject environment,string source_record)
        {
            guard_editor();var integrity = capture_frozen_hashes();
            Directory.CreateDirectory(ROOT+"/Scenes");Directory.CreateDirectory(ROOT+"/Art");Directory.CreateDirectory(EVIDENCE_ROOT);
            var scene = EditorSceneManager.OpenScene(V010DefenseTools.SCENE_PATH);
            var game = UnityEngine.Object.FindFirstObjectByType<FrontierGame>();
            if(!game || !game.defense_scenario)throw new InvalidOperationException("Frozen scene has no production scenario.");
            var original_scenario = game.defense_scenario;
            var scenario = AssetDatabase.LoadAssetAtPath<DefenseScenario>(SCENARIO_PATH);
            if(!scenario){scenario = ScriptableObject.CreateInstance<DefenseScenario>();AssetDatabase.CreateAsset(scenario,SCENARIO_PATH);}
            EditorUtility.CopySerialized(original_scenario,scenario);
            if(environment)scenario.environment_prefab = environment;
            scenario.version = "V0.10 optional camera/grounding review, not selected gameplay camera";
            scenario.source_record += "\nArt-only independent presentation review: "+source_record;
            game.defense_scenario = scenario;EditorUtility.SetDirty(scenario);
            foreach(var check in UnityEngine.Object.FindObjectsByType<V010DefenseChecks>(FindObjectsSortMode.None))UnityEngine.Object.DestroyImmediate(check);
            foreach(var placeholder in UnityEngine.Object.FindObjectsByType<ZombieGame.Presentation.FrozenEnvironmentView>(FindObjectsSortMode.None))
                if(placeholder.gameObject.scene == scene)UnityEngine.Object.DestroyImmediate(placeholder.gameObject);
            var preview = (GameObject)PrefabUtility.InstantiatePrefab(scenario.environment_prefab);
            preview.name = "Independent review placeholder (hidden automatically for production runtime)";
            preview.transform.localPosition = scenario.environment_translation;
            // Existing scene reference disables this serialized Terrain-preservation placeholder on Play.
            var reference = UnityEngine.Object.FindFirstObjectByType<ZombieGame.Tests.V010Defense.V010SceneReference>();
            if(reference)reference.reference = preview;
            var capture = game.gameObject.AddComponent<PresentationCapture>();
            capture.balance_sha256 = hash_file("balance/unit_balance.json");capture.grounding_version = source_record;
            capture.output_folder = EVIDENCE_ROOT;capture.capture_on_start = false;
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,SCENE_PATH);
            verify_frozen_hashes(integrity);AssetDatabase.Refresh();
            Debug.Log("[PresentationReview] Prepared "+SCENE_PATH+"; frozen scene/scenario/prefab, wall, farm and balance unchanged.");
        }

        [MenuItem("Zombie Game/V0.10/Presentation review/Capture five angles and grounding comparison")]
        public static void capture_review_play()
        {
            guard_editor();
            if(!File.Exists(SCENE_PATH))throw new FileNotFoundException("Prepare independent review first.",SCENE_PATH);
            EditorSceneManager.OpenScene(SCENE_PATH);
            var capture = UnityEngine.Object.FindFirstObjectByType<PresentationCapture>();
            if(!capture)throw new InvalidOperationException("Review capture fixture missing.");
            capture.capture_on_start = true;EditorApplication.EnterPlaymode();
        }

        [MenuItem("Zombie Game/V0.10/Presentation review/Open independent review")]
        public static void open_review()
        {guard_editor();EditorSceneManager.OpenScene(SCENE_PATH);EditorApplication.ExecuteMenuItem("Window/General/Game");}

        public static GameObject clone_frozen_environment()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(V010DefenseTools.PREFAB_PATH);
            if(!source)throw new FileNotFoundException("Frozen environment source unavailable.",V010DefenseTools.PREFAB_PATH);
            return PrefabUtility.LoadPrefabContents(V010DefenseTools.PREFAB_PATH);
        }

        private static IntegrityReport capture_frozen_hashes()
        {
            var result = new IntegrityReport {source = "v0.10.0-preview.1 reference-only frozen assets"};
            foreach(string path in FROZEN_PATHS)result.files.Add(new IntegrityRecord{path=path,sha256_before=hash_file(path)});
            return result;
        }

        private static void verify_frozen_hashes(IntegrityReport report)
        {
            foreach(var file in report.files)
            {
                file.sha256_after = hash_file(file.path);
                if(file.sha256_before != file.sha256_after)throw new InvalidOperationException("Frozen asset changed during presentation preparation: "+file.path);
            }
            report.status = "PASS";File.WriteAllText(EVIDENCE_ROOT+"/baseline-integrity.json",JsonUtility.ToJson(report,true));
        }

        public static string hash_file(string path)
        {
            if(!File.Exists(path))throw new FileNotFoundException("Expected traceable source is absent.",path);
            using(var stream=File.OpenRead(path))using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }

        private static void guard_editor()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop the owned review before preparing/switching scenes.");
            for(int index=0;index<UnityEngine.SceneManagement.SceneManager.sceneCount;index++)
                if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(index).isDirty)throw new InvalidOperationException("Unsaved scene detected; preserve user edits before switching.");
        }
    }
}
