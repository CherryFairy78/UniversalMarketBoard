using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using UniversalisMarketBoard.Models;

namespace UniversalisMarketBoard.Services;

public sealed class ItemSearchIndex
{
    private readonly IDataManager dataManager;
    private readonly Lock syncRoot = new();
    private ItemSearchEntry[] items = [];
    private ItemSearchEntry[] itemsBySearchKey = [];
    private Dictionary<string, ItemSearchEntry> itemsByExactName = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<uint, ItemSearchEntry> itemsById = [];
    private Dictionary<string, ItemSearchEntry> itemsByNormalizedName = new(StringComparer.OrdinalIgnoreCase);
    private string lastSearchQuery = string.Empty;
    private ItemSearchEntry[] lastSearchResults = [];
    private Task? loadTask;

    public ItemSearchIndex(IDataManager dataManager)
    {
        this.dataManager = dataManager;
    }

    public bool IsLoading
    {
        get
        {
            lock (syncRoot)
            {
                return loadTask is { IsCompleted: false };
            }
        }
    }

    public Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        lock (syncRoot)
        {
            loadTask ??= LoadAsync(cancellationToken);
            return loadTask;
        }
    }

    public IReadOnlyList<ItemSearchEntry> Search(string searchText, int limit)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return [];
        }

        var normalized = searchText.Trim().ToUpperInvariant();
        if (normalized.Length == 0)
        {
            return [];
        }

        lock (syncRoot)
        {
            if (string.Equals(lastSearchQuery, normalized, StringComparison.Ordinal))
            {
                return lastSearchResults;
            }
        }

        var startsWithMatches = new List<ItemSearchEntry>(limit);
        var containsMatches = new List<ItemSearchEntry>(Math.Min(limit, 16));
        var seenItemIds = new HashSet<uint>();
        var prefixStartIndex = FindPrefixStartIndex(itemsBySearchKey, normalized);

        for (var index = prefixStartIndex; index < itemsBySearchKey.Length && startsWithMatches.Count < limit; index++)
        {
            var item = itemsBySearchKey[index];
            if (!item.SearchKey.StartsWith(normalized, StringComparison.Ordinal))
            {
                break;
            }

            startsWithMatches.Add(item);
            seenItemIds.Add(item.ItemId);
        }

        if (startsWithMatches.Count < limit)
        {
            foreach (var item in items)
            {
                if (startsWithMatches.Count + containsMatches.Count >= limit)
                {
                    break;
                }

                if (!seenItemIds.Add(item.ItemId) || !item.SearchKey.Contains(normalized, StringComparison.Ordinal))
                {
                    continue;
                }

                containsMatches.Add(item);
            }
        }

        if (startsWithMatches.Count == 0 && containsMatches.Count == 0)
        {
            lock (syncRoot)
            {
                lastSearchQuery = normalized;
                lastSearchResults = Array.Empty<ItemSearchEntry>();
            }

            return [];
        }

        var results = new List<ItemSearchEntry>(Math.Min(limit, startsWithMatches.Count + containsMatches.Count));
        results.AddRange(startsWithMatches);
        if (results.Count < limit)
        {
            results.AddRange(containsMatches);
        }

        var resultArray = results.ToArray();
        lock (syncRoot)
        {
            lastSearchQuery = normalized;
            lastSearchResults = resultArray;
        }

        return resultArray;
    }

    public bool TryGetItem(uint itemId, out ItemSearchEntry? itemSearchEntry)
    {
        return itemsById.TryGetValue(itemId, out itemSearchEntry);
    }

    public bool TryGetItem(string itemName, out ItemSearchEntry? itemSearchEntry)
    {
        if (string.IsNullOrWhiteSpace(itemName))
        {
            itemSearchEntry = null;
            return false;
        }

        var trimmedName = itemName.Trim();
        if (itemsByExactName.TryGetValue(trimmedName, out itemSearchEntry))
        {
            return true;
        }

        var normalizedName = NormalizeLookupName(trimmedName);
        if (string.IsNullOrEmpty(normalizedName))
        {
            itemSearchEntry = null;
            return false;
        }

        return itemsByNormalizedName.TryGetValue(normalizedName, out itemSearchEntry);
    }

    private Task LoadAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var itemSheet = dataManager.GetExcelSheet<Item>();
            var loadedItems = new List<ItemSearchEntry>();

            foreach (var item in itemSheet)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var name = item.Name.ToString().Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                loadedItems.Add(new ItemSearchEntry(item.RowId, name, name.ToUpperInvariant()));
            }

            loadedItems.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
            items = [.. loadedItems];
            itemsBySearchKey = loadedItems
                .OrderBy(item => item.SearchKey, StringComparer.Ordinal)
                .ThenBy(item => item.Name, StringComparer.Ordinal)
                .ToArray();
            itemsById = loadedItems.ToDictionary(item => item.ItemId);
            itemsByExactName = loadedItems
                .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            itemsByNormalizedName = loadedItems
                .Select(item => new { Item = item, NormalizedName = NormalizeLookupName(item.Name) })
                .Where(entry => !string.IsNullOrEmpty(entry.NormalizedName))
                .GroupBy(entry => entry.NormalizedName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Item, StringComparer.OrdinalIgnoreCase);
            lastSearchQuery = string.Empty;
            lastSearchResults = Array.Empty<ItemSearchEntry>();
        }, cancellationToken);
    }

    private static int FindPrefixStartIndex(IReadOnlyList<ItemSearchEntry> source, string normalized)
    {
        var low = 0;
        var high = source.Count;

        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            var comparison = string.CompareOrdinal(source[middle].SearchKey, normalized);
            if (comparison < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private static string NormalizeLookupName(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasSpace = false;

        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToUpperInvariant(character));
                previousWasSpace = false;
                continue;
            }

            if (!char.IsWhiteSpace(character) || previousWasSpace)
            {
                continue;
            }

            builder.Append(' ');
            previousWasSpace = true;
        }

        return builder.ToString().Trim();
    }
}
