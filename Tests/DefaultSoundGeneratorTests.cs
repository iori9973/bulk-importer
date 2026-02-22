using System.IO;
using BulkImporter;
using Xunit;

namespace BulkImporter.Tests;

public class DefaultSoundGeneratorTests : IDisposable
{
    // テストごとにキャッシュを削除して毎回生成を確認する
    private readonly string _cacheDir =
        Path.Combine(Path.GetTempPath(), "BulkImporterTests", "BulkImporter");

    public DefaultSoundGeneratorTests()
    {
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, recursive: true);
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, recursive: true);
    }

    [Fact]
    public void EnsureDefaultSound_ReturnsExistingFile()
    {
        string path = DefaultSoundGenerator.EnsureDefaultSound();

        Assert.True(File.Exists(path), $"WAV ファイルが存在しません: {path}");
    }

    [Fact]
    public void EnsureDefaultSound_CalledTwice_ReturnsSamePath()
    {
        string path1 = DefaultSoundGenerator.EnsureDefaultSound();
        string path2 = DefaultSoundGenerator.EnsureDefaultSound();

        Assert.Equal(path1, path2);
    }

    [Fact]
    public void EnsureDefaultSound_ProducesValidRiffHeader()
    {
        string path = DefaultSoundGenerator.EnsureDefaultSound();
        using var reader = new BinaryReader(File.OpenRead(path));

        // RIFF チャンク
        Assert.Equal("RIFF", ReadFourCC(reader));
        reader.ReadInt32(); // chunk size（ファイルサイズ依存のためスキップ）
        Assert.Equal("WAVE", ReadFourCC(reader));

        // fmt チャンク
        Assert.Equal("fmt ", ReadFourCC(reader));
        Assert.Equal(16,    reader.ReadInt32());  // fmt チャンクサイズ
        Assert.Equal(1,     reader.ReadInt16());  // PCM フォーマット
        Assert.Equal(1,     reader.ReadInt16());  // モノラル
        Assert.Equal(44100, reader.ReadInt32());  // サンプルレート
        reader.ReadInt32();                       // バイトレート（スキップ）
        reader.ReadInt16();                       // ブロックアライン（スキップ）
        Assert.Equal(16,    reader.ReadInt16());  // 16bit

        // data チャンク
        Assert.Equal("data", ReadFourCC(reader));
    }

    [Fact]
    public void EnsureDefaultSound_SamplesAreWithinValidRange()
    {
        string path = DefaultSoundGenerator.EnsureDefaultSound();
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        // WAV ヘッダー（44 バイト）をスキップ
        reader.ReadBytes(44);

        int totalSamples = 0;
        while (stream.Position < stream.Length)
        {
            short sample = reader.ReadInt16();
            Assert.InRange(sample, (short)-32767, (short)32767);
            totalSamples++;
        }

        Assert.True(totalSamples > 0, "サンプルが1つも生成されていません");
    }

    [Fact]
    public void EnsureDefaultSound_SampleCountMatchesExpected()
    {
        // 短音×6(各0.10s=4410) + 長音(0.40s=17640) + ギャップ×6(25ms=1102)
        // = 6×4410 + 17640 + 6×1102 = 26460 + 17640 + 6612 = 50712
        const int sampleRate = 44100;
        const int noteShort  = (int)(sampleRate * 0.10);  // 4410
        const int noteLong   = (int)(sampleRate * 0.40);  // 17640
        const int gap        = (int)(sampleRate * 0.025); // 1102
        const int expectedCount = noteShort * 6 + noteLong + gap * 6; // 50712

        string path = DefaultSoundGenerator.EnsureDefaultSound();
        long dataSize      = new FileInfo(path).Length - 44; // WAV ヘッダー除く
        int  actualSamples = (int)(dataSize / 2);            // 16bit = 2 バイト/サンプル

        Assert.Equal(expectedCount, actualSamples);
    }

    private static string ReadFourCC(BinaryReader reader) =>
        new string(reader.ReadChars(4));
}
