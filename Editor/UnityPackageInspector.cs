using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace BulkImporter
{
    internal class VpmInstallerConfig
    {
        // packageId → version requirement
        public Dictionary<string, string> VpmDependencies = new Dictionary<string, string>();
    }

    /// <summary>
    /// .unitypackage（gzip+tar）を検査して vpm-package-auto-installer の
    /// config.json を読み取るユーティリティ。
    /// </summary>
    internal static class UnityPackageInspector
    {
        private const int MaxAssetBufferBytes = 8 * 1024; // 8KB 以下のアセットのみバッファ

        /// <summary>
        /// .unitypackage が vpm-package-auto-installer を含む場合に config を返す。
        /// 含まない・解析失敗の場合は null。
        /// </summary>
        public static VpmInstallerConfig TryReadVpmConfig(string packagePath)
        {
            try
            {
                var json = ExtractConfigJson(packagePath);
                return json != null ? ParseVpmConfig(json) : null;
            }
            catch
            {
                return null;
            }
        }

        // ---------------------------------------------------------------

        private static string ExtractConfigJson(string packagePath)
        {
            using var fs = File.OpenRead(packagePath);
            using var gz = new GZipStream(fs, CompressionMode.Decompress);

            // guid → asset bytes（小さいファイルのみ保持）
            var assetData = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            // guid → pathname テキスト
            var pathnames = new Dictionary<string, string>(StringComparer.Ordinal);

            var headerBuf = new byte[512];
            while (ReadFully(gz, headerBuf, 512))
            {
                if (headerBuf[0] == 0) break; // end-of-archive

                // エントリ名（null 終端 ASCII、最大 100 バイト）
                var name = ReadNullTerminatedAscii(headerBuf, 0, 100);
                if (string.IsNullOrEmpty(name)) break;

                // タイプフラグ（byte 156）: '5' = ディレクトリ
                char typeFlag = (char)headerBuf[156];

                // サイズ（8進数文字列、bytes 124–135）
                var sizeStr = ReadNullTerminatedAscii(headerBuf, 124, 12).Trim();
                long size = sizeStr.Length > 0 ? Convert.ToInt64(sizeStr, 8) : 0;

                byte[] data = null;
                if (size > 0)
                {
                    if (typeFlag != '5' && size <= MaxAssetBufferBytes)
                    {
                        data = new byte[size];
                        if (!ReadFully(gz, data, (int)size)) break;
                    }
                    else
                    {
                        SkipBytes(gz, size);
                    }
                    long padding = (512 - size % 512) % 512;
                    if (padding > 0) SkipBytes(gz, padding);
                }

                // エントリ名を解析: ./[guid]/pathname など
                var normalized = name.Replace('\\', '/').TrimStart('.').TrimStart('/');
                var slash = normalized.IndexOf('/');
                if (slash < 0 || data == null) continue;

                var guid     = normalized.Substring(0, slash);
                var fileType = normalized.Substring(slash + 1);

                if (fileType == "pathname")
                    pathnames[guid] = Encoding.UTF8.GetString(data).Trim();
                else if (fileType == "asset")
                    assetData[guid] = data;
            }

            // config.json を探す
            foreach (var kv in pathnames)
            {
                if (!kv.Value.EndsWith("/config.json", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (assetData.TryGetValue(kv.Key, out var bytes))
                    return Encoding.UTF8.GetString(bytes);
            }

            return null;
        }

        private static VpmInstallerConfig ParseVpmConfig(string json)
        {
            var match = Regex.Match(
                json,
                @"""vpmDependencies""\s*:\s*\{([^}]*)\}",
                RegexOptions.Singleline);
            if (!match.Success) return null;

            var config = new VpmInstallerConfig();
            var pairs = Regex.Matches(
                match.Groups[1].Value,
                @"""([^""]+)""\s*:\s*""([^""]*)""");
            foreach (Match p in pairs)
                config.VpmDependencies[p.Groups[1].Value] = p.Groups[2].Value;

            return config.VpmDependencies.Count > 0 ? config : null;
        }

        // ---------------------------------------------------------------

        private static bool ReadFully(Stream stream, byte[] buf, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int read = stream.Read(buf, offset, count - offset);
                if (read == 0) return false;
                offset += read;
            }
            return true;
        }

        private static void SkipBytes(Stream stream, long count)
        {
            var buf = new byte[4096];
            long remaining = count;
            while (remaining > 0)
            {
                int toRead = (int)Math.Min(remaining, buf.Length);
                int read = stream.Read(buf, 0, toRead);
                if (read == 0) break;
                remaining -= read;
            }
        }

        private static string ReadNullTerminatedAscii(byte[] buf, int offset, int maxLen)
        {
            int end = offset;
            while (end < offset + maxLen && buf[end] != 0) end++;
            return Encoding.ASCII.GetString(buf, offset, end - offset);
        }
    }
}
