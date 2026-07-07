using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Inventory;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using UniversalisMarketBoard.Services;
using UniversalisMarketBoard.Windows;

namespace UniversalisMarketBoard;

public sealed unsafe class Plugin : IDalamudPlugin
{
    private const int ItemSearchContextItemOffset = 6192;
    private const int LogRetentionDays = 30;
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IContextMenu ContextMenu { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;

    private const string MainCommandName = "/umb";
    private const string DevCommandName = "/umbdev";

    public Configuration Configuration { get; }
    public string VersionLabel { get; }
    public WindowSystem WindowSystem { get; } = new("UniversalisMarketBoard");

    private ItemSearchIndex ItemSearchIndex { get; }
    private LifestreamTravelService LifestreamTravelService { get; }
    private MarketBoardWindow MainWindow { get; }
    private AppearanceWindow AppearanceWindow { get; }
    private string ActiveCommandName { get; }

    public Plugin()
    {
        VersionLabel = FormatVersionLabel();
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (Configuration.EnsureDefaults())
        {
            Configuration.Save();
        }

        CleanupOldLogs();

        var universalisClient = new UniversalisClient();
        ItemSearchIndex = new ItemSearchIndex(DataManager);
        LifestreamTravelService = new LifestreamTravelService(PluginInterface);
        MainWindow = new MarketBoardWindow(this, universalisClient, ItemSearchIndex, LifestreamTravelService);
        AppearanceWindow = new AppearanceWindow(this);
        ActiveCommandName = PluginInterface.IsDev ? DevCommandName : MainCommandName;

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(AppearanceWindow);

        CommandManager.AddHandler(ActiveCommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = PluginInterface.IsDev
                ? "Open the Universal Market Board dev build."
                : "Open the Universal Market Board browser."
        });

        ContextMenu.OnMenuOpened += OnMenuOpened;
        PluginInterface.UiBuilder.Draw += DrawUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleAppearanceUi;

        Log.Information("Universal Market Board loaded.");
    }

    public void Dispose()
    {
        ContextMenu.OnMenuOpened -= OnMenuOpened;
        PluginInterface.UiBuilder.Draw -= DrawUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleAppearanceUi;

        WindowSystem.RemoveAllWindows();
        MainWindow.Dispose();
        AppearanceWindow.Dispose();

        CommandManager.RemoveHandler(ActiveCommandName);
    }

    private void OnCommand(string command, string args)
    {
        MainWindow.Toggle();
    }

    private void DrawUi()
    {
        WindowSystem.Draw();
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        if (!Configuration.ShowContextMenuOption)
        {
            return;
        }

        if (TryGetContextMenuItemId(args, out var itemId) && itemId != 0)
        {
            args.AddMenuItem(new MenuItem
            {
                Name = "Search in Market Board",
                PrefixChar = 'U',
                Priority = -1,
                OnClicked = _ => OpenItem(itemId),
            });
        }
    }

    private bool TryGetContextMenuItemId(IMenuOpenedArgs args, out uint itemId)
    {
        if (args.MenuType == ContextMenuType.Inventory && args.Target is MenuTargetInventory inventoryTarget)
        {
            itemId = inventoryTarget.TargetItem?.BaseItemId ?? inventoryTarget.TargetItem?.ItemId ?? 0;
            return itemId != 0;
        }

        if (args.MenuType == ContextMenuType.Default &&
            string.Equals(args.AddonName, "ItemSearch", StringComparison.OrdinalIgnoreCase))
        {
            itemId = GetAgentContextItemId(args.AgentPtr, ItemSearchContextItemOffset);
            if (itemId != 0)
            {
                return true;
            }
        }

        if (args.MenuType == ContextMenuType.Default &&
            !string.IsNullOrEmpty(args.AddonName) &&
            args.AddonName.Contains("ChatLog", StringComparison.OrdinalIgnoreCase))
        {
            itemId = GetChatLogContextItemId();
            if (itemId != 0)
            {
                return true;
            }
        }

        if (args.MenuType == ContextMenuType.Default &&
            IsVendorContextAddon(args.AddonName) &&
            TryGetHoveredItemId(out itemId))
        {
            return true;
        }

        if (args.MenuType == ContextMenuType.Default &&
            args.Target is MenuTargetDefault defaultTarget &&
            TryResolveDefaultTargetItemId(defaultTarget, out itemId))
        {
            return true;
        }

        itemId = 0;
        return false;
    }

