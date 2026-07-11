using System;
using System.Numerics;
using Dalamud.Configuration;
using UniversalisMarketBoard.Models;

namespace UniversalisMarketBoard;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    private const string CurrentWindowTitle = "Universal Market Board";
    private const string LegacyWindowTitle = "Universalis Market Board";
    private static readonly ThemeColor DefaultTextColor = ThemeColor.From(0.93f, 0.95f, 0.98f, 1f);
    private static readonly ThemeColor DefaultMutedTextColor = ThemeColor.From(0.63f, 0.68f, 0.77f, 1f);
    private static readonly ThemeColor DefaultHeadingColor = ThemeColor.From(0.47f, 0.62f, 0.96f, 1f);
    private static readonly ThemeColor DefaultBackgroundColor = ThemeColor.From(0.11f, 0.13f, 0.17f, 0.98f);
    private static readonly ThemeColor DefaultTitleBarColor = ThemeColor.From(0.15f, 0.18f, 0.26f, 1f);
    private static readonly ThemeColor DefaultTableHeaderColor = ThemeColor.From(0.27f, 0.4f, 0.73f, 1f);
    private static readonly ThemeColor DefaultCardBackgroundColor = ThemeColor.From(0.16f, 0.19f, 0.25f, 0.95f);
    private static readonly ThemeColor DefaultButtonColor = ThemeColor.From(0.29f, 0.45f, 0.8f, 1f);
    private static readonly ThemeColor DefaultButtonTextColor = ThemeColor.From(0.98f, 0.99f, 1f, 1f);
    private static readonly ThemeColor DefaultHeaderTitleTextColor = ThemeColor.From(0.98f, 0.99f, 1f, 1f);
    private static readonly ThemeColor DefaultTableHeaderTextColor = ThemeColor.From(0.98f, 0.99f, 1f, 1f);

    public int Version { get; set; } = 22;

    public ScopeKind SelectedScopeKind { get; set; } = ScopeKind.DataCenter;
    public string SelectedDataCenter { get; set; } = "Aether";
    public uint SelectedWorldId { get; set; } = 73;
    public bool SortHighestToLowest { get; set; }
    public bool ShowHighQuality { get; set; } = true;
    public bool ShowNormalQuality { get; set; } = true;
    public bool ShowContextMenuOption { get; set; } = true;
    public string WindowHeaderText { get; set; } = CurrentWindowTitle;
    public ThemeColor TextColor { get; set; } = DefaultTextColor.Clone();
    public ThemeColor MutedTextColor { get; set; } = DefaultMutedTextColor.Clone();
    public ThemeColor HeadingColor { get; set; } = DefaultHeadingColor.Clone();
    public ThemeColor BackgroundColor { get; set; } = DefaultBackgroundColor.Clone();
    public ThemeColor TitleBarColor { get; set; } = DefaultTitleBarColor.Clone();
    public ThemeColor HeaderTitleTextColor { get; set; } = DefaultHeaderTitleTextColor.Clone();
    public ThemeColor TableHeaderColor { get; set; } = DefaultTableHeaderColor.Clone();
    public ThemeColor TableHeaderTextColor { get; set; } = DefaultTableHeaderTextColor.Clone();
    public ThemeColor CardBackgroundColor { get; set; } = DefaultCardBackgroundColor.Clone();
    public ThemeColor ButtonColor { get; set; } = DefaultButtonColor.Clone();
    public ThemeColor ButtonTextColor { get; set; } = DefaultButtonTextColor.Clone();

    public void ResetAppearance()
    {
        ApplyThemePreset("Blue");
    }

    public void ApplyThemePreset(string themeName)
    {
        WindowHeaderText = CurrentWindowTitle;

        switch (themeName.Trim().ToLowerInvariant())
        {
            case "red":
                ApplyAccentTheme(
                    ThemeColor.From(0.9f, 0.34f, 0.38f, 1f),
                    ThemeColor.From(0.78f, 0.24f, 0.31f, 1f));
                break;
            case "yellow":
                ApplyAccentTheme(
                    ThemeColor.From(0.88f, 0.72f, 0.24f, 1f),
                    ThemeColor.From(0.78f, 0.6f, 0.16f, 1f));
                break;
            case "pink":
                ApplyAccentTheme(
                    ThemeColor.From(0.91f, 0.47f, 0.77f, 1f),
                    ThemeColor.From(0.79f, 0.34f, 0.66f, 1f));
                break;
            case "green":
                ApplyAccentTheme(
                    ThemeColor.From(0.36f, 0.78f, 0.56f, 1f),
                    ThemeColor.From(0.26f, 0.63f, 0.44f, 1f));
                break;
            case "purple":
                ApplyAccentTheme(
                    ThemeColor.From(0.62f, 0.5f, 0.94f, 1f),
                    ThemeColor.From(0.48f, 0.37f, 0.82f, 1f));
                break;
            case "orange":
                ApplyAccentTheme(
                    ThemeColor.From(0.93f, 0.58f, 0.24f, 1f),
                    ThemeColor.From(0.82f, 0.45f, 0.14f, 1f));
                break;
            case "grey":
                ApplyAccentTheme(
                    ThemeColor.From(0.58f, 0.64f, 0.74f, 1f),
                    ThemeColor.From(0.43f, 0.49f, 0.58f, 1f));
                break;
            case "white":
                ApplyAccentTheme(
                    ThemeColor.From(0.9f, 0.93f, 0.98f, 1f),
                    ThemeColor.From(0.74f, 0.8f, 0.9f, 1f));
                break;
            case "black":
                ApplyAppearance(
                    ThemeColor.From(0.95f, 0.96f, 0.98f, 1f),
                    ThemeColor.From(0.68f, 0.72f, 0.79f, 1f),
                    ThemeColor.From(0.78f, 0.82f, 0.9f, 1f),
                    ThemeColor.From(0.05f, 0.06f, 0.08f, 0.98f),
                    ThemeColor.From(0.08f, 0.09f, 0.12f, 1f),
                    ThemeColor.From(0.98f, 0.99f, 1f, 1f),
                    ThemeColor.From(0.17f, 0.19f, 0.24f, 1f),
                    ThemeColor.From(0.98f, 0.99f, 1f, 1f),
                    ThemeColor.From(0.1f, 0.12f, 0.16f, 0.96f),
                    ThemeColor.From(0.24f, 0.28f, 0.36f, 1f),
                    ThemeColor.From(0.98f, 0.99f, 1f, 1f));
                break;
            case "blue":
            default:
                ApplyAccentTheme(
                    DefaultTableHeaderColor.Clone(),
                    DefaultButtonColor.Clone());
                break;
        }
    }

    private void ApplyAccentTheme(ThemeColor headerColor, ThemeColor buttonColor)
    {
        ApplyAppearance(
            DefaultTextColor.Clone(),
            DefaultMutedTextColor.Clone(),
            headerColor.Clone(),
            DefaultBackgroundColor.Clone(),
            DefaultTitleBarColor.Clone(),
            DefaultHeaderTitleTextColor.Clone(),
            headerColor.Clone(),
            DefaultTableHeaderTextColor.Clone(),
            DefaultCardBackgroundColor.Clone(),
            buttonColor.Clone(),
            DefaultButtonTextColor.Clone());
    }

    private void ApplyAppearance(
        ThemeColor textColor,
        ThemeColor mutedTextColor,
        ThemeColor headingColor,
        ThemeColor backgroundColor,
        ThemeColor titleBarColor,
        ThemeColor headerTitleTextColor,
        ThemeColor tableHeaderColor,
        ThemeColor tableHeaderTextColor,
        ThemeColor cardBackgroundColor,
        ThemeColor buttonColor,
        ThemeColor buttonTextColor)
    {
        TextColor = textColor;
        MutedTextColor = mutedTextColor;
        HeadingColor = headingColor;
        BackgroundColor = backgroundColor;
        TitleBarColor = titleBarColor;
        HeaderTitleTextColor = headerTitleTextColor;
        TableHeaderColor = tableHeaderColor;
        TableHeaderTextColor = tableHeaderTextColor;
        CardBackgroundColor = cardBackgroundColor;
        ButtonColor = buttonColor;
        ButtonTextColor = buttonTextColor;
    }

    public bool EnsureDefaults()
    {
        var changed = false;

        if (TextColor == null)
        {
            TextColor = DefaultTextColor.Clone();
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(WindowHeaderText))
        {
            WindowHeaderText = CurrentWindowTitle;
            changed = true;
        }
        else if (string.Equals(WindowHeaderText.Trim(), LegacyWindowTitle, StringComparison.OrdinalIgnoreCase))
        {
            WindowHeaderText = CurrentWindowTitle;
            changed = true;
        }

        if (MutedTextColor == null)
        {
            MutedTextColor = DefaultMutedTextColor.Clone();
            changed = true;
        }

        if (HeadingColor == null)
        {
            HeadingColor = DefaultHeadingColor.Clone();
            changed = true;
        }

        if (BackgroundColor == null)
        {
            BackgroundColor = DefaultBackgroundColor.Clone();
            changed = true;
        }

        if (TitleBarColor == null)
        {
            TitleBarColor = DefaultTitleBarColor.Clone();
            changed = true;
        }

        if (HeaderTitleTextColor == null)
        {
            HeaderTitleTextColor = DefaultHeaderTitleTextColor.Clone();
            changed = true;
        }

        if (TableHeaderColor == null)
        {
            TableHeaderColor = DefaultTableHeaderColor.Clone();
            changed = true;
        }

        if (TableHeaderTextColor == null)
        {
            TableHeaderTextColor = DefaultTableHeaderTextColor.Clone();
            changed = true;
        }

        if (CardBackgroundColor == null)
        {
            CardBackgroundColor = DefaultCardBackgroundColor.Clone();
            changed = true;
        }

        if (ButtonColor == null)
        {
            ButtonColor = DefaultButtonColor.Clone();
            changed = true;
        }

        if (ButtonTextColor == null)
        {
            ButtonTextColor = DefaultButtonTextColor.Clone();
            changed = true;
        }

        if (Version < 20)
        {
            Version = 20;
            changed = true;
        }

        if (Version < 21)
        {
            Version = 21;
            changed = true;
        }

        if (Version < 22)
        {
            Version = 22;
            changed = true;
        }

        return changed;
    }

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}

[Serializable]
public sealed class ThemeColor
{
    public float R { get; set; }
    public float G { get; set; }
    public float B { get; set; }
    public float A { get; set; }

    public Vector4 ToVector4()
    {
        return new Vector4(R, G, B, A);
    }

    public void Set(Vector4 color)
    {
        R = color.X;
        G = color.Y;
        B = color.Z;
        A = color.W;
    }

    public static ThemeColor From(float r, float g, float b, float a)
    {
        return new ThemeColor
        {
            R = r,
            G = g,
            B = b,
            A = a,
        };
    }

    public ThemeColor Clone()
    {
        return From(R, G, B, A);
    }
}
