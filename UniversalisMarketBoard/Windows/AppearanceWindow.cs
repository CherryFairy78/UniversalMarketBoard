using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace UniversalisMarketBoard.Windows;

public sealed class AppearanceWindow : Window, IDisposable
{
    private const float ColorEditorWidth = 255f;

    private readonly Plugin plugin;

    public AppearanceWindow(Plugin plugin)
        : base("Universal Market Board Appearance###UniversalisMarketBoardAppearance")
    {
        this.plugin = plugin;

        Size = new Vector2(460f, 300f);
        SizeCondition = ImGuiCond.FirstUseEver;
        Flags = ImGuiWindowFlags.NoScrollbar;
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
        WindowName = $"{plugin.Configuration.WindowHeaderText} Appearance {plugin.VersionLabel}###UniversalisMarketBoardAppearance";
        ImGui.PushStyleColor(ImGuiCol.Text, plugin.Configuration.TextColor.ToVector4());
        ImGui.TextColored(plugin.Configuration.HeadingColor.ToVector4(), "Appearance");
        ImGui.Spacing();

        ImGui.TextUnformatted("Window Title");
        var headerText = plugin.Configuration.WindowHeaderText;
        if (DrawProminentInput("##header-title", "Type a custom title", ref headerText, 100))
        {
            plugin.Configuration.WindowHeaderText = string.IsNullOrWhiteSpace(headerText)
                ? "Universal Market Board"
                : headerText;
            plugin.Configuration.Save();
        }

        ImGui.Spacing();
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

        ImGui.PopStyleColor();
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

    private static Vector4 Tint(Vector4 color, float brightness, float alphaScale)
    {
        return new Vector4(
            Math.Clamp(color.X * brightness, 0f, 1f),
            Math.Clamp(color.Y * brightness, 0f, 1f),
            Math.Clamp(color.Z * brightness, 0f, 1f),
            Math.Clamp(color.W * alphaScale, 0f, 1f));
    }
}