    private static bool IsVendorContextAddon(string? addonName)
    {
        if (string.IsNullOrWhiteSpace(addonName))
        {
            return false;
        }

        return addonName.Contains("Shop", StringComparison.OrdinalIgnoreCase)
            || addonName.Contains("Exchange", StringComparison.OrdinalIgnoreCase)
            || addonName.Contains("Trade", StringComparison.OrdinalIgnoreCase)
            || addonName.Contains("Store", StringComparison.OrdinalIgnoreCase)
            || addonName.Contains("Collectables", StringComparison.OrdinalIgnoreCase)
            || addonName.Contains("Inclusion", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetHoveredItemId(out uint itemId)
    {
        itemId = NormalizeContextItemId((uint)GameGui.HoveredItem);
        return itemId != 0;
    }

    private bool TryResolveDefaultTargetItemId(MenuTargetDefault defaultTarget, out uint itemId)
    {
        foreach (var candidate in GetTargetNameCandidates(defaultTarget.TargetName))
        {
            if (ItemSearchIndex.TryGetItem(candidate, out var itemEntry) && itemEntry != null)
            {
                itemId = itemEntry.ItemId;
                return true;
            }
        }

        itemId = 0;
        return false;
    }

    private static IEnumerable<string> GetTargetNameCandidates(string? targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in BuildTargetNameCandidates(targetName))
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var trimmed = candidate.Trim();
            if (seen.Add(trimmed))
            {
                yield return trimmed;
            }
        }
    }

    private static IEnumerable<string> BuildTargetNameCandidates(string targetName)
    {
        yield return targetName;

        var flattened = targetName
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        yield return flattened;

        var firstLine = targetName
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(firstLine))
        {
            yield return firstLine;
        }

        if (!string.IsNullOrWhiteSpace(flattened))
        {
            yield return TrimDecorations(flattened);
        }

        if (!string.IsNullOrWhiteSpace(firstLine))
        {
            yield return TrimDecorations(firstLine);
        }
    }

    private static string TrimDecorations(string value)
    {
        return value
            .Trim()
            .TrimStart('\uE03E', '\uE03F', '\uE040', '\uE041', '\uE042', '\uE043')
            .Trim();
    }

    private static uint GetChatLogContextItemId()
    {
        var agentModule = AgentModule.Instance();
        if (agentModule == null)
        {
            return 0;
        }

        var chatLogAgent = (AgentChatLog*)agentModule->GetAgentByInternalId(AgentId.ChatLog);
        if (chatLogAgent == null)
        {
            return 0;
        }

        var baseItemId = chatLogAgent->LinkedItem.GetBaseItemId();
        if (baseItemId != 0)
        {
            return baseItemId;
        }

        return chatLogAgent->ContextItemId;
    }

    private static uint GetAgentContextItemId(IntPtr agentPtr, int offset)
    {
        if (agentPtr == IntPtr.Zero)
        {
            return 0;
        }

        var itemId = *(uint*)((byte*)agentPtr + offset);
        return NormalizeContextItemId(itemId);
    }

    private static uint NormalizeContextItemId(uint itemId)
    {
        if (itemId > 1_000_000)
        {
            itemId -= 1_000_000;
        }

        if (itemId > 500_000)
        {
            itemId -= 500_000;
        }

        return itemId;
    }

    private void CleanupOldLogs()
    {
        try
        {
            var configDirectory = PluginInterface.GetPluginConfigDirectory();
            if (string.IsNullOrWhiteSpace(configDirectory) || !Directory.Exists(configDirectory))
            {
                return;
            }

            var cutoffUtc = DateTime.UtcNow.AddDays(-LogRetentionDays);
            foreach (var logFile in Directory.EnumerateFiles(configDirectory, "*.log*", SearchOption.AllDirectories))
            {
                var lastWriteUtc = File.GetLastWriteTimeUtc(logFile);
                if (lastWriteUtc < cutoffUtc)
                {
                    File.Delete(logFile);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Unable to clean old plugin log files.");
        }
    }

    private static string FormatVersionLabel()
    {
        var version = typeof(Plugin).Assembly.GetName().Version;
        if (version == null)
        {
            return "v0.0.0";
        }

        var build = version.Build >= 0 ? version.Build : 0;
        return $"v{version.Major}.{version.Minor}.{build}";
    }

    private void OpenItem(uint itemId)
    {
        MainWindow.OpenItem(itemId);
    }

    public void ToggleMainUi() => MainWindow.Toggle();
    public void ToggleAppearanceUi() => AppearanceWindow.Toggle();
}
