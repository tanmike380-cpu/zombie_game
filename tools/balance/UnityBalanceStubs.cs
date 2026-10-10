using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// CPU-only test adapter. Production UnityBalance.cs is compiled unchanged;
// these types are never imported into Assets or used by the game.
namespace UnityEngine
{
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType load_type) { }
    }
    public static class Application { public static string dataPath; }
    public static class Debug { public static void Log(object value) => Console.WriteLine(value); }
    public static class Mathf { public static float Max(float left, float right) => MathF.Max(left, right); }
    public readonly struct Hash128
    {
        private readonly string hash_text;
        private Hash128(string value) { hash_text = value; }
        public static Hash128 Compute(string value) => new Hash128(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).Substring(0, 32));
        public override string ToString() => hash_text;
    }
    public static class JsonUtility
    {
        public static T FromJson<T>(string json) => JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { IncludeFields = true });
    }
}
