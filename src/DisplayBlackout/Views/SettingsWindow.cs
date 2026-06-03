using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Rendering;

using DisplayBlackout.Platform;
using DisplayBlackout.Services;
using ProTranslate.MewUI;

namespace DisplayBlackout.Views;

internal sealed class SettingsView : UserControl
{
    public SettingsView(BlackoutService blackoutService, DisplayNumberService displayNumberService, IDisplayPowerService displayPowerService, SettingsService? settingsService = null)
    {
        var monitorPicker = new MonitorPickerView(blackoutService, displayPowerService);

        var blackoutToggle = new ToggleSwitch()
            .BindIsChecked(blackoutService.IsBlackedOut);

        var opacitySlider = new Slider()
            .Width(200)
            .SmallChange(1)
            .Maximum(100)
            .Bind(RangeBase.ValueProperty, blackoutService.Opacity, v => (double)v, v => (int)Math.Round(v));

        var clickThroughToggle = new ToggleSwitch()
            .BindIsChecked(blackoutService.ClickThrough);

        Content = new ScrollViewer()
            .AutoVerticalScroll()
            .Content(
                new StackPanel()
                    .Vertical()
                    .Padding(24)
                    .Spacing(4)
                    .Children(
                        // Instructions
                        new TextBlock()
                            .Bind(TextBlock.TextProperty, T("Settings.InstructionText"))
                            .WithTheme((t, c) => c.Foreground(t.Palette.DisabledText))
                            .Margin(0, 0, 0, 12),

                        // Monitor picker
                        new Border()
                            .CornerRadius(8)
                            .Padding(24)
                            .Margin(0, 0, 0, 4)
                            .WithTheme((t, c) => c
                                .Background(t.Palette.ContainerBackground)
                                .BorderBrush(t.Palette.ControlBorder)
                                .BorderThickness(1))
                            .Child(monitorPicker),

                        // Identify button
                        new Button()
                            .Content(new TextBlock().Text("123").FontSize(11).Bold())
                            .Padding(6, 3)
                            .CornerRadius(4)
                            .ToolTip("Identify displays")
                            .HorizontalAlignment(HorizontalAlignment.Right)
                            .Margin(0, 0, 0, 8)
                            .OnClick(() => displayNumberService.ShowFor(TimeSpan.FromSeconds(2)))
                            .WithTheme((t, c) => c
                                .Background(t.IsDark
                                    ? Color.FromArgb(160, 50, 50, 50)
                                    : Color.FromArgb(160, 200, 200, 200))
                                .BorderThickness(0)),

                        // Display blackout toggle
                        SettingsCard(
                            new Image() { Source = ImageSource.FromResource(System.Reflection.Assembly.GetExecutingAssembly(), "icon.ico") }
                                .Width(20).Height(20),
                            T("Settings.DisplayBlackout"), T("Settings.DisplayBlackoutDesc"), blackoutToggle),

                        // Opacity slider
                        SettingsCard("\uF08C", T("Settings.Opacity"), T("Settings.OpacityDesc"), opacitySlider),

                        // Click through toggle
                        SettingsCard("\uE8B0", T("Settings.ClickThrough"), T("Settings.ClickThroughDesc"), clickThroughToggle),

                        // Activation section header
                        new TextBlock()
                            .Bind(TextBlock.TextProperty, T("Settings.Activation"))
                            .Bold()
                            .Margin(0, 24, 0, 4),

                        // Activation shortcut display
                        CreateShortcutCard(),

                        // Appearance
                        new Expander()
                            .Header(new TextBlock().Bind(TextBlock.TextProperty, T("Settings.Appearance")).Bold())
                            .Margin(0, 20, 0, 0)
                            .Content(
                                new StackPanel()
                                    .Vertical()
                                    .Margin(0, 4, 0, 0)
                                    .Spacing(4)
                                    .Children(
                                        // Theme
                                        SettingsCard("\uE793", T("Settings.Theme"), T("Settings.ThemeDesc"),
                                            CreateThemePicker(settingsService)),

                                        // Language
                                        SettingsCard("\uE774", T("Settings.Language"), T("Settings.LanguageDesc"),
                                            CreateLanguagePicker(settingsService)),

                                        // Accent color
                                        SettingsCard("\uE790", T("Settings.AccentColor"), T("Settings.AccentColorDesc"),
                                            CreateAccentPicker(settingsService))
                                    ))
                    ));
    }

