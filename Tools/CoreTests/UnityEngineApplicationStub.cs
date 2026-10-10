namespace UnityEngine
{
    /// <summary>Test-only stand-in for the Unity persistent data directory.</summary>
    public static class Application
    {
        public static string persistentDataPath { get; set; }
    }
}
