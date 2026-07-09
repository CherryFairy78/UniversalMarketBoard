using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using UniversalisMarketBoard.Models;
using UniversalisMarketBoard.Services;

namespace UniversalisMarketBoard.Windows;

public sealed class MarketBoardWindow : Window, IDisposable
{
    private const int MaxSearchResults = 50;
    private const uint AllWorldsId = 0;
    private const int ThemeStyleColorCount = 14;
    private const int WindowStyleVarCount = 1;
    private const int ContentStyleVarCount = 6;
    private const float SectionHeaderHeight = 40f;
    private const float ListingCellHeight = 32f;
    private const float ListingCellSpacing = 7f;
    private const float SummaryCardMinWidth = 165f;
    private const float SearchResultsHeight = 144f;

    private readonly Plugin plugin;
    private readonly UniversalisClient universalisClient;
    private readonly ItemSearchIndex itemSearchIndex;
    private readonly LifestreamTravelService lifestreamTravelService;

    private CancellationTokenSource? bootstrapCts;
    private CancellationTokenSource? itemSearchCts;
    private CancellationTokenSource? listingsCts;

    private string itemSearchText = string.Empty;
    private List<ItemSearchEntry> itemSearchResults = [];
    private ItemSearchEntry? selectedItem;
    private MarketScopeCatalog? marketScopeCatalog;
    private UniversalisMarketData? marketData;
    private string? bootstrapError;
    private string? listingsError;
    private string? travelStatus;
    private bool travelStatusIsError;
    private bool isBootstrapping = true;
    private bool isSearchingItems;
    private bool isLoadingListings;
    private bool resetCollapsedConditionNextFrame;
    private int itemSearchRequestVersion;
    private int listingsRequestVersion;
    public MarketBoardWindow(
        Plugin plugin,
        UniversalisClient universalisClient,
        ItemSearchIndex itemSearchIndex,
        LifestreamTravelService lifestreamTravelService)
        : base("Universal Market Board###UniversalisMarketBoard")
    {
        this.plugin = plugin;
        this.universalisClient = universalisClient;
        this.itemSearchIndex = itemSearchIndex;
        this.lifestreamTravelService = lifestreamTravelService;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(820, 560),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        AllowPinning = false;
        AllowClickthrough = false;

        StartBootstrap();
    }

    public void Dispose()
    {
        bootstrapCts?.Cancel();
        bootstrapCts?.Dispose();
        itemSearchCts?.Cancel();
        itemSearchCts?.Dispose();
        listingsCts?.Cancel();
        listingsCts?.Dispose();
    }