    // Shorthand: creates a reactive ObservableValue<string> from a translation key
    private static ObservableValue<string> T(string key) =>
        TranslationExtensions.TranslationBinding(key);

    private static FrameworkElement SettingsCard(string icon, ObservableValue<string> header, ObservableValue<string> description, FrameworkElement action)
    {
        var iconElement = new TextBlock()
            .Text(icon)
            .FontFamily("Segoe MDL2 Assets")
            .FontSize(16)
            .CenterVertical()
            .WithTheme((t, c) => c.Foreground(t.Palette.WindowText));

        return SettingsCard(iconElement, header, description, action);
    }

    private static FrameworkElement SettingsCard(FrameworkElement iconElement, ObservableValue<string> header, ObservableValue<string> description, FrameworkElement action)
    {
        return new Border()
            .CornerRadius(4)
            .BorderThickness(1)
            .Padding(16)
            .WithTheme((t, c) => c
                .Background(t.Palette.ControlBackground)
                .BorderBrush(t.Palette.ControlBackground.Lerp(t.Palette.ControlBorder, 0.5)))
            .Child(
                new DockPanel()
                    .Children(
                        action.DockRight().CenterVertical(),
                        iconElement.Margin(0, 0, 12, 0),
                        new StackPanel()
                            .Vertical()
                            .Spacing(2)
                            .CenterVertical()
                            .Children(
                                new TextBlock().Bind(TextBlock.TextProperty, header).SemiBold(),
                                new TextBlock().Bind(TextBlock.TextProperty, description)
                                    .WithTheme((t, c) => c.Foreground(t.Palette.DisabledText))
                            )
                    ));
    }

    private static int ThemeToIndex(string? theme) => theme switch
    {
        "Light" => 1,
        "Dark" => 2,
        _ => 0
    };

    // (code, translation key) — code is the stable value stored in settings
    private static readonly (string Code, string Key)[] ThemeOptions =
    [
        ("System", "Theme.System"),
        ("Light",  "Theme.Light"),
        ("Dark",   "Theme.Dark"),
    ];

    private static FrameworkElement CreateThemePicker(SettingsService? settingsService)
    {
        var combo = new ComboBox().Width(120);

        void RefreshItems() =>
            combo.Items(ThemeOptions,
                t => ProTranslate.MewUI.TranslationService.Source.Translate(t.Key),
                t => t.Code);

        RefreshItems();
        combo.SelectedIndex(ThemeToIndex(settingsService?.LoadTheme()));

        // Re-translate the dropdown labels when the language changes.
        var cultureWatcher = T("Theme.System");
        cultureWatcher.Changed += () =>
        {
            var savedIndex = combo.SelectedIndex;
            RefreshItems();
            combo.SelectedIndex(savedIndex);
        };
        combo.Tag = cultureWatcher;   // strong ref keeps cultureWatcher alive

        combo.OnSelectionChanged(_ =>
        {
            var idx = combo.SelectedIndex;
            if (idx < 0 || idx >= ThemeOptions.Length) return;
            var code = ThemeOptions[idx].Code;
            var variant = code switch
            {
                "Light" => ThemeVariant.Light,
                "Dark"  => ThemeVariant.Dark,
                _       => ThemeVariant.System
            };
            Application.Current?.SetTheme(variant);
            settingsService?.SaveTheme(code);
        });

        return combo;
    }

