using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace UniversalisMarketBoard.Models;

public enum ScopeKind
{
    DataCenter,
    World,
}

public sealed record ItemSearchEntry(
    uint ItemId,
    string Name,
    string SearchKey);

public sealed record WorldOption(uint Id, string Name);

public sealed record DataCenterOption(string Name, string Region, string Selector, bool IsRegionAggregate, IReadOnlyList<WorldOption> Worlds)
{
    public string RegionDisplayName => Region.Replace("-", " ");
    public string DisplayName => IsRegionAggregate ? $"All {RegionDisplayName}" : $"{Name} ({RegionDisplayName})";
    public string ScopeLabel => IsRegionAggregate ? $"All {RegionDisplayName}" : Name;
    public string AllWorldsLabel => IsRegionAggregate ? $"All {RegionDisplayName}" : $"All ({Name})";
}

public sealed class MarketScopeCatalog
{
    public IReadOnlyList<DataCenterOption> DataCenters { get; init; } = [];

    public DataCenterOption? FindScope(string selector)
    {
        return DataCenters.FirstOrDefault(dc => dc.Selector == selector || dc.Name == selector);
    }

    public string? FindDataCenterName(uint worldId)
    {
        return DataCenters
            .Where(dc => !dc.IsRegionAggregate)
            .FirstOrDefault(dc => dc.Worlds.Any(world => world.Id == worldId))
            ?.Name;
    }

    public string? FindWorldName(uint worldId)
    {
        return DataCenters
            .Where(dc => !dc.IsRegionAggregate)
            .SelectMany(dc => dc.Worlds)
            .FirstOrDefault(world => world.Id == worldId)
            ?.Name;
    }

    public string? FindDataCenterRegion(uint worldId)
    {
        return DataCenters
            .Where(dc => !dc.IsRegionAggregate)
            .FirstOrDefault(dc => dc.Worlds.Any(world => world.Id == worldId))
            ?.Region;
    }
}

public sealed class UniversalisMarketData
{
    public required string ScopeLabel { get; init; }
    public required List<MarketListing> Listings { get; init; }
    public required List<MarketSaleHistoryEntry> RecentHistory { get; init; }
    public float ApproxDailySales { get; init; }
    public float ApproxDailySalesNq { get; init; }
    public float ApproxDailySalesHq { get; init; }
    public int MinPrice => Listings.Count == 0 ? 0 : Listings.Min(listing => listing.PricePerUnit);
    public int MaxPrice => Listings.Count == 0 ? 0 : Listings.Max(listing => listing.PricePerUnit);
    public MarketSaleHistoryEntry? LastSale => RecentHistory.OrderByDescending(entry => entry.Timestamp).FirstOrDefault();

    public float GetDailySales(bool highQuality)
    {
        var qualityVelocity = highQuality ? ApproxDailySalesHq : ApproxDailySalesNq;
        return qualityVelocity > 0 ? qualityVelocity : ApproxDailySales;
    }
}

public sealed class MarketListing
{
    [JsonPropertyName("worldName")]
    public string WorldName { get; init; } = string.Empty;

    [JsonPropertyName("hq")]
    public bool IsHighQuality { get; init; }

    [JsonPropertyName("pricePerUnit")]
    public int PricePerUnit { get; init; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; init; }

    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("tax")]
    public int Tax { get; init; }

    [JsonPropertyName("retainerName")]
    public string RetainerName { get; init; } = string.Empty;

    [JsonPropertyName("lastReviewTime")]
    public long LastReviewTime { get; init; }

    [JsonIgnore]
    public DateTime LastReviewTimeLocal => DateTimeOffset.FromUnixTimeSeconds(LastReviewTime).LocalDateTime;

    [JsonIgnore]
    public int TotalWithTax => Total + Tax;
}

public sealed class UniversalisMarketResponse
{
    [JsonPropertyName("listings")]
    public List<MarketListing> Listings { get; init; } = [];

    [JsonPropertyName("recentHistory")]
    public List<MarketSaleHistoryEntry> RecentHistory { get; init; } = [];

    [JsonPropertyName("regularSaleVelocity")]
    public float ApproxDailySales { get; init; }

    [JsonPropertyName("nqSaleVelocity")]
    public float ApproxDailySalesNq { get; init; }

    [JsonPropertyName("hqSaleVelocity")]
    public float ApproxDailySalesHq { get; init; }
}

public sealed class UniversalisBatchMarketResponse
{
    [JsonPropertyName("items")]
    public Dictionary<string, UniversalisMarketResponse> Items { get; init; } = [];
}

public sealed class MarketSaleHistoryEntry
{
    [JsonPropertyName("worldName")]
    public string WorldName { get; init; } = string.Empty;

    [JsonPropertyName("hq")]
    public bool IsHighQuality { get; init; }

    [JsonPropertyName("pricePerUnit")]
    public int PricePerUnit { get; init; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; init; }

    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; }

    [JsonIgnore]
    public DateTime TimestampLocal => DateTimeOffset.FromUnixTimeSeconds(Timestamp).LocalDateTime;
}

public sealed class UniversalisWorld
{
    [JsonPropertyName("id")]
    public uint Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

public sealed class UniversalisDataCenter
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("region")]
    public string Region { get; init; } = string.Empty;

    [JsonPropertyName("worlds")]
    public List<uint> WorldIds { get; init; } = [];
}
