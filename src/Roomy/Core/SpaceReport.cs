using System.Text;

namespace Roomy.Core;

/// <summary>
/// A plain-text report of what's taking up a drive: a folder tree down to a few levels and the biggest
/// files, with full paths. Easy to read, share, or paste into an assistant to ask what's safe to remove.
/// </summary>
public static class SpaceReport
{
    public const int MaxDepth = 5;
    public const long MinFolderSize = 500L * 1024 * 1024;

    public static string Build(SpaceNode root, string rootPath, IEnumerable<BigFile> bigFiles, long? driveTotal, long? driveFree,
        DateTime when, string title, string foldersHeading, string filesHeading)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{title} · {rootPath} · {when:yyyy-MM-dd HH:mm}");
        if (driveTotal is { } total && driveFree is { } free)
            sb.AppendLine($"{Format.Bytes(total - free)} used of {Format.Bytes(total)}, {Format.Bytes(free)} free");
        sb.AppendLine($"Folders shown down to {MaxDepth} levels and {Format.Bytes(MinFolderSize)} or more.");
        sb.AppendLine();

        sb.AppendLine(foldersHeading);
        sb.AppendLine(new string('-', foldersHeading.Length));
        void Walk(SpaceNode node, int depth)
        {
            foreach (var child in node.Children.Where(c => c.Size >= MinFolderSize).OrderByDescending(c => c.Size))
            {
                sb.Append(Format.Bytes(child.Size).PadLeft(10)).Append("  ").Append(new string(' ', depth * 2)).AppendLine(child.FullPath);
                if (depth + 1 < MaxDepth) Walk(child, depth + 1);
            }
        }
        Walk(root, 0);
        sb.AppendLine();

        sb.AppendLine(filesHeading);
        sb.AppendLine(new string('-', filesHeading.Length));
        foreach (var f in bigFiles.OrderByDescending(f => f.Size))
            sb.Append(Format.Bytes(f.Size).PadLeft(10)).Append("  ").AppendLine(f.FullPath);
        return sb.ToString();
    }
}
