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
                dataCenter.WorldIds
                    .Where(worldLookup.ContainsKey)
                    .Select(worldId => new WorldOption(worldId, worldLookup[worldId].Name))
                    .OrderBy(world => world.Name)
                    .ToList()))
            .Where(dataCenter => dataCenter.Worlds.Count > 0)
            .OrderBy(dataCenter => dataCenter.Region)
            .ThenBy(dataCenter => dataCenter.Name)
            .ToList();

        return new MarketScopeCatalog
        {
            DataCenters = dataCenters,
        };
    }

    public bool TryGetCachedMarketData(string scopeSelector, string scopeLabel, uint itemId, bool? highQualityOnly, out UniversalisMarketData? marketData)
    {
        var cacheKey = BuildMarketDataCacheKey(scopeSelector, itemId, highQualityOnly);
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

    public async Task<UniversalisMarketData> GetMarketDataAsync(string scopeSelector, string scopeLabel, uint itemId, bool? highQualityOnly, CancellationToken cancellationToken)
    {
        if (TryGetCachedMarketData(scopeSelector, scopeLabel, itemId, highQualityOnly, out var cachedMarketData) &&
            cachedMarketData != null)
        {
            return cachedMarketData;
        }

        var selector = Uri.EscapeDataString(scopeSelector);
        var query = $"entries=1{BuildHighQualityQuery(highQualityOnly)}";
        var response = await GetOptionalAsync<UniversalisMarketResponse>($"{selector}/{itemId}?{query}", cancellationToken).ConfigureAwait(false);

        var marketData = new UniversalisMarketData
        {
            ScopeLabel = scopeLabel,
            Listings = response?.Listings ?? [],
            RecentHistory = response?.RecentHistory ?? [],
            ApproxDailySales = response?.ApproxDailySales ?? 0,
        };

        var cacheKey = BuildMarketDataCacheKey(scopeSelector, itemId, highQualityOnly);
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

    private static string BuildHighQualityQuery(bool? highQualityOnly)
    {
        if (highQualityOnly == null)
        {
            return string.Empty;
        }

        return $"&hq={(highQualityOnly.Value ? "true" : "false")}";
    }

    private static string BuildMarketDataCacheKey(string scopeSelector, uint itemId, bool? highQualityOnly)
    {
        return $"{scopeSelector}|{itemId}|{(highQualityOnly.HasValue ? (highQualityOnly.Value ? "hq" : "nq") : "all")}";
    }

    private sealed record CachedMarketData(DateTime StoredAtUtc, UniversalisMarketData Data);
}
