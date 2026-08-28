using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ZedWorkspaces;

/// <summary>
/// Launches projects in the Zed editor via its CLI.
/// </summary>
public static class ZedLauncher
{
    /// <summary>
    /// Opens <paramref name="folderPath"/> in Zed. Prefers the <c>zed</c> CLI on PATH and
    /// falls back to the standard Windows install locations.
    /// </summary>
    public static bool Launch(string folderPath, bool newWindow)
    {
        var args = (newWindow ? "-n " : string.Empty) + "\"" + folderPath + "\"";

        foreach (var candidate in GetCandidates())
        {
            if (candidate == "zed")
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = "zed", Arguments = args, UseShellExecute = false });
                    return true;
                }
                catch (Exception)
                {
                    // "zed" is not on PATH; try the absolute candidates below.
                }
            }
            else if (File.Exists(candidate))
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = candidate, Arguments = args, UseShellExecute = false });
                    return true;
                }
                catch (Exception)
                {
                    // Try the next candidate.
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Selects <paramref name="path"/> in File Explorer.
    /// </summary>
    public static void RevealInExplorer(string path)
    {
        try
        {
            Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
        catch (Exception)
        {
            // Ignore: explorer failure is non-fatal.
        }
    }

    private static IEnumerable<string> GetCandidates()
    {
        yield return "zed";

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // User-level installation layout used by the Zed Windows installer.
        yield return Path.Combine(local, "Programs", "Zed", "zed.exe");
        yield return Path.Combine(local, "Programs", "Zed", "Zed.exe");

        // Alternative layouts seen in the wild.
        yield return Path.Combine(local, "Zed", "zed.exe");
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        yield return Path.Combine(appData, "Zed", "zed.exe");
    }
}