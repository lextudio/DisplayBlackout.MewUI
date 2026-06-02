using System.Reflection;

using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Platform.MacOS;

using DisplayBlackout.Platform;
using DisplayBlackout.Platform.MacOS;
using DisplayBlackout.Platform.Win32;
using DisplayBlackout.Services;
using System.Globalization;
using DisplayBlackout.Views;
using ProTranslate;
using ProTranslate.Generated;
#if DEBUG
using LeXtudio.DevFlow.Agent.MewUI;
#endif

IDisplayService displayService;
IBlackoutOverlayFactory overlayFactory;
ISystemEventService systemEvents;
IAppIndicator appIndicator;
IDisplayPowerService displayPowerService;

var asm = Assembly.GetExecutingAssembly();

if (OperatingSystem.IsMacOS())
{
    MacOSPlatform.Register();
    MewVGMacOSBackend.Register();

    var platformServices = new MacOSPlatformServices();
    displayService = platformServices.DisplayService;
    overlayFactory = platformServices.OverlayFactory;
    systemEvents = platformServices.SystemEvents;
    appIndicator = platformServices.CreateAppIndicator(asm);
    displayPowerService = platformServices.DisplayPowerService;
}
else if (OperatingSystem.IsWindows())
{
    Win32Platform.Register();
    GdiBackend.Register();

    var platformServices = new Win32PlatformServices();
    displayService = platformServices.DisplayService;
    overlayFactory = platformServices.OverlayFactory;
    systemEvents = platformServices.SystemEvents;
    appIndicator = platformServices.CreateAppIndicator(asm);
    displayPowerService = platformServices.DisplayPowerService;
}
else
{
    throw new PlatformNotSupportedException("DisplayBlackout.MewUI currently supports Windows and macOS.");
}

// For Windows 7
//FontResources.Register(typeof(SettingsView).Assembly!.GetManifestResourceStream("DisplayBlackout.Resources.SEGMDL2.TTF")!, "Segoe MDL2 Assets");

// Required on macOS for Segoe MDL2 Assets icons
if (OperatingSystem.IsMacOS())
    FontResources.Register(typeof(SettingsView).Assembly!.GetManifestResourceStream("DisplayBlackout.Resources.SEGMDL2.TTF")!, "Segoe MDL2 Assets");

// Parse command-line arguments
var cliArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
bool openSettings = cliArgs.Any(arg => arg.Equals("/OpenSettings", StringComparison.OrdinalIgnoreCase));
bool resetSettings = cliArgs.Any(arg => arg.Equals("/ResetSettings", StringComparison.OrdinalIgnoreCase));

// Initialize services
var settingsService = new SettingsService();
if (resetSettings)
{
    settingsService.ResetAll();
}

// Initialize ProTranslate — use saved language, or fall back to system UI language on first launch
var defaultCulture = CultureInfo.GetCultureInfo("en");
string[] supportedLanguages = ["en", "zh-Hans", "zh-Hant", "ko", "ja"];

var savedLanguage = settingsService.LoadLanguage();
CultureInfo initialCulture;
if (savedLanguage != null)
{
    initialCulture = CultureInfo.GetCultureInfo(savedLanguage);
}
else
{
    // First launch: pick the best match from the system UI language chain
    var systemCulture = CultureInfo.CurrentUICulture;
    initialCulture = defaultCulture;
    // Walk the culture chain: zh-TW → zh → invariant
    var candidates = new List<CultureInfo> { systemCulture };
    var parent = systemCulture.Parent;
    while (parent != null && parent != CultureInfo.InvariantCulture)
    {
        candidates.Add(parent);
        parent = parent.Parent;
    }
    foreach (var candidate in candidates)
    {
        var name = candidate.Name;
        if (supportedLanguages.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            initialCulture = candidate;
            break;
        }
        // zh-TW / zh-HK / zh-MO → zh-Hant
        if (name.StartsWith("zh-", StringComparison.OrdinalIgnoreCase) &&
            !name.Equals("zh-Hans", StringComparison.OrdinalIgnoreCase))
        {
            initialCulture = CultureInfo.GetCultureInfo("zh-Hant");
            break;
        }
    }
}

var cultures = new CultureService(initialCulture);
var translationProvider = new ProTranslateGeneratedTranslationProvider("Strings");
var translations = new global::ProTranslate.TranslationService(
    translationProvider, cultures,
    new TranslationFallbackOptions { DefaultCulture = defaultCulture });
ProTranslate.MewUI.TranslationService.UseService(translations, cultures);

var blackoutService = new BlackoutService(settingsService, displayService, overlayFactory);
var displayNumberService = new DisplayNumberService(displayService);

// System events (hotkey, display change, focus change)
bool hotkeyAvailable = systemEvents.Initialize();
systemEvents.HotkeyPressed += (_, _) => blackoutService.Toggle();
systemEvents.DisplayChanged += (_, _) =>
{
    if (blackoutService.IsBlackedOut.Value)
    {
        blackoutService.Restore();
    }
};
systemEvents.FocusChanged += (_, _) => blackoutService.BringAllToFront();

appIndicator.Clicked += () => blackoutService.Toggle();
appIndicator.ExitRequested += async () =>
{
    var sleeping = displayService.GetDisplays()
        .Where(d => displayPowerService.IsAsleep(d.Id))
        .ToList();

    if (sleeping.Count > 0)
    {
        var names = string.Join(", ", sleeping.Select(d => $"Display {d.Name}"));
        bool confirmed = await MessageBox.ConfirmAsync(
            $"{names} is physically powered off.\n" +
            "If you quit now, it will stay off after restarting the app.\n" +
            "You may need to disconnect and reconnect the cable to recover it.\n\n" +
            "Quit anyway?",
            owner: Application.Current?.AllWindows.FirstOrDefault());
        if (!confirmed) return;
    }

    Environment.Exit(0);
};
appIndicator.Show();

blackoutService.IsBlackedOut.Subscribe(() => appIndicator.SetActive(blackoutService.IsBlackedOut.Value));

// Restore saved theme/accent
var savedAccent = settingsService.LoadAccent();
var accent = Enum.TryParse<Accent>(savedAccent, out var parsed) ? parsed : Accent.Pink;
var savedTheme = settingsService.LoadTheme() switch
{
    "Light" => ThemeVariant.Light,
    "Dark" => ThemeVariant.Dark,
    _ => ThemeVariant.System
};

Application.Create()
    .UseTheme(savedTheme)
    .UseAccent(accent)
    .BuildMainWindow(() =>
    {
        var window = new MainWindow(blackoutService, displayNumberService, displayPowerService, settingsService, hotkeyAvailable);
        appIndicator.DoubleClicked += () => window.Show();
#if DEBUG
        window.OnLoaded(() => Application.Current.AddMewUIDevFlowAgent());
#endif
        return window;
    })
    .Run();

// Cleanup
appIndicator.Dispose();
blackoutService.Dispose();
systemEvents.Dispose();
