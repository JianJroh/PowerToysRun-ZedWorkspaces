using System;
using System.Collections.Generic;
using ManagedCommon;
using Wox.Plugin;

namespace ZedRecentProjects;

/// <summary>
/// Minimal host stub so the plugin can be exercised outside PowerToys.
/// </summary>
public sealed class StubApi : IPublicAPI
{
    public void ChangeQuery(string query, bool requery) { }
    public void RemoveUserSelectedItem(Result result) { }
    public Theme GetCurrentTheme() => Theme.Dark;
    public event Common.UI.ThemeChangedHandler ThemeChanged;
    public void SaveAppAllSettings() { }
    public void ReloadAllPluginData() { }
    public void CheckForNewUpdate() { }
    public void ShowMsg(string title, string subTitle, string iconPath, bool useMainWindowAsOwner)
        => Console.WriteLine($"  [host msg] {title}: {subTitle}");
    public List<PluginPair> GetAllPlugins() => new();
    public void ShowNotification(string title, string subTitle) { }
}