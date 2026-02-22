using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BulkImporter
{
    /// <summary>
    /// OS に応じた方法で音声ファイルを再生する。
    /// Windows: WAV → winmm.dll PlaySound / その他 → PowerShell + WPF MediaPlayer
    /// macOS  : afplay コマンド（WAV・MP3・AAC・FLAC・AIFF 等対応）
    /// Linux  : aplay コマンド（WAV のみ）
    /// </summary>
    internal static class NotificationAudioPlayer
    {
        public static void Play()
        {
            if (!BulkImporterSettings.SoundEnabled)
                return;

            string custom = BulkImporterSettings.AudioFilePath;
            string path = !string.IsNullOrEmpty(custom) && File.Exists(custom)
                ? custom
                : DefaultSoundGenerator.EnsureDefaultSound();

            if (string.IsNullOrEmpty(path))
            {
                UnityEngine.Debug.LogWarning("[BulkImporter] 再生できる音声ファイルが見つかりませんでした。");
                return;
            }
            Play(path);
        }

        public static void Play(string absolutePath)
        {
            try
            {
                PlayPlatformSpecific(absolutePath);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[BulkImporter] 音声の再生に失敗しました: {e.Message}");
            }
        }

        private static void PlayPlatformSpecific(string absolutePath)
        {
#if UNITY_EDITOR_WIN
            bool isWav = string.Equals(Path.GetExtension(absolutePath), ".wav",
                                       StringComparison.OrdinalIgnoreCase);
            if (isWav)
                PlayWindows(absolutePath);
            else
                PlayWindowsNonWav(absolutePath);
#elif UNITY_EDITOR_OSX
            PlayMac(absolutePath);
#else
            PlayLinux(absolutePath);
#endif
        }

#if UNITY_EDITOR_WIN
        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        private static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);

        private const uint SndFilename  = 0x00020000;
        private const uint SndAsync     = 0x00000001;
        private const uint SndNoDefault = 0x00000002;

        private static void PlayWindows(string path)
        {
            PlaySound(path, IntPtr.Zero, SndFilename | SndAsync | SndNoDefault);
        }

        private static void PlayWindowsNonWav(string path)
        {
            string uri = new Uri(path).AbsoluteUri.Replace("'", "''");

            string script =
                "Add-Type -AssemblyName presentationCore\n" +
                $"$p = New-Object System.Windows.Media.MediaPlayer\n" +
                $"$p.Open([uri]'{uri}')\n" +
                "$p.Play()\n" +
                "Start-Sleep -Milliseconds 500\n" +
                "$nd = $p.NaturalDuration\n" +
                "if ($nd.HasTimeSpan) { $d = [int]$nd.TimeSpan.TotalSeconds + 1 } else { $d = 10 }\n" +
                "Start-Sleep -Seconds $d\n" +
                "$p.Stop()";

            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName               = "powershell.exe",
                Arguments              = $"-NonInteractive -WindowStyle Hidden -EncodedCommand {encoded}",
                UseShellExecute        = false,
                CreateNoWindow         = true
            };
            Task.Run(() => System.Diagnostics.Process.Start(psi));
        }

#elif UNITY_EDITOR_OSX
        private static void PlayMac(string path)
        {
            System.Diagnostics.Process.Start("afplay", $"\"{path}\"");
        }

#else
        private static void PlayLinux(string path)
        {
            System.Diagnostics.Process.Start("aplay", $"\"{path}\"");
        }
#endif
    }
}
