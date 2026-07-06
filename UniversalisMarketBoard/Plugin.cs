using System;
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
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IContextMenu ContextMenu { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;

    private const string CommandName = "/umb";

    public Configuration Configuration { get; }
    public WindowSystem WindowSystem { get; } = new("UniversalisMarketBoard");

    private ItemSearchIndex ItemSearchIndex { get; }
    private LifestreamTravelService LifestreamTravelService { get; }
    private MarketBoardWindow MainWindow { get; }
    private AppearanceWindow AppearanceWindow { get; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (Configuration.EnsureDefaults())
        {
            Configuration.Save();
        }

        var universalisClient = new UniversalisClient();
        ItemSearchIndex = new ItemSearchIndex(DataManager);
        LifestreamTravelService = new LifestreamTravelService(PluginInterface);
        MainWindow = new MarketBoardWindow(this, universalisClient, ItemSearchIndex, LifestreamTravelService);
        AppearanceWindow = new AppearanceWindow(this);

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(AppearanceWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Universal Market Board browser."
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

        CommandManager.RemoveHandler(CommandName);
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
            args.Target is MenuTargetDefault defaultTarget &&
            ItemSearchIndex.TryGetItem(defaultTarget.TargetName, out var itemEntry) &&
            itemEntry != null)
        {
            itemId = itemEntry.ItemId;
            return true;
        }

        itemId = 0;
        return false;
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

    private void OpenItem(uint itemId)
    {
        MainWindow.OpenItem(itemId);
    }

    public void ToggleMainUi() => MainWindow.Toggle();
    public void ToggleAppearanceUi() => AppearanceWindow.Toggle();
}
