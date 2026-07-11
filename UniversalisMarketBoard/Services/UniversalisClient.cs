using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UniversalisMarketBoard.Models;

namespace UniversalisMarketBoard.Services;

public sealed class UniversalisClient
{
    private static readonly TimeSpan MarketDataCacheLifetime = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(350),
        TimeSpan.FromMilliseconds(900),
    ];

    private static readonly HttpClient HttpClient = new()
    {
        BaseAddress = new Uri("https://universalis.app/api/v2/"),
        Timeout = TimeSpan.FromSeconds(20),
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
    private static readonly Lock CacheLock = new();
    private static readonly Dictionary<string, CachedMarketData> MarketDataCache = [];

    public async Task<MarketScopeCatalog> GetMarketScopeCatalogAsync(CancellationToken cancellationToken)
    {
        var worldsTask = GetAsync<List<UniversalisWorld>>("worlds", cancellationToken);
        var dataCentersTask = GetAsync<List<UniversalisDataCenter>>("data-centers", cancellationToken);

        await Task.WhenAll(worldsTask, dataCentersTask).ConfigureAwait(false);

        var worldLookup = worldsTask.Result.ToDictionary(world => world.Id);
        var dataCenters = dataCentersTask.Result
            .Where(dataCenter => dataCenter.WorldIds.Count > 0)
            .Select(dataCenter => new DataCenterOption(
                dataCenter.Name,
                dataCenter.Region,
                dataCenter.Name,
                false,
                dataCenter.WorldIds
                    .Where(worldLookup.ContainsKey)
                    .Select(worldId => new WorldOption(worldId, worldLookup[worldId].Name))
                    .OrderBy(world => world.Name)
                    .ToList()))
            .Where(dataCenter => dataCenter.Worlds.Count > 0)
            .ToList();

        var aggregateScopes = dataCenters
            .GroupBy(dataCenter => dataCenter.Region)
            .Where(group => ShouldAddAggregateScope(group.Key, group.Count()))
            .Select(group => new DataCenterOption(
                group.Key,
                group.Key,
                group.Key,
                true,
                group
                    .SelectMany(dataCenter => dataCenter.Worlds)
                    .GroupBy(world => world.Id)
                    .Select(groupedWorld => groupedWorld.First())
                    .OrderBy(world => world.Name)
                    .ToList()))
            .ToList();

        var availableScopes = aggregateScopes
            .Concat(dataCenters)
            .OrderBy(dataCenter => dataCenter.Region)
            .ThenBy(dataCenter => dataCenter.IsRegionAggregate ? 0 : 1)
            .ThenBy(dataCenter => dataCenter.Name)
            .ToList();

        return new MarketScopeCatalog
        {
            DataCenters = availableScopes,
        };
    }

    public bool TryGetCachedMarketData(string scopeSelector, string scopeLabel, uint itemId, bool? highQualityOnly, int? entriesLimit, out UniversalisMarketData? marketData)
    {
        var cacheKey = BuildMarketDataCacheKey(scopeSelector, itemId, highQualityOnly, entriesLimit);
        lock (CacheLock)
        {
            if (MarketDataCache.TryGetValue(cacheKey, out var cached) &&
                DateTime.UtcNow - cached.StoredAtUtc <= MarketDataCacheLifetime)
            {
                marketData = cached.Data;
                return true;
            }
        }

        marketData = null;
        return false;
    }

    public void InvalidateMarketData(string scopeSelector, uint itemId, bool? highQualityOnly, int? entriesLimit)
    {
        var cacheKey = BuildMarketDataCacheKey(scopeSelector, itemId, highQualityOnly, entriesLimit);
        lock (CacheLock)
        {
            MarketDataCache.Remove(cacheKey);
        }
    }

    public async Task<UniversalisMarketData> GetMarketDataAsync(string scopeSelector, string scopeLabel, uint itemId, bool? highQualityOnly, int? entriesLimit, CancellationToken cancellationToken)
    {
        if (TryGetCachedMarketData(scopeSelector, scopeLabel, itemId, highQualityOnly, entriesLimit, out var cachedMarketData) &&
            cachedMarketData != null)
        {
            return cachedMarketData;
        }

        var selector = Uri.EscapeDataString(scopeSelector);
        var query = BuildMarketDataQuery(highQualityOnly, entriesLimit);
        var response = await GetOptionalAsync<UniversalisMarketResponse>($"{selector}/{itemId}{query}", cancellationToken).ConfigureAwait(false);
        var isSingleWorldScope = uint.TryParse(scopeSelector, out _);
        var fallbackWorldName = isSingleWorldScope ? scopeLabel : string.Empty;

        var marketData = new UniversalisMarketData
        {
            ScopeLabel = scopeLabel,
            Listings = NormalizeListings(response?.Listings, fallbackWorldName),
            RecentHistory = NormalizeRecentHistory(response?.RecentHistory, fallbackWorldName),
            ApproxDailySales = response?.ApproxDailySales ?? 0,
            ApproxDailySalesNq = response?.ApproxDailySalesNq ?? 0,
            ApproxDailySalesHq = response?.ApproxDailySalesHq ?? 0,
        };

        var cacheKey = BuildMarketDataCacheKey(scopeSelector, itemId, highQualityOnly, entriesLimit);
        lock (CacheLock)
        {
            MarketDataCache[cacheKey] = new CachedMarketData(DateTime.UtcNow, marketData);
        }

        return marketData;
    }

    private static async Task<T> GetAsync<T>(string relativePath, CancellationToken cancellationToken)
    {
        using var response = await GetResponseWithRetryAsync(relativePath, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException($"Universalis returned an empty response for '{relativePath}'.");
    }

    private static async Task<T?> GetOptionalAsync<T>(string relativePath, CancellationToken cancellationToken)
        where T : class
    {
        using var response = await GetResponseWithRetryAsync(relativePath, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException($"Universalis returned an empty response for '{relativePath}'.");
    }

    private static async Task<HttpResponseMessage> GetResponseWithRetryAsync(string relativePath, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var response = await HttpClient.GetAsync(relativePath, cancellationToken).ConfigureAwait(false);
                if (!ShouldRetry(response.StatusCode) || attempt >= RetryDelays.Length)
                {
                    return response;
                }

                response.Dispose();
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < RetryDelays.Length)
            {
            }
            catch (HttpRequestException ex) when (ShouldRetry(ex.StatusCode) && attempt < RetryDelays.Length)
            {
            }

            await Task.Delay(RetryDelays[attempt], cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool ShouldRetry(HttpStatusCode? statusCode)
    {
        return statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;
    }

    private static string BuildMarketDataQuery(bool? highQualityOnly, int? entriesLimit)
    {
        var queryParts = new List<string>(2);
        if (entriesLimit.HasValue)
        {
            queryParts.Add($"entries={entriesLimit.Value}");
        }

        if (highQualityOnly.HasValue)
        {
            queryParts.Add($"hq={(highQualityOnly.Value ? "true" : "false")}");
        }

        return queryParts.Count == 0
            ? string.Empty
            : $"?{string.Join("&", queryParts)}";
    }

    private static string BuildMarketDataCacheKey(string scopeSelector, uint itemId, bool? highQualityOnly, int? entriesLimit)
    {
        var qualityKey = highQualityOnly.HasValue ? (highQualityOnly.Value ? "hq" : "nq") : "all";
        var entriesKey = entriesLimit.HasValue ? $"entries-{entriesLimit.Value}" : "entries-full";
        return $"{scopeSelector}|{itemId}|{qualityKey}|{entriesKey}";
    }

    private static UniversalisMarketData BuildMarketData(string scopeLabel, UniversalisMarketResponse? response, string fallbackWorldName)
    {
        return new UniversalisMarketData
        {
            ScopeLabel = scopeLabel,
            Listings = NormalizeListings(response?.Listings, fallbackWorldName),
            RecentHistory = NormalizeRecentHistory(response?.RecentHistory, fallbackWorldName),
            ApproxDailySales = response?.ApproxDailySales ?? 0,
            ApproxDailySalesNq = response?.ApproxDailySalesNq ?? 0,
            ApproxDailySalesHq = response?.ApproxDailySalesHq ?? 0,
        };
    }

    private static bool ShouldAddAggregateScope(string region, int dataCenterCount)
    {
        if (dataCenterCount < 2)
        {
            return false;
        }

        return region is "Japan" or "North-America" or "Europe";
    }

    private static List<MarketListing> NormalizeListings(List<MarketListing>? listings, string fallbackWorldName)
    {
        if (listings == null || listings.Count == 0)
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(fallbackWorldName))
        {
            return listings;
        }

        return listings
            .Select(listing => string.IsNullOrWhiteSpace(listing.WorldName)
                ? new MarketListing
                {
                    WorldName = fallbackWorldName,
                    IsHighQuality = listing.IsHighQuality,
                    PricePerUnit = listing.PricePerUnit,
                    Quantity = listing.Quantity,
                    Total = listing.Total,
                    Tax = listing.Tax,
                    RetainerName = listing.RetainerName,
                    LastReviewTime = listing.LastReviewTime,
                }
                : listing)
            .ToList();
    }

    private static List<MarketSaleHistoryEntry> NormalizeRecentHistory(List<MarketSaleHistoryEntry>? recentHistory, string fallbackWorldName)
    {
        if (recentHistory == null || recentHistory.Count == 0)
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(fallbackWorldName))
        {
            return recentHistory;
        }

        return recentHistory
            .Select(entry => string.IsNullOrWhiteSpace(entry.WorldName)
                ? new MarketSaleHistoryEntry
                {
                    WorldName = fallbackWorldName,
                    IsHighQuality = entry.IsHighQuality,
                    PricePerUnit = entry.PricePerUnit,
                    Quantity = entry.Quantity,
                    Total = entry.Total,
                    Timestamp = entry.Timestamp,
                }
                : entry)
            .ToList();
    }

    private sealed record CachedMarketData(DateTime StoredAtUtc, UniversalisMarketData Data);
}
