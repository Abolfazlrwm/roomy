using Roomy.Core;

namespace Roomy.Tests;

public sealed class SpaceReportTests
{
    const long GB = 1L << 30;

    [Fact]
    public void ListsBigFoldersAsAnIndentedTreeAndTheBiggestFiles()
    {
        var root = new SpaceNode { Name = "C:", FullPath = @"C:\", Size = 100 * GB };
        var users = new SpaceNode { Name = "Users", FullPath = @"C:\Users", Size = 60 * GB, Parent = root };
        var you = new SpaceNode { Name = "you", FullPath = @"C:\Users\you", Size = 55 * GB, Parent = users };
        var tiny = new SpaceNode { Name = "tiny", FullPath = @"C:\tiny", Size = 10L << 20, Parent = root };
        var win = new SpaceNode { Name = "Windows", FullPath = @"C:\Windows", Size = 30 * GB, Parent = root };
        root.Children = [tiny, users, win];
        users.Children = [you];

        var text = SpaceReport.Build(root, @"C:\", [new BigFile(@"C:\Users\you\big.iso", 4 * GB), new BigFile(@"C:\a.zip", GB)],
            100 * GB, 20 * GB, new DateTime(2026, 11, 24, 14, 0, 0), "Roomy space report", "Largest folders", "Largest files");

        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        Assert.StartsWith(@"Roomy space report · C:\ · 2026-11-24 14:00", lines[0]);
        int users_ = lines.FindIndex(l => l.EndsWith(@"  C:\Users"));
        int you_ = lines.FindIndex(l => l.EndsWith(@"    C:\Users\you"));
        int win_ = lines.FindIndex(l => l.EndsWith(@"  C:\Windows"));
        Assert.True(users_ > 0 && you_ == users_ + 1 && win_ > you_, "bigger folders first, children indented under their parent");
        Assert.DoesNotContain(lines, l => l.EndsWith(@"C:\tiny"));
        int iso = lines.FindIndex(l => l.EndsWith(@"C:\Users\you\big.iso"));
        int zip = lines.FindIndex(l => l.EndsWith(@"C:\a.zip"));
        Assert.True(iso > win_ && zip > iso, "files listed after folders, biggest first");
    }
}
