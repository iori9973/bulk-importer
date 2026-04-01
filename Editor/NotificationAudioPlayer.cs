using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace BulkImporter
{
    /// <summary>
    /// OS に応じた方法で音声ファイルを再生する。
    /// Windows: PowerShell + WPF MediaPlayer（音量制御対応）
    /// macOS  : afplay コマンド（WAV・MP3・AAC・FLAC・AIFF 等対応）
    /// Linux  : paplay コマンド（PulseAudio）
    /// </summary>
    internal static class NotificationAudioPlayer
    {
        private static Process _currentProcess;

        public static bool IsPlaying
        {
            get
            {
                if (_currentProcess == null) return false;
                if (_currentProcess.HasExited)
                {
                    _currentProcess = null;
                    return false;
                }
                return true;
            }
        }

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
            Stop();
            try
            {
                PlayPlatformSpecific(absolutePath);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[BulkImporter] 音声の再生に失敗しました: {e.Message}");
            }
        }

        public static void Stop()
        {
            if (_currentProcess == null) return;
            try
            {
                if (!_currentProcess.HasExited)
                    _currentProcess.Kill();
            }
            catch (Exception) { }
            _currentProcess = null;
        }

        private static void PlayPlatformSpecific(string absolutePath)
        {
            float volume = BulkImporterSettings.Volume;
            if (volume <= 0f) return;

#if UNITY_EDITOR_WIN
            PlayWindows(absolutePath, volume);
#elif UNITY_EDITOR_OSX
            PlayMac(absolutePath, volume);
#else
            PlayLinux(absolutePath, volume);
#endif
        }

#if UNITY_EDITOR_WIN
        private static void PlayWindows(string path, float volume)
        {
            string uri = new Uri(path).AbsoluteUri.Replace("'", "''");
            string vol = volume.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

            string script =
                "Add-Type -AssemblyName presentationCore\n" +
                $"$p = New-Object System.Windows.Media.MediaPlayer\n" +
                $"$p.Open([uri]'{uri}')\n" +
                $"$p.Volume = {vol}\n" +
                "$p.Play()\n" +
                "Start-Sleep -Milliseconds 500\n" +
                "$nd = $p.NaturalDuration\n" +
                "if ($nd.HasTimeSpan) { $d = [int]$nd.TimeSpan.TotalSeconds + 1 } else { $d = 10 }\n" +
                "Start-Sleep -Seconds $d\n" +
                "$p.Stop()";

            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            var psi = new ProcessStartInfo
            {
                FileName               = "powershell.exe",
                Arguments              = $"-NonInteractive -WindowStyle Hidden -EncodedCommand {encoded}",
                UseShellExecute        = false,
                CreateNoWindow         = true
            };
            _currentProcess = Process.Start(psi);
        }

#elif UNITY_EDITOR_OSX
        private static void PlayMac(string path, float volume)
        {
            string vol = volume.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            _currentProcess = Process.Start("afplay", $"-v {vol} \"{path}\"");
        }

#else
        private static void PlayLinux(string path, float volume)
        {
            // paplay (PulseAudio) は 0-65536 の範囲で音量を指定
            int pulseVol = (int)(volume * 65536);
            _currentProcess = Process.Start("paplay", $"--volume={pulseVol} \"{path}\"");
        }
#endif
    }
}