    public override void Draw()
    {
        WindowName = $"{plugin.Configuration.WindowHeaderText} {plugin.VersionLabel}###UniversalisMarketBoard";
        ImGui.PushStyleColor(ImGuiCol.Text, plugin.Configuration.TextColor.ToVector4());
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 18f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 14f);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, 12f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(12f, 8f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(10f, 10f));
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(8f, 8f));
        DrawDashboardLayout();
        ImGui.PopStyleVar(ContentStyleVarCount);
        ImGui.PopStyleColor();
    }

    public override void PreDraw()
    {
        var backgroundColor = plugin.Configuration.BackgroundColor.ToVector4();
        var titleBarColor = plugin.Configuration.TitleBarColor.ToVector4();
        var headerTitleTextColor = plugin.Configuration.HeaderTitleTextColor.ToVector4();
        var cardColor = plugin.Configuration.CardBackgroundColor.ToVector4();
        var mutedTextColor = plugin.Configuration.MutedTextColor.ToVector4();
        var buttonColor = plugin.Configuration.ButtonColor.ToVector4();

        ImGui.PushStyleColor(ImGuiCol.Text, headerTitleTextColor);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, mutedTextColor);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, backgroundColor);
        ImGui.PushStyleColor(ImGuiCol.TitleBg, titleBarColor);
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, Tint(titleBarColor, 1.12f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, cardColor);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Tint(cardColor, 0.95f, 1f));
        ImGui.PushStyleColor(ImGuiCol.PopupBg, Tint(cardColor, 0.9f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Header, Tint(cardColor, 1.08f, 1f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Tint(cardColor, 1.18f, 1f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, Tint(cardColor, 1.28f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Button, buttonColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Tint(buttonColor, 1.14f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Tint(buttonColor, 0.88f, 1f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 20f);
    }

    public override void PostDraw()
    {
        if (resetCollapsedConditionNextFrame)
        {
            CollapsedCondition = ImGuiCond.None;
            resetCollapsedConditionNextFrame = false;
        }

        ImGui.PopStyleVar(WindowStyleVarCount);
        ImGui.PopStyleColor(ThemeStyleColorCount);
    }

    public void OpenItem(uint itemId)
    {
        var entry = ResolveItemEntry(itemId);
        if (entry == null)
        {
            return;
        }

        if (marketScopeCatalog == null && !isBootstrapping)
        {
            StartBootstrap();
        }

        selectedItem = entry;
        itemSearchText = entry.Name;
        itemSearchResults = [entry];
        RequestItemSearch();

        if (Collapsed == true)
        {
            IsOpen = false;
            _ = Plugin.Framework.RunOnTick(() =>
            {
                Collapsed = false;
                CollapsedCondition = ImGuiCond.Appearing;
                resetCollapsedConditionNextFrame = true;
                IsOpen = true;
                BringToFront();
                RequestListingsRefresh();
            }, TimeSpan.Zero, 1, CancellationToken.None);
            return;
        }

        IsOpen = true;
        BringToFront();
        RequestListingsRefresh();
    }

    private ItemSearchEntry? ResolveItemEntry(uint itemId)
    {
        foreach (var candidateId in GetCandidateItemIds(itemId))
        {
            if (itemSearchIndex.TryGetItem(candidateId, out var indexedEntry) && indexedEntry != null)
            {
                return indexedEntry;
            }

            if (!Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().TryGetRow(candidateId, out var sheetItem))
            {
                continue;
            }

            var name = sheetItem.Name.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                return new ItemSearchEntry(candidateId, name, name.ToUpperInvariant());
            }
        }

        return null;
    }

    private static IEnumerable<uint> GetCandidateItemIds(uint itemId)
    {
        if (itemId == 0)
        {
            yield break;
        }

        yield return itemId;

        if (itemId > 1_000_000)
        {
            yield return itemId - 1_000_000;
        }

        if (itemId > 500_000)
        {
            yield return itemId - 500_000;
        }
    }

    private void RequestItemSearch()
    {
        itemSearchCts?.Cancel();
        itemSearchCts?.Dispose();

        if (string.IsNullOrWhiteSpace(itemSearchText))
        {
            itemSearchResults = [];
            isSearchingItems = false;
            return;
        }

        itemSearchCts = new CancellationTokenSource();
        var cancellationToken = itemSearchCts.Token;
        var searchText = itemSearchText;
        var requestVersion = Interlocked.Increment(ref itemSearchRequestVersion);
        isSearchingItems = true;

        _ = Task.Run(() =>
        {
            try
            {
                var results = itemSearchIndex.Search(searchText, MaxSearchResults).ToList();
                if (cancellationToken.IsCancellationRequested || requestVersion != itemSearchRequestVersion)
                {
                    return;
                }

                itemSearchResults = results;
            }
            finally
            {
                if (requestVersion == itemSearchRequestVersion)
                {
                    isSearchingItems = false;
                }
            }
        }, cancellationToken);
    }

    private void DrawDashboardLayout()
    {
        if (!lifestreamTravelService.IsAvailable)
        {
            DrawInlineNotice(
                "Travel buttons require the Lifestream plugin to be loaded.",
                Tint(plugin.Configuration.ButtonColor.ToVector4(), 1f, 0.18f),
                plugin.Configuration.MutedTextColor.ToVector4());
            ImGui.Spacing();
        }

        if (ImGui.BeginTable("umb-top-layout", 2, ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Search", ImGuiTableColumnFlags.WidthStretch, 1.18f);
            ImGui.TableSetupColumn("Scope", ImGuiTableColumnFlags.WidthStretch, 1f);

            ImGui.TableNextColumn();
            DrawSectionCard("umb-search-card", "Search Item", GetHeaderPalette()[0], DrawSearchPanelContent, 320f);

            ImGui.TableNextColumn();
            DrawSectionCard("umb-scope-card", "Market Scope", GetHeaderPalette()[1], DrawScopePanelContent, 320f);

            ImGui.EndTable();
        }

        ImGui.Spacing();
        DrawSectionCard("umb-listings-card", "Listings", GetHeaderPalette()[2], DrawListingsPanelContent);
    }

    private void DrawSearchPanelContent()
    {
        ImGui.TextUnformatted("Item Name");
        if (DrawProminentInput("##item-search", "Type an item name", ref itemSearchText, 100))
        {
            RequestItemSearch();
        }

        if (itemSearchIndex.IsLoading)
        {
            ImGui.TextDisabled("Building item index...");
        }
        else if (isSearchingItems)
        {
            ImGui.TextDisabled("Searching items...");
        }

        if (selectedItem != null)
        {
            DrawInlineNotice(
                $"Selected item: {selectedItem.Name} ({selectedItem.ItemId})",
                Tint(plugin.Configuration.TableHeaderColor.ToVector4(), 1.02f, 0.32f),
                plugin.Configuration.TextColor.ToVector4());
        }

        if (itemSearchText.Length == 0)
        {
            ImGui.TextDisabled("Start typing to search the item sheet.");
            return;
        }

        if (itemSearchResults.Count == 0)
        {
            ImGui.TextDisabled("No matching items.");
            return;
        }

        using var resultsChild = ImRaii.Child("umb-search-results", new Vector2(0f, SearchResultsHeight), false);
        if (!resultsChild.Success)
        {
            return;
        }

        foreach (var entry in itemSearchResults)
        {
            var isSelected = selectedItem?.ItemId == entry.ItemId;
            if (ImGui.Selectable($"{entry.Name}##{entry.ItemId}", isSelected))
            {
                selectedItem = entry;
                RequestListingsRefresh();
            }
        }
    }

    private void DrawScopePanelContent()
    {
        if (isBootstrapping)
        {
            ImGui.TextDisabled("Loading worlds and data centres...");
            return;
        }

        if (!string.IsNullOrWhiteSpace(bootstrapError))
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), bootstrapError);
            if (DrawStyledButton("Retry"))
            {
                StartBootstrap();
            }

            return;
        }

        if (marketScopeCatalog == null)
        {
            StartBootstrap();
            ImGui.TextDisabled("Loading worlds and data centres...");
            return;
        }

        var selectedScope = plugin.Configuration.SelectedScopeKind;
        if (ImGui.RadioButton("Data Centre##scope", selectedScope == ScopeKind.DataCenter))
        {
            plugin.Configuration.SelectedScopeKind = ScopeKind.DataCenter;
            plugin.Configuration.SelectedWorldId = AllWorldsId;
            plugin.Configuration.Save();
            RequestListingsRefresh();
        }

        ImGui.SameLine();
        if (ImGui.RadioButton("World##scope", selectedScope == ScopeKind.World))
        {
            plugin.Configuration.SelectedScopeKind = ScopeKind.World;
            if (plugin.Configuration.SelectedWorldId == AllWorldsId)
            {
                plugin.Configuration.SelectedWorldId = GetPreferredWorldIdForSelectedDataCenter(marketScopeCatalog);
            }
            plugin.Configuration.Save();
            RequestListingsRefresh();
        }

        DrawDataCenterCombo(marketScopeCatalog);
        DrawWorldCombo(marketScopeCatalog);

        var showHq = plugin.Configuration.ShowHighQuality;
        if (ImGui.Checkbox("Show HQ", ref showHq))
        {
            plugin.Configuration.ShowHighQuality = showHq;
            plugin.Configuration.Save();
        }

        ImGui.SameLine();
        var showNq = plugin.Configuration.ShowNormalQuality;
        if (ImGui.Checkbox("Show NQ", ref showNq))
        {
            plugin.Configuration.ShowNormalQuality = showNq;
            plugin.Configuration.Save();
        }

        ImGui.SameLine();
        var lowestFirst = !plugin.Configuration.SortHighestToLowest;
        if (ImGui.Checkbox("Lowest to Highest", ref lowestFirst))
        {
            plugin.Configuration.SortHighestToLowest = !lowestFirst;
            plugin.Configuration.Save();
        }

        if (DrawStyledButton("Refresh Prices", new Vector2(-110f, 0f)))
        {
            RequestListingsRefresh();
        }

        ImGui.SameLine();
        if (DrawStyledButton("Appearance"))
        {
            plugin.ToggleAppearanceUi();
        }
    }

    private void DrawDataCenterCombo(MarketScopeCatalog catalog)
    {
        var selectedLabel = GetSelectedDataCenterOption(catalog)?.DisplayName
            ?? plugin.Configuration.SelectedDataCenter;

        ImGui.TextUnformatted("Data Centre");
        ImGui.SetNextItemWidth(-1f);
        if (!ImGui.BeginCombo("##data-centre", selectedLabel))
        {
            return;
        }

        foreach (var dataCenter in catalog.DataCenters)
        {
            var isSelected = dataCenter.Selector == plugin.Configuration.SelectedDataCenter;
            if (ImGui.Selectable(dataCenter.DisplayName, isSelected))
            {
                plugin.Configuration.SelectedDataCenter = dataCenter.Selector;
                if (plugin.Configuration.SelectedWorldId == 0 ||
                    dataCenter.Worlds.All(world => world.Id != plugin.Configuration.SelectedWorldId))
                {
                    plugin.Configuration.SelectedWorldId = AllWorldsId;
                }

                plugin.Configuration.Save();
                RequestListingsRefresh();
            }

            if (isSelected)
            {
                ImGui.SetItemDefaultFocus();
            }
        }

        ImGui.EndCombo();
    }

    private void DrawWorldCombo(MarketScopeCatalog catalog)
    {
        var selectedDataCenter = GetSelectedDataCenterOption(catalog);
        if (selectedDataCenter == null)
        {
            return;
        }

        var selectedWorld = selectedDataCenter.Worlds.FirstOrDefault(world => world.Id == plugin.Configuration.SelectedWorldId);
        var selectedWorldLabel = plugin.Configuration.SelectedWorldId == AllWorldsId
            ? selectedDataCenter.AllWorldsLabel
            : selectedWorld?.Name;
        if (selectedWorldLabel == null)
        {
            return;
        }

        if (plugin.Configuration.SelectedWorldId != AllWorldsId && selectedWorld == null)
        {
            selectedWorld = selectedDataCenter.Worlds.FirstOrDefault();
            if (selectedWorld == null)
            {
                return;
            }

            plugin.Configuration.SelectedWorldId = selectedWorld.Id;
            plugin.Configuration.Save();
            selectedWorldLabel = selectedWorld.Name;
        }

        ImGui.TextUnformatted("World");
        ImGui.SetNextItemWidth(-1f);
        if (!ImGui.BeginCombo("##world", selectedWorldLabel))
        {
            return;
        }

        var allWorldsSelected = plugin.Configuration.SelectedWorldId == AllWorldsId;
        if (ImGui.Selectable(selectedDataCenter.AllWorldsLabel, allWorldsSelected))
        {
            plugin.Configuration.SelectedScopeKind = ScopeKind.DataCenter;
            plugin.Configuration.SelectedWorldId = AllWorldsId;
            plugin.Configuration.Save();
            RequestListingsRefresh();
        }

        if (allWorldsSelected)
        {
            ImGui.SetItemDefaultFocus();
        }

        foreach (var world in selectedDataCenter.Worlds)
        {
            var isSelected = world.Id == plugin.Configuration.SelectedWorldId;
            if (ImGui.Selectable(world.Name, isSelected))
            {
                plugin.Configuration.SelectedScopeKind = ScopeKind.World;
                plugin.Configuration.SelectedWorldId = world.Id;
                plugin.Configuration.Save();
                RequestListingsRefresh();
            }

            if (isSelected)
            {
                ImGui.SetItemDefaultFocus();
            }
        }

        ImGui.EndCombo();
    }

    private void DrawListingsPanelContent()
    {
        if (selectedItem == null)
        {
            ImGui.TextDisabled("Choose an item to load prices.");
            return;
        }

        if (isLoadingListings)
        {
            ImGui.TextDisabled("Loading listings from Universalis...");
        }

        if (!string.IsNullOrWhiteSpace(listingsError))
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), listingsError);
        }

        if (!string.IsNullOrWhiteSpace(travelStatus))
        {
            var color = travelStatusIsError
                ? new Vector4(1f, 0.4f, 0.4f, 1f)
                : plugin.Configuration.MutedTextColor.ToVector4();
            ImGui.TextColored(color, travelStatus);
        }

        if (marketData == null)
        {
            ImGui.TextDisabled("No listings loaded yet.");
            return;
        }

        var filteredListings = FilterListings(marketData.Listings).ToList();
        var hqCount = marketData.Listings.Count(listing => listing.IsHighQuality);
        var nqCount = marketData.Listings.Count - hqCount;
        var lastSale = marketData.LastSale;

        DrawSummaryCards(marketData.ScopeLabel, marketData.Listings.Count, hqCount, nqCount, marketData.MinPrice, marketData.MaxPrice, marketData.ApproxDailySales);

        var saleMessage = lastSale != null
            ? $"Last sold: {lastSale.PricePerUnit:N0} gil x{lastSale.Quantity:N0} on {lastSale.WorldName} ({(lastSale.IsHighQuality ? "HQ" : "NQ")}) at {FormatTimestamp(lastSale.TimestampLocal)}"
            : "Last sold: no recent history";
        DrawInlineNotice(saleMessage, Tint(plugin.Configuration.CardBackgroundColor.ToVector4(), 0.98f, 0.82f), plugin.Configuration.TextColor.ToVector4());

        if (marketData.Listings.Count == 0 && marketData.RecentHistory.Count == 0)
        {
            ImGui.TextDisabled("No Universalis market data is available for this item in the selected scope.");
            return;
        }

        if (filteredListings.Count == 0)
        {
            ImGui.TextDisabled("No listings match the current HQ/NQ filters.");
            return;
        }

        DrawListingGrid(filteredListings);
    }

    private void DrawListingGrid(IReadOnlyList<MarketListing> filteredListings)
    {
        using var child = ImRaii.Child("market-listing-grid", new Vector2(0f, 0f), false, ImGuiWindowFlags.HorizontalScrollbar);
        if (!child.Success)
        {
            return;
        }

        var widths = BuildListingColumnWidths(ImGui.GetContentRegionAvail().X);
        DrawListingHeader(widths);

        for (var index = 0; index < filteredListings.Count; index++)
        {
            DrawListingRow(filteredListings[index], widths, index);
        }
    }

    private IEnumerable<MarketListing> FilterListings(IEnumerable<MarketListing> listings)
    {
        var filtered = listings.Where(listing =>
            (plugin.Configuration.ShowHighQuality || !listing.IsHighQuality) &&
            (plugin.Configuration.ShowNormalQuality || listing.IsHighQuality));

        return plugin.Configuration.SortHighestToLowest
            ? filtered.OrderByDescending(listing => listing.PricePerUnit).ThenByDescending(listing => listing.Total)
            : filtered.OrderBy(listing => listing.PricePerUnit).ThenBy(listing => listing.Total);
    }

    private void DrawSectionCard(string id, string title, Vector4 accentColor, Action drawContent, float height = 0f)
    {
        using var child = ImRaii.Child(id, new Vector2(0f, height), true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        if (!child.Success)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var cursor = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var titleColor = plugin.Configuration.TableHeaderTextColor.ToVector4();

        drawList.AddRectFilled(
            cursor,
            new Vector2(cursor.X + width, cursor.Y + SectionHeaderHeight),
            ImGui.GetColorU32(accentColor),
            18f,
            ImDrawFlags.RoundCornersTop);

        var titleSize = ImGui.CalcTextSize(title);
        drawList.AddText(
            new Vector2(cursor.X + 16f, cursor.Y + ((SectionHeaderHeight - titleSize.Y) / 2f)),
            ImGui.GetColorU32(titleColor),
            title);

        ImGui.Dummy(new Vector2(width, SectionHeaderHeight + 4f));
        drawContent();

        var borderColor = Tint(plugin.Configuration.TableHeaderColor.ToVector4(), 0.9f, 0.3f);
        drawList.AddRect(
            ImGui.GetWindowPos(),
            ImGui.GetWindowPos() + ImGui.GetWindowSize(),
            ImGui.GetColorU32(borderColor),
            18f);
    }

    private bool DrawStyledButton(string label, Vector2? size = null)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, plugin.Configuration.ButtonTextColor.ToVector4());
        var clicked = size.HasValue ? ImGui.Button(label, size.Value) : ImGui.Button(label);
        ImGui.PopStyleColor();
        return clicked;
    }

    private bool DrawProminentInput(string id, string hint, ref string value, int maxLength)
    {
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Tint(plugin.Configuration.TableHeaderColor.ToVector4(), 1.02f, 0.3f));
        ImGui.PushStyleColor(ImGuiCol.Border, Tint(plugin.Configuration.ButtonColor.ToVector4(), 1f, 0.9f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.SetNextItemWidth(-1f);
        var changed = ImGui.InputTextWithHint(id, hint, ref value, maxLength);
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(2);
        return changed;
    }

    private void DrawTravelButton(MarketListing listing, float width, string rowKey)
    {
        if (!lifestreamTravelService.IsAvailable)
        {
            DrawButtonCell($"travel-{rowKey}", "Travel", width, false);
            return;
        }

        if (!DrawButtonCell($"travel-{rowKey}", "Travel", width, true))
        {
            return;
        }

        travelStatusIsError = !lifestreamTravelService.TryTravelToWorld(listing.WorldName, out var message);
        travelStatus = message;
    }

    private void DrawSummaryCards(string scopeLabel, int totalListings, int hqCount, int nqCount, int minPrice, int maxPrice, float approxDailySales)
    {
        var palette = GetHeaderPalette();
        var cards = new (string Label, string Value, Vector4 Accent)[]
        {
            ("Scope", scopeLabel, palette[0]),
            ("Listings", $"{totalListings:N0}", palette[1]),
            ("HQ / NQ", $"{hqCount:N0} / {nqCount:N0}", palette[2]),
            ("Price Span", $"{minPrice:N0} - {maxPrice:N0}", palette[3]),
            ("Daily Sales", $"{approxDailySales:F1}/day", palette[4]),
        };

        var availableWidth = ImGui.GetContentRegionAvail().X;
        var columns = Math.Clamp(
            (int)Math.Floor((availableWidth + ListingCellSpacing) / (SummaryCardMinWidth + ListingCellSpacing)),
            1,
            cards.Length);
        var cardWidth = (availableWidth - (ListingCellSpacing * (columns - 1))) / columns;

        for (var index = 0; index < cards.Length; index++)
        {
            if (index > 0 && index % columns != 0)
            {
                ImGui.SameLine(0f, ListingCellSpacing);
            }

            DrawSummaryCard(cards[index].Label, cards[index].Value, cards[index].Accent, cardWidth);
        }
    }

    private void DrawSummaryCard(string label, string value, Vector4 accentColor, float width)
    {
        var size = new Vector2(width, 78f);
        var position = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var bodyColor = Tint(plugin.Configuration.CardBackgroundColor.ToVector4(), 1.03f, 0.92f);

        ImGui.InvisibleButton($"summary-{label}", size);
        drawList.AddRectFilled(position, position + size, ImGui.GetColorU32(bodyColor), 16f);
        drawList.AddRectFilled(
            position,
            new Vector2(position.X + size.X, position.Y + 26f),
            ImGui.GetColorU32(accentColor),
            16f,
            ImDrawFlags.RoundCornersTop);
        drawList.AddText(position + new Vector2(12f, 6f), ImGui.GetColorU32(plugin.Configuration.TableHeaderTextColor.ToVector4()), label);
        drawList.AddText(position + new Vector2(12f, 40f), ImGui.GetColorU32(plugin.Configuration.TextColor.ToVector4()), FitText(value, size.X - 24f));
    }

    private void DrawInlineNotice(string message, Vector4 backgroundColor, Vector4 textColor)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var height = 36f;
        var position = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();

        ImGui.InvisibleButton($"notice-{message.GetHashCode()}", new Vector2(width, height));
        drawList.AddRectFilled(position, position + new Vector2(width, height), ImGui.GetColorU32(backgroundColor), 14f);
        drawList.AddText(position + new Vector2(12f, 10f), ImGui.GetColorU32(textColor), FitText(message, width - 24f));
    }

    private float[] BuildListingColumnWidths(float availableWidth)
    {
        var weights = new[] { 1.28f, 0.72f, 0.88f, 0.68f, 0.88f, 1.02f, 1.06f, 0.74f };
        var spacing = ListingCellSpacing * (weights.Length - 1);
        var baseWidth = Math.Max(104f, (availableWidth - spacing) / weights.Sum());
        return weights.Select(weight => weight * baseWidth).ToArray();
    }

    private void DrawListingHeader(IReadOnlyList<float> widths)
    {
        var palette = GetHeaderPalette();
        var labels = new[] { "World", "Quality", "Price", "Qty", "Total", "Retainer", "Updated", "Teleport" };

        for (var index = 0; index < labels.Length; index++)
        {
            if (index > 0)
            {
                ImGui.SameLine(0f, ListingCellSpacing);
            }

            DrawTextCell($"header-{labels[index]}", labels[index], widths[index], ListingCellHeight, palette[Math.Min(index, palette.Length - 1)], plugin.Configuration.TableHeaderTextColor.ToVector4(), index == 0);
        }

        ImGui.Spacing();
    }

    private void DrawListingRow(MarketListing listing, IReadOnlyList<float> widths, int index)
    {
        var mutedColor = plugin.Configuration.MutedTextColor.ToVector4();
        var textColor = plugin.Configuration.TextColor.ToVector4();
        var worldColor = Tint(plugin.Configuration.TitleBarColor.ToVector4(), 0.92f, 0.72f);
        var bodyColor = Tint(plugin.Configuration.CardBackgroundColor.ToVector4(), 1.04f, 0.94f);
        var rowKey = $"{index}-{listing.WorldName}-{listing.LastReviewTime}-{listing.PricePerUnit}-{listing.Quantity}-{listing.Total}";

        DrawTextCell($"world-{rowKey}", listing.WorldName, widths[0], ListingCellHeight, worldColor, textColor, true);
        ImGui.SameLine(0f, ListingCellSpacing);
        DrawTextCell($"quality-{rowKey}", listing.IsHighQuality ? "HQ" : "NQ", widths[1], ListingCellHeight, bodyColor, textColor);
        ImGui.SameLine(0f, ListingCellSpacing);
        DrawTextCell($"price-{rowKey}", $"{listing.PricePerUnit:N0}", widths[2], ListingCellHeight, bodyColor, textColor);
        ImGui.SameLine(0f, ListingCellSpacing);
        DrawTextCell($"qty-{rowKey}", $"{listing.Quantity:N0}", widths[3], ListingCellHeight, bodyColor, textColor);
        ImGui.SameLine(0f, ListingCellSpacing);
        DrawTextCell(
            $"total-{rowKey}",
            $"{listing.TotalWithTax:N0}",
            widths[4],
            ListingCellHeight,
            bodyColor,
            textColor,
            hoverText: $"Base total: {listing.Total:N0} gil\nTax: {listing.Tax:N0} gil");
        ImGui.SameLine(0f, ListingCellSpacing);
        DrawTextCell($"retainer-{rowKey}", string.IsNullOrWhiteSpace(listing.RetainerName) ? "-" : listing.RetainerName, widths[5], ListingCellHeight, bodyColor, textColor, true);
        ImGui.SameLine(0f, ListingCellSpacing);
        DrawTextCell($"updated-{rowKey}", FormatTimestamp(listing.LastReviewTimeLocal), widths[6], ListingCellHeight, bodyColor, mutedColor);
        ImGui.SameLine(0f, ListingCellSpacing);
        DrawTravelButton(listing, widths[7], rowKey);
    }

    private string FormatTimestamp(DateTime timestamp)
    {
        return timestamp.ToString(GetTimestampFormat());
    }

    private string GetTimestampFormat()
    {
        var homeWorldId = Plugin.PlayerState.HomeWorld.RowId;
        var region = marketScopeCatalog?.FindDataCenterRegion(homeWorldId);
        if (IsNorthAmericaRegion(region))
        {
            return "hh:mm tt MM/dd/yyyy";
        }

        return "hh:mm tt dd/MM/yyyy";
    }

    private static bool IsNorthAmericaRegion(string? region)
    {
        if (string.IsNullOrWhiteSpace(region))
        {
            return false;
        }

        return region.Contains("north", StringComparison.OrdinalIgnoreCase) &&
               region.Contains("america", StringComparison.OrdinalIgnoreCase);
    }

    private void DrawTextCell(string id, string text, float width, float height, Vector4 backgroundColor, Vector4 foregroundColor, bool leftAlign = false, string? hoverText = null)
    {
        var displayText = FitText(text, width - 24f);
        var position = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();

        ImGui.InvisibleButton(id, new Vector2(width, height));
        var hovered = ImGui.IsItemHovered();
        var tint = hovered ? Tint(backgroundColor, 1.03f, 1f) : backgroundColor;
        drawList.AddRectFilled(position, position + new Vector2(width, height), ImGui.GetColorU32(tint), 15f);

        var textSize = ImGui.CalcTextSize(displayText);
        var textPosition = leftAlign
            ? new Vector2(position.X + 14f, position.Y + ((height - textSize.Y) / 2f))
            : new Vector2(position.X + Math.Max(14f, (width - textSize.X) / 2f), position.Y + ((height - textSize.Y) / 2f));
        drawList.AddText(textPosition, ImGui.GetColorU32(foregroundColor), displayText);

        if (hovered && !string.IsNullOrWhiteSpace(hoverText))
        {
            DrawListingHoverTooltip(hoverText);
            return;
        }

        if (hovered && displayText != text)
        {
            DrawListingHoverTooltip(text);
        }
    }

    private void DrawListingHoverTooltip(string text)
    {
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 32f);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    private bool DrawButtonCell(string id, string text, float width, bool enabled)
    {
        var position = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var background = plugin.Configuration.ButtonColor.ToVector4();
        var textColor = plugin.Configuration.ButtonTextColor.ToVector4();

        if (!enabled)
        {
            ImGui.BeginDisabled();
        }

        var clicked = ImGui.InvisibleButton(id, new Vector2(width, ListingCellHeight));
        var hovered = ImGui.IsItemHovered() && enabled;

        if (!enabled)
        {
            ImGui.EndDisabled();
        }

        var fill = hovered ? Tint(background, 1.08f, 1f) : background;
        if (!enabled)
        {
            fill = Tint(background, 0.92f, 0.45f);
            textColor = Tint(textColor, 0.92f, 0.65f);
        }

        drawList.AddRectFilled(position, position + new Vector2(width, ListingCellHeight), ImGui.GetColorU32(fill), 15f);
        var textSize = ImGui.CalcTextSize(text);
        drawList.AddText(
            new Vector2(position.X + ((width - textSize.X) / 2f), position.Y + ((ListingCellHeight - textSize.Y) / 2f)),
            ImGui.GetColorU32(textColor),
            text);
        return enabled && clicked;
    }

    private Vector4[] GetHeaderPalette()
    {
        var baseHeader = plugin.Configuration.TableHeaderColor.ToVector4();
        var baseButton = plugin.Configuration.ButtonColor.ToVector4();
        var title = plugin.Configuration.TitleBarColor.ToVector4();

        return
        [
            Blend(title, baseHeader, 0.38f),
            Blend(title, baseButton, 0.48f),
            baseHeader,
            Blend(baseHeader, baseButton, 0.42f),
            baseButton,
            Blend(Tint(baseHeader, 0.9f, 1f), Tint(baseButton, 1.08f, 1f), 0.5f),
            Blend(Tint(title, 0.92f, 1f), baseHeader, 0.62f),
            Blend(baseButton, title, 0.32f),
        ];
    }

    private static Vector4 Blend(Vector4 left, Vector4 right, float weight)
    {
        return new Vector4(
            (left.X * (1f - weight)) + (right.X * weight),
            (left.Y * (1f - weight)) + (right.Y * weight),
            (left.Z * (1f - weight)) + (right.Z * weight),
            (left.W * (1f - weight)) + (right.W * weight));
    }

    private static string FitText(string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text) || ImGui.CalcTextSize(text).X <= maxWidth)
        {
            return text;
        }

        const string ellipsis = "...";
        var candidate = text;
        while (candidate.Length > 0 && ImGui.CalcTextSize(candidate + ellipsis).X > maxWidth)
        {
            candidate = candidate[..^1];
        }

        return candidate.Length == 0 ? ellipsis : candidate + ellipsis;
    }

    private static Vector4 Tint(Vector4 color, float brightness, float alphaScale)
    {
        return new Vector4(
            Math.Clamp(color.X * brightness, 0f, 1f),
            Math.Clamp(color.Y * brightness, 0f, 1f),
            Math.Clamp(color.Z * brightness, 0f, 1f),
            Math.Clamp(color.W * alphaScale, 0f, 1f));
    }

    private void StartBootstrap()
    {
        bootstrapCts?.Cancel();
        bootstrapCts?.Dispose();
        bootstrapCts = new CancellationTokenSource();

        isBootstrapping = true;
        bootstrapError = null;

        _ = Task.Run(async () =>
        {
            try
            {
                await itemSearchIndex.EnsureLoadedAsync(bootstrapCts.Token).ConfigureAwait(false);
                marketScopeCatalog = await universalisClient.GetMarketScopeCatalogAsync(bootstrapCts.Token).ConfigureAwait(false);
                EnsureSelectedScopeIsValid();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                bootstrapError = $"Unable to load Universalis scope data: {ex.Message}";
                Plugin.Log.Error(ex, "Failed to bootstrap market scope data.");
            }
            finally
            {
                isBootstrapping = false;
            }
        }, bootstrapCts.Token);
    }

    private void EnsureSelectedScopeIsValid()
    {
        if (marketScopeCatalog == null || marketScopeCatalog.DataCenters.Count == 0)
        {
            return;
        }

        var selectedDataCenter = GetSelectedDataCenterOption(marketScopeCatalog);
        if (selectedDataCenter != null)
        {
            if (plugin.Configuration.SelectedWorldId != AllWorldsId &&
                selectedDataCenter.Worlds.All(world => world.Id != plugin.Configuration.SelectedWorldId))
            {
                plugin.Configuration.SelectedWorldId = AllWorldsId;
                plugin.Configuration.Save();
            }

            return;
        }

        var preferredWorldId = Plugin.PlayerState.CurrentWorld.RowId != 0
            ? Plugin.PlayerState.CurrentWorld.RowId
            : Plugin.PlayerState.HomeWorld.RowId;
        var preferredDataCenter = preferredWorldId == 0
            ? null
            : marketScopeCatalog.FindDataCenterName(preferredWorldId);
        if (!string.IsNullOrWhiteSpace(preferredDataCenter))
        {
            plugin.Configuration.SelectedDataCenter = preferredDataCenter;
            plugin.Configuration.SelectedWorldId = AllWorldsId;
            plugin.Configuration.Save();
            return;
        }

        var dataCenter = marketScopeCatalog.DataCenters[0];
        plugin.Configuration.SelectedDataCenter = dataCenter.Selector;
        plugin.Configuration.SelectedWorldId = AllWorldsId;
        plugin.Configuration.Save();
    }

    private void RequestListingsRefresh()
    {
        if (selectedItem == null || marketScopeCatalog == null)
        {
            return;
        }

        listingsCts?.Cancel();
        listingsCts?.Dispose();
        listingsCts = new CancellationTokenSource();

        isLoadingListings = true;
        listingsError = null;
        var requestVersion = Interlocked.Increment(ref listingsRequestVersion);
        var selectedDataCenter = GetSelectedDataCenterOption(marketScopeCatalog);

        var useDataCenterScope = plugin.Configuration.SelectedWorldId == AllWorldsId;
        var selector = useDataCenterScope
            ? selectedDataCenter?.Selector ?? plugin.Configuration.SelectedDataCenter
            : plugin.Configuration.SelectedWorldId.ToString();
        var selectedItemId = selectedItem.ItemId;
        var highQualityOnly = plugin.Configuration.ShowHighQuality == plugin.Configuration.ShowNormalQuality
            ? (bool?)null
            : plugin.Configuration.ShowHighQuality;

        var scopeLabel = useDataCenterScope
            ? plugin.Configuration.SelectedScopeKind == ScopeKind.World && plugin.Configuration.SelectedWorldId == AllWorldsId
                ? selectedDataCenter?.AllWorldsLabel ?? plugin.Configuration.SelectedDataCenter
                : selectedDataCenter?.ScopeLabel ?? plugin.Configuration.SelectedDataCenter
            : marketScopeCatalog.FindWorldName(plugin.Configuration.SelectedWorldId) ?? plugin.Configuration.SelectedWorldId.ToString();

        if (universalisClient.TryGetCachedMarketData(selector, scopeLabel, selectedItemId, highQualityOnly, out var cachedMarketData) &&
            cachedMarketData != null)
        {
            marketData = cachedMarketData;
        }
        else
        {
            marketData = null;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var loadedMarketData = await universalisClient.GetMarketDataAsync(selector, scopeLabel, selectedItemId, highQualityOnly, listingsCts.Token).ConfigureAwait(false);
                if (requestVersion == listingsRequestVersion)
                {
                    marketData = loadedMarketData;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (requestVersion == listingsRequestVersion)
                {
                    listingsError = ex is HttpRequestException { StatusCode: HttpStatusCode.GatewayTimeout }
                        ? "Universalis timed out while loading that scope. Please try Refresh Prices again."
                        : $"Unable to load listings: {ex.Message}";
                    marketData = null;
                }

                Plugin.Log.Error(ex, "Failed to load Universalis listings.");
            }
            finally
            {
                if (requestVersion == listingsRequestVersion)
                {
                    isLoadingListings = false;
                }
            }
        }, listingsCts.Token);
    }

    private uint GetPreferredWorldIdForSelectedDataCenter(MarketScopeCatalog catalog)
    {
        var selectedDataCenter = GetSelectedDataCenterOption(catalog);
        if (selectedDataCenter == null || selectedDataCenter.Worlds.Count == 0)
        {
            return AllWorldsId;
        }

        var currentWorldId = Plugin.PlayerState.CurrentWorld.RowId;
        if (selectedDataCenter.Worlds.Any(world => world.Id == currentWorldId))
        {
            return currentWorldId;
        }

        var homeWorldId = Plugin.PlayerState.HomeWorld.RowId;
        if (selectedDataCenter.Worlds.Any(world => world.Id == homeWorldId))
        {
            return homeWorldId;
        }

        return selectedDataCenter.Worlds[0].Id;
    }

    private DataCenterOption? GetSelectedDataCenterOption(MarketScopeCatalog catalog)
    {
        return catalog.FindScope(plugin.Configuration.SelectedDataCenter)
            ?? catalog.DataCenters.FirstOrDefault();
    }

    private sealed class StyleColorScope(int count) : IDisposable
    {
        public void Dispose()
        {
            ImGui.PopStyleColor(count);
        }
    }
}
