// Unity API のスタブ。
// テストプロジェクトは Unity なしで動作するため、最低限の型を定義する。
using System.IO;

namespace UnityEngine
{
    internal static class Application
    {
        internal static string temporaryCachePath { get; } =
            Path.Combine(Path.GetTempPath(), "BulkImporterTests");
    }

    internal static class Debug
    {
        internal static void LogWarning(object message) { }
        internal static void Log(object message) { }
    }
}
