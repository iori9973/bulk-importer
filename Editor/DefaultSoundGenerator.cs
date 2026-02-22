using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace BulkImporter
{
    /// <summary>
    /// デフォルト通知音（7音メロディ）を PCM WAV として生成・キャッシュする。
    /// ファイルは Unity の temporaryCachePath に保存され、外部ファイル依存がない。
    /// </summary>
    internal static class DefaultSoundGenerator
    {
        private static readonly string CachePath = Path.Combine(
            Application.temporaryCachePath,
            "BulkImporter",
            "complete_v1.wav");

        /// <summary>
        /// デフォルト音の絶対パスを返す。未生成なら先に生成する。
        /// </summary>
        public static string EnsureDefaultSound()
        {
            if (!File.Exists(CachePath))
                GenerateChime(CachePath);

            return CachePath;
        }

        private static void GenerateChime(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            const int sampleRate    = 44100;
            const int channels      = 1;
            const int bitsPerSample = 16;

            int gapSamples = (int)(sampleRate * 0.025);

            float[] n1 = GenerateBellTone(523.25, 0.10, sampleRate, decayRate: 12.0); // ド
            float[] n2 = GenerateBellTone(523.25, 0.10, sampleRate, decayRate: 12.0); // ド
            float[] n3 = GenerateBellTone(523.25, 0.10, sampleRate, decayRate: 12.0); // ド
            float[] n4 = GenerateBellTone(523.25, 0.10, sampleRate, decayRate: 12.0); // ド
            float[] n5 = GenerateBellTone(466.16, 0.10, sampleRate, decayRate: 12.0); // シ♭
            float[] n6 = GenerateBellTone(587.33, 0.10, sampleRate, decayRate: 12.0); // レ
            float[] n7 = GenerateBellTone(523.25, 0.40, sampleRate, decayRate:  3.5); // ドー

            int totalSamples = n1.Length + gapSamples
                             + n2.Length + gapSamples
                             + n3.Length + gapSamples
                             + n4.Length + gapSamples
                             + n5.Length + gapSamples
                             + n6.Length + gapSamples
                             + n7.Length;
            float[] samples = new float[totalSamples];
            int offset = 0;

            n1.CopyTo(samples, offset); offset += n1.Length + gapSamples;
            n2.CopyTo(samples, offset); offset += n2.Length + gapSamples;
            n3.CopyTo(samples, offset); offset += n3.Length + gapSamples;
            n4.CopyTo(samples, offset); offset += n4.Length + gapSamples;
            n5.CopyTo(samples, offset); offset += n5.Length + gapSamples;
            n6.CopyTo(samples, offset); offset += n6.Length + gapSamples;
            n7.CopyTo(samples, offset);

            int dataSize = samples.Length * (bitsPerSample / 8);

            using var stream = new FileStream(path, FileMode.Create);
            using var writer = new BinaryWriter(stream);

            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));

            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * (bitsPerSample / 8));
            writer.Write((short)(channels * (bitsPerSample / 8)));
            writer.Write((short)bitsPerSample);

            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);

            foreach (float s in samples)
            {
                float clamped = Math.Max(-1f, Math.Min(1f, s));
                writer.Write((short)(clamped * short.MaxValue));
            }
        }

        private static float[] GenerateBellTone(double frequency, double durationSec, int sampleRate,
                                                double decayRate = 5.0)
        {
            int total         = (int)(sampleRate * durationSec);
            int attackSamples = (int)(sampleRate * 0.004);
            float[] samples   = new float[total];

            for (int i = 0; i < total; i++)
            {
                double t = (double)i / sampleRate;

                double attack = i < attackSamples
                    ? (double)i / attackSamples
                    : 1.0;

                double decay    = Math.Exp(-decayRate * t);
                double envelope = attack * decay;

                double wave = Math.Sin(2 * Math.PI * frequency       * t) * 1.00
                            + Math.Sin(2 * Math.PI * frequency * 2.0 * t) * 0.35
                            + Math.Sin(2 * Math.PI * frequency * 3.0 * t) * 0.12;

                samples[i] = (float)(wave / 1.47 * envelope * 0.75);
            }

            return samples;
        }
    }
}
