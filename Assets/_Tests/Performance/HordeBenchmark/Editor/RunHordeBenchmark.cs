using UnityEditor;

namespace ZombieGame.EditorTools
{
    public static class RunHordeBenchmark
    {
        [MenuItem("Tools/Zombie Game/Run 1K 5K 10K Benchmark")]
        public static void RunAutomatedBenchmark()
        {
            System.Environment.SetEnvironmentVariable("ZOMBIE_BENCHMARK_AUTORUN", "1");
            CreateHordeBenchmarkScene.CreateScene();
            var game_view_type = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            var game_view = EditorWindow.GetWindow(game_view_type);
            game_view.Show();
            game_view.maximized = true;
            game_view.Focus();
            EditorApplication.isPlaying = true;
        }
    }
}
