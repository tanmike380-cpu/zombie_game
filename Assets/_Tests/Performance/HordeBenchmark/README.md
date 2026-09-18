# Horde Render Benchmark

This folder contains the first performance test for the project. It is isolated
from production game code by its own directory and `UNITY_EDITOR` compilation guard.

Because the benchmark component is compiled only in the Unity Editor:

- benchmark scripts are available in Editor Play Mode;
- generated benchmark scenes remain under this folder;
- benchmark code is excluded from Player builds;
- production code under `Assets/_Game/` does not depend on this test.

The folder includes its own unlit shader with GPU instancing enabled. It does
not rely on a template shader that may lack an instancing variant.

Create the scene from:

`Tools > Zombie Game > Create 10K Horde Benchmark Scene`

The generated scene is saved to:

`Assets/_Tests/Performance/HordeBenchmark/Scenes/Tech_HordeBenchmark.unity`

## Fixed map scale

The benchmark uses a fixed `256 x 256` tile map as a practical reference for a
They Are Billions-style survival map. One tile equals one Unity world unit, so
the test area is `256 x 256` world units.

The benchmark camera shows the entire map. This deliberately creates a
worst-case render test in which all 10,000 placeholders are potentially visible
at the same time.

## Automated measurement

The automated run measures 1,000, 5,000, and 10,000 instances. Each stage uses
a three-second warmup followed by a ten-second sample. Results are written as
CSV to the path in `ZOMBIE_BENCHMARK_RESULT_PATH`.
