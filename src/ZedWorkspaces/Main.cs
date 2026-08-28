using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Plugin;

namespace ZedWorkspaces;

/// <summary>
/// Main class of the "Zed Workspaces" plugin.
/// </summary>
public class Main : IPlugin, IContextMenu, ISettingProvider, IDisposable
{
    /// <summary>
    /// Stable ID of the plugin.
    /// </summary>
    public static string PluginID => "B53E277C0942415A80F6361C20D7DE13";

    /// <summary>
    /// Name of the plugin.
    /// </summary>
    public string Name => "Zed Workspaces";

    /// <summary>
    /// Description of the plugin.
    /// </summary>
    public string Description => "Search recent projects from the Zed editor and open them in Zed.";

    private PluginInitContext Context { get; set; }

    private const string IconPath = "Images/icon.png";

    private bool Disposed { get; set; }

    private PowerLauncherPluginSettings RuntimeSettings { get; set; }

    private static readonly PluginAdditionalOption[] OptionDefinitions =
    [
        new()
        {
            Key = "max_results",
            DisplayLabel = "Maximum number of recent projects",
            DisplayDescription = "How many recent projects to list. Defaults to 20.",
            PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Numberbox,
            NumberValue = 20,
            NumberBoxMin = 1,
            NumberBoxMax = 100,
        },
        new()
        {
            Key = "open_new_window",
            DisplayLabel = "Always open in a new window",
            DisplayDescription = "Open with `zed --new <path>`. When disabled, follow Zed's default opening behavior.",
            PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
            Value = true,
        },
        new()
        {
            Key = "show_relative_time",
            DisplayLabel = "Show relative last-opened time",
            DisplayDescription = "Append e.g. \"3 hours ago\" to each result subtitle.",
            PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
            Value = true,
        },
    ];

    /// <inheritdoc/>
    public IEnumerable<PluginAdditionalOption> AdditionalOptions => OptionDefinitions;

    /// <inheritdoc/>
    public void UpdateSettings(PowerLauncherPluginSettings settings) => RuntimeSettings = settings;

    /// <inheritdoc/>
    public Control CreateSettingPanel() => null; // custom WPF panels are only available to built-in plugins

    /// <inheritdoc/>
    public List<Result> Query(Query query)
    {
        var maxResults = ReadMaxResults();
        var showRelativeTime = ReadOptionBool("show_relative_time", true);
        var openNewWindow = ReadOptionBool("open_new_window", true);
        var search = query.Search?.Trim() ?? string.Empty;

        var results = new List<Result>();
        var projects = ZedDatabase.LoadRecentProjects(maxResults * 4);

        foreach (var project in projects)
        {
            if (results.Count >= maxResults)
            {
                break;
            }

            var title = project.DisplayName;
            var subtitle = BuildSubtitle(project, showRelativeTime);

            int score;
            IList<int> titleHighlight = null;
            if (search.Length > 0)
            {
                var matchedName = TextMatcher.TryScore(search, title, out var nameScore, out var nameHighlight);
                var matchedPath = TextMatcher.TryScore(search, project.DisplayPath, out var pathScore, out _);
                if (!matchedName && !matchedPath)
                {
                    continue; // no fuzzy match
                }

                if (matchedName && (!matchedPath || nameScore >= pathScore))
                {
                    score = nameScore;
                    titleHighlight = nameHighlight;
                }
                else
                {
                    score = pathScore;
                }
            }
            else
            {
                score = 0;
            }

            results.Add(new Result
            {
                Title = title,
                SubTitle = subtitle,
                IcoPath = IconPath,
                Score = score,
                TitleHighlightData = titleHighlight,
                ContextData = project,
                ToolTipData = new ToolTipData(title, project.DisplayPath),
                Action = _ => OpenZed(project, openNewWindow),
            });
        }

        return results;
    }

    /// <inheritdoc/>
    public void Init(PluginInitContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
    {
        if (selectedResult.ContextData is not ZedProject project)
        {
            return [];
        }

        return
        [
            new ContextMenuResult
            {
                PluginName = Name,
                Title = "Open in Zed",
                Glyph = "\xE72A", // OpenFile
                FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                Action = _ => OpenZed(project, true),
            },
            new ContextMenuResult
            {
                PluginName = Name,
                Title = "Reveal in File Explorer",
                Glyph = "\xE8B7", // OpenFolderHorizontal
                FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                Action = _ =>
                {
                    ZedLauncher.RevealInExplorer(project.FirstPath);
                    return true;
                },
            },
            new ContextMenuResult
            {
                PluginName = Name,
                Title = "Copy path",
                Glyph = "\xE8C8", // Copy
                FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                AcceleratorKey = Key.C,
                AcceleratorModifiers = ModifierKeys.Control,
                Action = _ =>
                {
                    Clipboard.SetDataObject(project.FirstPath);
                    return true;
                },
            },
        ];
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the plugin.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (Disposed || !disposing)
        {
            return;
        }

        Disposed = true;
    }

    private bool OpenZed(ZedProject project, bool newWindow)
    {
        if (!ZedLauncher.Launch(project.FirstPath, newWindow))
        {
            Context?.API.ShowMsg(Name, "Could not launch Zed. Install Zed or make sure `zed` is on PATH.");
        }

        return true;
    }

    private string BuildSubtitle(ZedProject project, bool showRelativeTime)
    {
        var time = showRelativeTime && project.OpenedAtUtc is { } openedAt ? "  ·  " + RelativeTime.Format(openedAt) : string.Empty;
        return project.DisplayPath + time;
    }

    private int ReadMaxResults()
    {
        var value = (int)(ReadOption("max_results")?.NumberValue ?? 0);
        return value > 0 ? Math.Min(value, 100) : 20;
    }

    private bool ReadOptionBool(string key, bool fallback) => ReadOption(key)?.Value ?? fallback;

    private PluginAdditionalOption ReadOption(string key)
    {
        var option = RuntimeSettings?.AdditionalOptions?.FirstOrDefault(o => o.Key == key);
        return option ?? OptionDefinitions.FirstOrDefault(o => o.Key == key);
    }

}