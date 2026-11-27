using Roomy.Core;

namespace Roomy.Tests;

public sealed class CrashLogTests : IDisposable
{
    readonly string _folder = Path.Combine(Path.GetTempPath(), "RoomyTests", Guid.NewGuid().ToString("N"));
    readonly string _original = CrashLog.FilePath;

    public CrashLogTests() => CrashLog.FilePath = Path.Combine(_folder, "crash.log");

    [Fact]
    public void WritesTheErrorAndSystemDetails()
    {
        CrashLog.Write(new InvalidOperationException("boom"), "startup");
        var text = File.ReadAllText(CrashLog.FilePath);
        Assert.Contains("startup", text);
        Assert.Contains("InvalidOperationException: boom", text);
        Assert.Contains("Roomy ", text);
        Assert.Contains("language", text);
    }

    [Fact]
    public void DatesUseTheStandardCalendarOnAnyWindowsLanguage()
    {
        var old = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("th-TH"); // Buddhist calendar
            CrashLog.Write(new Exception("x"), "test");
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = old; }
        Assert.StartsWith($"===== {DateTime.Now.Year}-", File.ReadAllText(CrashLog.FilePath));
    }

    [Fact]
    public void StaysSmallWhenWrittenToALot()
    {
        var big = new string('x', 20_000);
        for (int i = 0; i < 40; i++) CrashLog.Write(new Exception(big), "loop");
        Assert.True(new FileInfo(CrashLog.FilePath).Length < 300 * 1024);
    }

    [Fact]
    public void TrimmingStartsAtACompleteEntry()
    {
        // One huge entry followed by small ones: the halfway cut lands well inside the big entry,
        // so a mid-line cut starts the log in the middle of its "xxxx..." message.
        CrashLog.Write(new Exception(new string('x', 260_000)), "huge");
        for (int i = 0; i < 20; i++) CrashLog.Write(new Exception("small " + i), "small");
        var text = File.ReadAllText(CrashLog.FilePath);
        Assert.True(new FileInfo(CrashLog.FilePath).Length < 300 * 1024);
        Assert.StartsWith("===== ", text);
        Assert.Contains("small 19", text);
    }

    [Fact]
    public void NeverThrowsWhenTheFolderCantBeWritten()
    {
        CrashLog.FilePath = Path.Combine(_folder, "nul", "<bad>", "crash.log");
        CrashLog.Write(new Exception("x"), "test");
    }

    [Fact]
    public void MessagePointsToTheLogAndShowsTheRealCause()
    {
        var inner = new FileNotFoundException("icu.dll missing");
        var text = CrashLog.Describe(new TypeInitializationException("Roomy.Localization.S", inner), "Roomy couldn't start.");
        Assert.Contains("FileNotFoundException: icu.dll missing", text);
        Assert.Contains(CrashLog.FilePath, text);
    }

    public void Dispose()
    {
        CrashLog.FilePath = _original;
        try { Directory.Delete(_folder, recursive: true); } catch { }
    }
}