    private static readonly (string Code, string Label)[] Languages =
    [
        ("en",      "English"),
        ("zh-Hans", "简体中文"),
        ("zh-Hant", "繁體中文"),
        ("ko",      "한국어"),
        ("ja",      "日本語"),
        ("de",      "Deutsch"),
        ("fr",      "Français"),
        ("it",      "Italiano"),
        ("es",      "Español"),
        ("pl",      "Polski"),
    ];

    private static FrameworkElement CreateLanguagePicker(SettingsService? settingsService)
    {
        // Show whichever language is actually active (saved or auto-detected on first launch)
        var activeCode = ProTranslate.MewUI.TranslationService.Culture.Name;
        var selectedIndex = Math.Max(0, Array.FindIndex(Languages, l =>
            l.Code.Equals(activeCode, StringComparison.OrdinalIgnoreCase)));

        return new ComboBox()
            .Width(120)
            .Items(Languages.Select(l => l.Label).ToArray())
            .SelectedIndex(selectedIndex)
            .OnSelectionChanged(selected =>
            {
                if (selected is not string label) return;
                var match = Array.Find(Languages, l => l.Label == label);
                if (match == default) return;
                ProTranslate.MewUI.TranslationService.Culture =
                    System.Globalization.CultureInfo.GetCultureInfo(match.Code);
                settingsService?.SaveLanguage(match.Code);
            });
    }

    private static FrameworkElement CreateAccentPicker(SettingsService? settingsService)
    {
        var panel = new StackPanel().Horizontal().Spacing(4);

        foreach (var accent in BuiltInAccent.Accents)
        {
            var a = accent; // capture
            var color = a.GetAccentColor(false);
            var swatch = new Border()
                .Width(24).Height(24)
                .CornerRadius(12)
                .Background(color)
                .Cursor(CursorType.Hand)
                .OnMouseDown(_ =>
                {
                    Application.Current?.SetAccent(a);
                    settingsService?.SaveAccent(a.ToString());
                });

            panel.Add(swatch);
        }

        return panel;
    }

    private static FrameworkElement CreateShortcutCard()
    {
        var keys = OperatingSystem.IsMacOS()
            ? new StackPanel().Horizontal().Spacing(4)
                .Children(KeyBadge("\u2318"), KeyBadge("\u21E7"), KeyBadge("B"))
            : new StackPanel().Horizontal().Spacing(4)
                .Children(WinKeyBadge(), KeyBadge("Shift"), KeyBadge("B"));

        return SettingsCard("\uEDA7", T("Settings.ActivationShortcut"), T("Settings.ActivationShortcutDesc"), keys);
    }

    private static FrameworkElement WinKeyBadge()
    {
        // Windows logo: 4 squares matching the original PathIcon SVG data
        var winLogo = new PathShape
        {
            Data = PathGeometry.Parse("M9 20H0V11H9V20ZM20 20H11V11H20V20ZM9 9H0V0H9V9ZM20 9H11V0H20V9Z"),
            Stretch = Stretch.Uniform,
        };
        winLogo.Width(14).Height(14).CenterHorizontal().CenterVertical()
            .WithTheme((t, _) => winLogo.Fill(t.Palette.AccentText));

        return new Border()
            .Padding(6, 4)
            .MinWidth(32)
            .MinHeight(32)
            .WithTheme((t, c) => c
                .CornerRadius(t.Metrics.ControlCornerRadius)
                .Background(t.Palette.Accent)
                .BorderBrush(t.Palette.AccentBorderHotOverlay)
                .BorderThickness(1))
            .Child(winLogo);
    }

    private static FrameworkElement KeyBadge(string text)
    {
        var label = new TextBlock()
            .Text(text)
            .CenterHorizontal()
            .CenterVertical();

        return new Border()
            .Padding(6, 4)
            .MinWidth(32)
            .MinHeight(32)
            .WithTheme((t, c) => c
                .CornerRadius(t.Metrics.ControlCornerRadius)
                .Foreground(t.Palette.AccentText)
                .Background(t.Palette.Accent)
                .BorderBrush(t.Palette.AccentBorderHotOverlay)
                .BorderThickness(1))
            .Child(label);
    }
}
