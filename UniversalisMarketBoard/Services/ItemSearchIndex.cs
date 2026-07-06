using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
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
    private List<ItemSearchEntry> items = [];
    private Dictionary<string, ItemSearchEntry> itemsByExactName = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, ItemSearchEntry> itemsByNormalizedName = new(StringComparer.OrdinalIgnoreCase);
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
        var snapshot = items;

        return snapshot
            .Where(item => item.SearchKey.Contains(normalized))
            .OrderBy(item => item.SearchKey.StartsWith(normalized) ? 0 : 1)
            .ThenBy(item => item.Name)
            .Take(limit)
            .ToList();
    }

    public bool TryGetItem(uint itemId, out ItemSearchEntry? itemSearchEntry)
    {
        itemSearchEntry = items.FirstOrDefault(item => item.ItemId == itemId);
        return itemSearchEntry != null;
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
            items = loadedItems;
            itemsByExactName = loadedItems
                .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            itemsByNormalizedName = loadedItems
                .Select(item => new { Item = item, NormalizedName = NormalizeLookupName(item.Name) })
                .Where(entry => !string.IsNullOrEmpty(entry.NormalizedName))
                .GroupBy(entry => entry.NormalizedName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Item, StringComparer.OrdinalIgnoreCase);
        }, cancellationToken);
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
