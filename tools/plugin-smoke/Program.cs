using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Plugin;
using ZedWorkspaces;

// The real PowerToys host resolves plugins' helper assemblies (e.g. Testably.Abstractions)
// from its own install directory. Mirror that here so Query() can run outside the host.
var hostDirs = new List<string> { AppContext.BaseDirectory };
foreach (var pr in System.Diagnostics.Process.GetProcessesByName("PowerToys"))
{
    try
    {
        var hostDir = Path.GetDirectoryName(pr.MainModule?.FileName);
        if (!string.IsNullOrEmpty(hostDir) && File.Exists(Path.Combine(hostDir, "PowerToys.exe")))
        {
            hostDirs.Add(hostDir);
            break;
        }
    }
    catch { /* ignore */ }
}
hostDirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys"));
hostDirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "PowerToys"));

AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
{
    var name = new AssemblyName(args.Name).Name + ".dll";
    foreach (var dir in hostDirs)
    {
        var candidate = Path.Combine(dir, name);
        if (File.Exists(candidate))
        {
            return Assembly.LoadFrom(candidate);
        }
    }
    return null;
};

Console.OutputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine("== LoadRecentProjects(20) ==");
var projects = ZedDatabase.LoadRecentProjects(20);
foreach (var p in projects)
{
    var suffix = p.OpenedAtUtc is { } w
        ? " (" + w.ToString("yyyy-MM-dd HH:mm") + " UTC, " + RelativeTime.Format(w) + ")"
        : "";
    Console.WriteLine($"  {p.DisplayName}  ->  {p.DisplayPath}{suffix}");
}

Console.WriteLine("\n== TextMatcher checks ==");
Check("vibe", "vibe-quota");
Check("vibe", "D:\\Dev\\OSS\\My\\vibe-usage");
Check("zed", "ZedWorkspaces");
Check("resume", "D:\\Work\\Resume\\2026\\RESUME.md");
Check("nosuch", "vibe-quota");
Check("", "anything");

Console.WriteLine("\n== Full plugin Query via host stub ==");
using (var plugin = new Main())
{
    var context = new PluginInitContext { API = new StubApi() };
    plugin.Init(context);
    plugin.UpdateSettings(new PowerLauncherPluginSettings()); // empty settings -> fall back to defaults

    RunQuery(plugin, "vibe");
    RunQuery(plugin, "");
    RunQuery(plugin, "zed vo");

    var emptyResults = plugin.Query(new Query("", ""));
    if (emptyResults.Count > 0)
    {
        Console.WriteLine($"\n== Context menu on first empty-query result ==");
        foreach (var menu in plugin.LoadContextMenus(emptyResults[0]))
        {
            Console.WriteLine($"  {menu.Title}");
        }
    }
    plugin.Dispose();
}

static void RunQuery(Main plugin, string search)
{
    Console.WriteLine($"\n-- Query(\"{search}\") --");
    var q = new Query(search, search);
    q.ActionKeyword = "";
    string qSearch;
    try { qSearch = q.Search; }
    catch (Exception ex) { qSearch = "<error: " + ex.Message + ">"; }
    Console.WriteLine($"  Search='{qSearch}'");
    if (qSearch.StartsWith("<error"))
    {
        return;
    }
    var results = plugin.Query(q);
    Console.WriteLine($"  {results.Count} results");
    foreach (var r in results)
    {
        Console.WriteLine($"   [{r.Score,5}] {r.Title}  |  {r.SubTitle}");
    }
}

static void Check(string q, string s)
{
    var ok = TextMatcher.TryScore(q, s, out var score, out var hl);
    Console.WriteLine($"  TryScore({q,8}, {s,6})  -> ok={ok} score={score,5} hl=[{string.Join(",", hl)}]");
}