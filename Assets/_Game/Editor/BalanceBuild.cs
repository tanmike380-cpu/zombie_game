using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using ZombieGame.Balance;

namespace ZombieGame.EditorTools
{
    public sealed class BalanceBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;
        public void OnPreprocessBuild(BuildReport report) { sync_snapshot(); CharacterBake.ensure_models(); }

        [MenuItem("Tools/Zombie Game/Balance/Validate and sync build snapshot")]
        public static void sync_snapshot()
        {
            string source = Path.Combine(Application.dataPath,"../balance/unit_balance.json");
            string json = File.ReadAllText(source);
            UnitBalance.validate(JsonUtility.FromJson<BalanceConfig>(json));
            UnitBalance.reset_cache(); var human = UnitBalance.human;
            const string target = "Assets/_Game/Resources/BalanceGenerated/unit_balance.json";
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            if (!File.Exists(target) || File.ReadAllText(target) != json) File.WriteAllText(target,json);
            AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[BalanceBuild] exact snapshot hash=" + UnitBalance.source_hash);
        }
    }
}
