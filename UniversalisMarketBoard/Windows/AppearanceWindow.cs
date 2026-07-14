using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;

namespace UniversalisMarketBoard.Windows;

public sealed class AppearanceWindow : Window, IDisposable
{
    private const float ColorEditorWidth = 255f;
    private const string LifestreamGithubUrl = "https://github.com/NightmareXIV/Lifestream";

    private readonly Plugin plugin;
    private SettingsPanel selectedPanel;

    public AppearanceWindow(Plugin plugin)
        : base("Universal Market Board Settings###UniversalisMarketBoardSettings")
    {
        this.plugin = plugin;

        Size = new Vector2(520f, 560f);
        SizeCondition = ImGuiCond.FirstUseEver;
        AllowPinning = false;
        AllowClickthrough = false;
    }

    public void Dispose()
    {
    }

    public override void PreDraw()
    {
        var backgroundColor = plugin.Configuration.BackgroundColor.ToVector4();
        var titleBarColor = plugin.Configuration.TitleBarColor.ToVector4();
        var headerTitleTextColor = plugin.Configuration.HeaderTitleTextColor.ToVector4();
        var mutedTextColor = plugin.Configuration.MutedTextColor.ToVector4();
        var cardColor = plugin.Configuration.CardBackgroundColor.ToVector4();
        var buttonColor = plugin.Configuration.ButtonColor.ToVector4();

        ImGui.PushStyleColor(ImGuiCol.Text, headerTitleTextColor);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, mutedTextColor);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, backgroundColor);
        ImGui.PushStyleColor(ImGuiCol.TitleBg, titleBarColor);
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, Tint(titleBarColor, 1.12f, 1f));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Tint(cardColor, 0.95f, 1f));
        ImGui.PushStyleColor(ImGuiCol.PopupBg, Tint(cardColor, 0.9f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Button, buttonColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Tint(buttonColor, 1.14f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Tint(buttonColor, 0.88f, 1f));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor(10);
    }

    public override void Draw()
    {
        WindowName = $"{plugin.Configuration.WindowHeaderText} Settings {plugin.VersionLabel}###UniversalisMarketBoardSettings";
        ImGui.PushStyleColor(ImGuiCol.Text, plugin.Configuration.TextColor.ToVector4());
        switch (selectedPanel)
        {
            case SettingsPanel.Appearance:
                DrawAppearanceSettings();
                break;
            case SettingsPanel.Debug:
                DrawDebug();
                break;
            case SettingsPanel.Changelog:
                DrawChangelog();
                break;
            default:
                DrawSettingsHome();
                break;
        }

        ImGui.PopStyleColor();
    }

    private void DrawSettingsHome()
    {
        ImGui.TextColored(plugin.Configuration.HeadingColor.ToVector4(), "Settings");
        ImGui.TextDisabled("Choose an area to customise Universal Market Board or view support information.");
        ImGui.Spacing();

        if (DrawStyledButton("Appearance"))
        {
            selectedPanel = SettingsPanel.Appearance;
        }

        ImGui.SameLine();
        if (DrawStyledButton("Debug"))
        {
            selectedPanel = SettingsPanel.Debug;
        }

        ImGui.SameLine();
        if (DrawStyledButton("Changelog"))
        {
            selectedPanel = SettingsPanel.Changelog;
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextColored(plugin.Configuration.HeadingColor.ToVector4(), "Recommended Plugins");
        ImGui.TextDisabled("Universal Market Board works best with the following plugins enabled.");
        DrawPluginStatus(
            "Lifestream",
            plugin.IsLifestreamAvailable,
            "Enables one-click travel to the world shown on a market listing.",
            LifestreamGithubUrl);
    }

    private void DrawAppearanceSettings()
    {
        DrawPanelHeader("Appearance");

        ImGui.TextUnformatted("Behaviour");
        var showContextMenuOption = plugin.Configuration.ShowContextMenuOption;
        if (ImGui.Checkbox("Show right-click menu option", ref showContextMenuOption))
        {
            plugin.Configuration.ShowContextMenuOption = showContextMenuOption;
            plugin.Configuration.Save();
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Quick Themes");
        DrawThemeButtons();
        ImGui.Spacing();
        DrawColorEditor("Text", plugin.Configuration.TextColor);
        DrawColorEditor("Muted Text", plugin.Configuration.MutedTextColor);
        DrawColorEditor("Headings", plugin.Configuration.HeadingColor);
        DrawColorEditor("Background", plugin.Configuration.BackgroundColor);
        DrawColorEditor("Title Bar", plugin.Configuration.TitleBarColor);
        DrawColorEditor("Window Title Text", plugin.Configuration.HeaderTitleTextColor);
        DrawColorEditor("Header", plugin.Configuration.TableHeaderColor);
        DrawColorEditor("Header Text", plugin.Configuration.TableHeaderTextColor);
        DrawColorEditor("Card Backgrounds", plugin.Configuration.CardBackgroundColor);
        DrawColorEditor("Buttons", plugin.Configuration.ButtonColor);
        DrawColorEditor("Button Text", plugin.Configuration.ButtonTextColor);

        ImGui.Spacing();
        if (DrawStyledButton("Reset Appearance"))
        {
            plugin.Configuration.ResetAppearance();
            plugin.Configuration.Save();
        }
    }

    private void DrawChangelog()
    {
        DrawPanelHeader("Changelog");
        ImGui.TextDisabled("All published changes for Universal Market Board.");
        ImGui.Spacing();

        if (ImGui.BeginChild("umb-changelog-history", new Vector2(0f, -40f), true))
        {
            foreach (var line in Changelog.Content.Split('\n'))
            {
                var trimmedLine = line.TrimEnd('\r');
                if (trimmedLine.StartsWith("# ", StringComparison.Ordinal))
                {
                    continue;
                }

                if (trimmedLine.StartsWith("## ", StringComparison.Ordinal))
                {
                    ImGui.TextColored(plugin.Configuration.HeadingColor.ToVector4(), trimmedLine[3..]);
                    continue;
                }

                if (trimmedLine.StartsWith("- ", StringComparison.Ordinal))
                {
                    ImGui.Bullet();
                    ImGui.SameLine();
                    ImGui.TextWrapped(trimmedLine[2..]);
                    continue;
                }

                ImGui.TextWrapped(trimmedLine);
            }
        }

        ImGui.EndChild();

    }

    private void DrawDebug()
    {
        DrawPanelHeader("Debug");
        ImGui.TextDisabled("Copy this report when requesting support. It contains no character details.");
        ImGui.Spacing();

        var report = plugin.GetDebugReport();
        if (DrawStyledButton("Copy Debug Report"))
        {
            ImGui.SetClipboardText(report);
        }

        ImGui.Spacing();
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextMultiline(
            "##debug-report",
            ref report,
            8192,
            new Vector2(-1f, 280f),
            ImGuiInputTextFlags.ReadOnly);
    }

    private void DrawPanelHeader(string title)
    {
        if (DrawStyledButton("Back to Settings"))
        {
            selectedPanel = SettingsPanel.Home;
        }

        ImGui.SameLine();
        ImGui.TextColored(plugin.Configuration.HeadingColor.ToVector4(), title);
        ImGui.Spacing();
    }

    private void DrawPluginStatus(string pluginName, bool isDetected, string description, string? linkUrl = null)
    {
        var pluginNameColor = string.IsNullOrWhiteSpace(linkUrl)
            ? plugin.Configuration.HeadingColor.ToVector4()
            : plugin.Configuration.ButtonColor.ToVector4();
        ImGui.TextColored(pluginNameColor, pluginName);
        if (!string.IsNullOrWhiteSpace(linkUrl) && ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            ImGui.SetTooltip("Open Lifestream on GitHub");
            if (ImGui.IsItemClicked())
            {
                Util.OpenLink(linkUrl);
            }
        }

        ImGui.SameLine();
        ImGui.TextColored(
            isDetected ? new Vector4(0.4f, 0.9f, 0.55f, 1f) : plugin.Configuration.MutedTextColor.ToVector4(),
            isDetected ? "Detected" : "Not detected");
        ImGui.TextWrapped(description);
    }

    private void DrawThemeButtons()
    {
        var themes = new[]
        {
            "Red", "Yellow", "Pink", "Green", "Purple",
            "Orange", "Blue", "Grey", "White", "Black",
        };

        for (var index = 0; index < themes.Length; index++)
        {
            if (index > 0 && index % 5 != 0)
            {
                ImGui.SameLine();
            }

            if (DrawStyledButton(themes[index]))
            {
                plugin.Configuration.ApplyThemePreset(themes[index]);
                plugin.Configuration.Save();
            }
        }
    }

    private void DrawColorEditor(string label, ThemeColor color)
    {
        var value = color.ToVector4();
        ImGui.SetNextItemWidth(ColorEditorWidth);
        if (ImGui.ColorEdit4($"##{label}", ref value))
        {
            color.Set(value);
            plugin.Configuration.Save();
        }

        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
    }

    private bool DrawStyledButton(string label)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, plugin.Configuration.ButtonTextColor.ToVector4());
        var clicked = ImGui.Button(label);
        ImGui.PopStyleColor();
        return clicked;
    }

    private static Vector4 Tint(Vector4 color, float brightness, float alphaScale)
    {
        return new Vector4(
            Math.Clamp(color.X * brightness, 0f, 1f),
            Math.Clamp(color.Y * brightness, 0f, 1f),
            Math.Clamp(color.Z * brightness, 0f, 1f),
            Math.Clamp(color.W * alphaScale, 0f, 1f));
    }

    private enum SettingsPanel
    {
        Home,
        Appearance,
        Debug,
        Changelog,
    }
}
