namespace ZedRecentProjects;

/// <summary>
/// A single recent project loaded from Zed's workspace database.
/// </summary>
public sealed record ZedProject(string[] Paths, System.DateTime? OpenedAtUtc)
{
    /// <summary>
    /// The first (primary) root path of the project.
    /// </summary>
    public string FirstPath => Paths[0];

    /// <summary>
    /// Display title derived from the last path segment, e.g. the folder name.
    /// </summary>
    public string DisplayName
    {
        get
        {
            var trimmed = FirstPath.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            var name = System.IO.Path.GetFileName(trimmed);
            return name.Length > 0 ? name : trimmed;
        }
    }

    /// <summary>
    /// Subtitle: absolute path, with a marker when the workspace has multiple roots.
    /// </summary>
    public string DisplayPath => Paths.Length > 1 ? FirstPath + $"  (+{Paths.Length - 1} roots)" : FirstPath;
}