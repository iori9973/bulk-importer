using UnityEditor;

namespace BulkImporter
{
    internal static class BulkImporterSettings
    {
        private const string KeySoundEnabled   = "BulkImporter.SoundEnabled";
        private const string KeyAudioFilePath  = "BulkImporter.AudioFilePath";
        private const string KeyVolume         = "BulkImporter.Volume";
        private const string KeyLastDirectory  = "BulkImporter.LastDirectory";

        public static bool SoundEnabled
        {
            get => EditorPrefs.GetBool(KeySoundEnabled, true);
            set => EditorPrefs.SetBool(KeySoundEnabled, value);
        }

        public static string AudioFilePath
        {
            get => EditorPrefs.GetString(KeyAudioFilePath, "");
            set => EditorPrefs.SetString(KeyAudioFilePath, value);
        }

        public static float Volume
        {
            get => EditorPrefs.GetFloat(KeyVolume, 0.5f);
            set => EditorPrefs.SetFloat(KeyVolume, UnityEngine.Mathf.Clamp01(value));
        }

        public static string LastDirectory
        {
            get => EditorPrefs.GetString(KeyLastDirectory, "");
            set => EditorPrefs.SetString(KeyLastDirectory, value);
        }
    }
}
