using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Roomy.Core;

namespace Roomy.Tests;

/// <summary>
/// Cleanup must never reach files through a junction or symlink, wherever it sits in the path
/// (github.com/krezio/roomy/issues/4).
/// </summary>
public sealed class JunctionTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "RoomyTests", Guid.NewGuid().ToString("N"));
    readonly string _target;
    readonly string _sentinel;

    public JunctionTests()
    {
        _target = Path.Combine(_root, "target");
        Directory.CreateDirectory(Path.Combine(_target, "cache"));
        _sentinel = Path.Combine(_target, "cache", "sentinel.txt");
        File.WriteAllText(_sentinel, "keep me");
    }

    static void Junction(string link, string target)
    {
        var psi = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
        {
            CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        Assert.True(Directory.Exists(link), "mklink /J failed");
    }

    [Fact]
    public void JunctionInAnAncestorIsNotFollowed()
    {
        var link = Path.Combine(_root, "junction");
        Junction(link, _target);

        var r = FileOps.Delete(Path.Combine(link, "cache"), null);

        Assert.Equal(0, r.Files);
        Assert.True(File.Exists(_sentinel));
    }

    [Fact]
    public void JunctionInAnAncestorIsNotMeasured()
    {
        var link = Path.Combine(_root, "junction");
        Junction(link, _target);
        Assert.Equal((0L, 0), FileOps.Measure(Path.Combine(link, "cache"), null));
    }

    [Fact]
    public void JunctionAsTheRootIsNotFollowed()
    {
        var link = Path.Combine(_root, "junction");
        Junction(link, Path.Combine(_target, "cache"));

        var r = FileOps.Delete(link, null);

        Assert.Equal(0, r.Files);
        Assert.True(File.Exists(_sentinel));
    }

    [Fact]
    public void JunctionInsideTheTreeIsNotFollowed()
    {
        var cache = Path.Combine(_root, "real-cache");
        Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(cache, "junk.tmp"), "x");
        Junction(Path.Combine(cache, "link"), _target);

        var r = FileOps.Delete(cache, null);

        Assert.Equal(1, r.Files);
        Assert.True(File.Exists(_sentinel));
    }

    [Fact]
    public void SingleFileThroughAJunctionIsNotDeletedOrRecycled()
    {
        var link = Path.Combine(_root, "junction");
        Junction(link, _target);
        var viaLink = Path.Combine(link, "cache", "sentinel.txt");

        Assert.Equal(0, FileOps.Delete(viaLink, null).Files);
        Assert.Equal(0, FileOps.Recycle(viaLink, null).Files);
        Assert.True(File.Exists(_sentinel));
    }

    [Fact]
    public void NormalFoldersStillGetCleaned()
    {
        var cache = Path.Combine(_root, "plain");
        Directory.CreateDirectory(Path.Combine(cache, "sub"));
        File.WriteAllText(Path.Combine(cache, "a.tmp"), "aa");
        var ro = Path.Combine(cache, "sub", "b.tmp");
        File.WriteAllText(ro, "bbb");
        File.SetAttributes(ro, FileAttributes.ReadOnly);

        var r = FileOps.Delete(cache, null);

        Assert.Equal(2, r.Files);
        Assert.Equal(5, r.Freed);
        Assert.Empty(Directory.GetFileSystemEntries(cache));
        Assert.True(Directory.Exists(cache));
    }

    [Fact]
    public void RuntimeUnpackFoldersAreNeverCleaned()
    {
        // Single-file .NET apps (Roomy included) load files from %TEMP%\.net while they run (issue #10).
        var temp = Path.Combine(_root, "fake-temp");
        var runtime = Path.Combine(temp, ".net", "Roomy", "abc123");
        Directory.CreateDirectory(runtime);
        File.WriteAllText(Path.Combine(runtime, "vcruntime140_cor3.dll"), "keep");
        File.WriteAllText(Path.Combine(temp, "junk.tmp"), "xx");

        Assert.Equal((2L, 1), FileOps.Measure(temp, null));
        var r = FileOps.Delete(temp, null);

        Assert.Equal(1, r.Files);
        Assert.True(File.Exists(Path.Combine(runtime, "vcruntime140_cor3.dll")));
        Assert.False(File.Exists(Path.Combine(temp, "junk.tmp")));
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern uint GetShortPathNameW(string longPath, StringBuilder shortPath, uint length);

    [Fact]
    public void ShortEightDotThreeNamesStillWork()
    {
        var cache = Path.Combine(_root, "a long folder name");
        Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(cache, "x.tmp"), "xyz");
        var sb = new StringBuilder(1024);
        GetShortPathNameW(cache, sb, 1024);
        var shortPath = sb.ToString();
        if (shortPath.Length == 0 || string.Equals(shortPath, cache, StringComparison.OrdinalIgnoreCase)) return; // 8.3 names off on this drive

        Assert.Equal((3L, 1), FileOps.Measure(shortPath, null));
        Assert.Equal(1, FileOps.Delete(shortPath, null).Files);
    }

    public void Dispose()
    {
        try
        {
            // Remove junctions first so nothing is deleted through them.
            foreach (var d in Directory.GetDirectories(_root, "*", SearchOption.AllDirectories)
                         .Where(d => new DirectoryInfo(d).LinkTarget != null).OrderByDescending(d => d.Length))
                Directory.Delete(d);
            foreach (var f in Directory.GetFiles(_root, "*", SearchOption.AllDirectories))
                File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(_root, recursive: true);
        }
        catch { }
    }
}
