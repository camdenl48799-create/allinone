using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;\nusing System.Diagnostics;

namespace ALLINONE;

public partial class MainWindow : Window
{
    private readonly string dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ALLINONE");
    private readonly string settingsPath;
    private Settings settings = new();
    private int setupStep = 1;
    private readonly DispatcherTimer orbTimer = new() { Interval = TimeSpan.FromMilliseconds(70) };
    private int orbFrame;
    private bool gameMode;

    public MainWindow()
    {
        InitializeComponent();
        settingsPath = Path.Combine(dataDir, "settings.json");
        Directory.CreateDirectory(dataDir);
        LoadSettings();
        ApplyTheme(settings.Theme);
        orbTimer.Tick += (_, _) => AnimateOrb();
        orbTimer.Start();
        if (settings.SetupComplete) ShowApp(); else ShowSetup();
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(settingsPath))
                settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsPath)) ?? new Settings();
        }
        catch { settings = new Settings(); }
    }

    private void SaveSettingsToDisk()
    {
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void ShowSetup()
    {
        SetupView.Visibility = Visibility.Visible;
        AppView.Visibility = Visibility.Collapsed;
        SetupBack.Visibility = Visibility.Collapsed;
        RenderSetup();
    }

    private void RenderSetup()
    {
        SetupStep.Text = $"{setupStep:00} / 04";
        SetupBack.Visibility = setupStep > 1 ? Visibility.Visible : Visibility.Collapsed;
        NamePanel.Visibility = setupStep == 2 ? Visibility.Visible : Visibility.Collapsed;
        SafeModeBox.Visibility = setupStep == 3 ? Visibility.Visible : Visibility.Collapsed;

        if (setupStep == 1) { SetupTitle.Text = "Welcome"; SetupDescription.Text = "A native Windows AI workspace with a premium interface, local-first storage, and animated AI state."; }
        if (setupStep == 2) { SetupTitle.Text = "Make it yours"; SetupDescription.Text = "Choose the name ALLINONE should use for you."; NameBox.Text = settings.DisplayName; }
        if (setupStep == 3) { SetupTitle.Text = "Safety & privacy"; SetupDescription.Text = "Safe Mode adds extra guardrails to AI features."; SafeModeBox.IsChecked = settings.SafeMode; }
        if (setupStep == 4) { SetupTitle.Text = "Ready"; SetupDescription.Text = "Your workspace is configured. The model, memory, tools, and account layers can grow independently."; SetupNext.Content = "Launch ALLINONE"; }
    }

    private void SetupNext_Click(object sender, RoutedEventArgs e)
    {
        if (setupStep == 2) settings.DisplayName = string.IsNullOrWhiteSpace(NameBox.Text) ? "User" : NameBox.Text.Trim();
        if (setupStep == 3) settings.SafeMode = SafeModeBox.IsChecked == true;
        if (setupStep < 4) { setupStep++; RenderSetup(); }
        else { settings.SetupComplete = true; SaveSettingsToDisk(); ShowApp(); }
    }

    private void SetupBack_Click(object sender, RoutedEventArgs e)
    {
        if (setupStep > 1) { setupStep--; RenderSetup(); }
    }

    private void ShowApp()
    {
        SetupView.Visibility = Visibility.Collapsed;
        AppView.Visibility = Visibility.Visible;
        SettingsName.Text = settings.DisplayName;\n        AuthStatus.Text = settings.SignedIn ? $"Signed in as {settings.AccountName ?? settings.DisplayName}" : "Not signed in";
        SettingsSafe.IsChecked = settings.SafeMode;
        AddMessage("ALLINONE", $"Ready, {settings.DisplayName}. What are we building?");
        SetOrbState("idle");
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as Button)?.Tag?.ToString();
        ChatPage.Visibility = tag == "Chat" ? Visibility.Visible : Visibility.Collapsed;
        ProjectsPage.Visibility = tag == "Projects" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = tag == "Settings" ? Visibility.Visible : Visibility.Collapsed;\n        AccountPage.Visibility = tag == "Account" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Send_Click(object sender, RoutedEventArgs e) => SendPrompt();

    private void PromptBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SendPrompt();
            e.Handled = true;
        }
    }

    private void SendPrompt()
    {
        var prompt = PromptBox.Text.Trim();
        if (prompt.Length == 0) return;

        AddMessage(settings.DisplayName, prompt);
        PromptBox.Clear();

        gameMode = prompt.Contains("game", StringComparison.OrdinalIgnoreCase)
                || prompt.Contains("gameplay", StringComparison.OrdinalIgnoreCase)
                || prompt.Contains("video game", StringComparison.OrdinalIgnoreCase);

        SetOrbState(gameMode ? "game" : "thinking");
        AddMessage("ALLINONE", LocalFallback(prompt));
        SetOrbState(gameMode ? "game" : "idle");
    }

    private string LocalFallback(string prompt)
    {
        if (gameMode)
            return "Game-creation mode activated. The red AI orb is now representing the game-building workflow. A real model runtime can generate the game's code, assets, systems, and project files here.";
        if (prompt.Contains("code", StringComparison.OrdinalIgnoreCase))
            return "Coding mode activated. ALLINONE is structured for architecture, debugging, project files, memory, and permission-controlled tools.";
        if (prompt.Contains("hello", StringComparison.OrdinalIgnoreCase) || prompt.Contains("hi", StringComparison.OrdinalIgnoreCase))
            return $"Hey {settings.DisplayName}! ALLINONE is ready.";
        return "Request received. The native shell is ready for a high-capability model runtime, persistent memory, retrieval, and permission-controlled tools.";
    }

    private void SetOrbState(string state)
    {
        gameMode = state == "game";
        AIOrbText.Text = state switch
        {
            "game" => "🎮",
            "thinking" => "AI",
            _ => "AI"
        };
        AIOrbText.FontSize = state == "game" ? 42 : 34;
    }

    private void AnimateOrb()
    {
        if (AIOrb == null) return;
        orbFrame++;
        var phase = orbFrame * (gameMode ? 0.32 : 0.12);
        var pulse = gameMode ? 1.0 + Math.Sin(phase) * 0.055 : 1.0 + Math.Sin(phase) * 0.025;
        AIOrbScale.ScaleX = pulse;
        AIOrbScale.ScaleY = pulse;

        if (gameMode && orbFrame % 8 == 0)
        {
            AIOrbText.RenderTransform = new RotateTransform(Math.Sin(orbFrame * 0.8) * 5);
        }
        else if (!gameMode)
        {
            AIOrbText.RenderTransform = new RotateTransform(0);
        }
    }

    private void AddMessage(string author, string text)
    {
        WelcomePanel.Visibility = Visibility.Collapsed;
        var border = new Border
        {
            Background = (Brush)FindResource("Panel2Brush"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 12)
        };
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = author, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("AccentBrush") });
        panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 0), FontSize = 15 });
        border.Child = panel;
        Messages.Children.Add(border);
    }

    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as Button)?.Tag?.ToString();
        ApplyTheme(tag == "Light" ? "Light" : "Dark");
        settings.Theme = tag == "Light" ? "Light" : "Dark";
        SaveSettingsToDisk();
    }

    private void ApplyTheme(string theme)
    {
        var light = string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase);
        Resources["BackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(light ? "#F4F5F7" : "#0B0D12"));
        Resources["PanelBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(light ? "#FFFFFF" : "#12151D"));
        Resources["Panel2Brush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(light ? "#F0F1F4" : "#181C26"));
        Resources["InputBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(light ? "#FFFFFF" : "#0F1219"));
        Resources["HoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(light ? "#E7E8EC" : "#202532"));
        Resources["BorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(light ? "#D7D9DF" : "#292F3B"));
        Resources["TextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(light ? "#15171B" : "#F5F7FA"));
        Resources["MutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(light ? "#626976" : "#9BA3B2"));
    }

    private void GoogleSignUp_Click(object sender, RoutedEventArgs e) => StartBrowserAuth("google", true);\n\n    private void AppleSignUp_Click(object sender, RoutedEventArgs e) => StartBrowserAuth("apple", true);\n\n    private void SignIn_Click(object sender, RoutedEventArgs e) => StartBrowserAuth("account", false);\n\n    private void StartBrowserAuth(string provider, bool signUp)\n    {\n        // Production OAuth endpoints will be configured when the ALLINONE auth service is connected.\n        // The native app intentionally opens the system browser rather than embedding a login page.\n        var endpoint = Environment.GetEnvironmentVariable($"ALLINONE_{provider.ToUpperInvariant()}_AUTH_URL");\n        if (string.IsNullOrWhiteSpace(endpoint))\n        {\n            AuthStatus.Text = "Authentication service not configured yet.";\n            return;\n        }\n        try { Process.Start(new ProcessStartInfo(endpoint) { UseShellExecute = true }); AuthStatus.Text = signUp ? $"Opening {provider} sign-up in your browser…" : "Opening sign-in in your browser…"; }\n        catch { AuthStatus.Text = "Could not open the authentication browser."; }\n    }\n\n    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        settings.DisplayName = string.IsNullOrWhiteSpace(SettingsName.Text) ? "User" : SettingsName.Text.Trim();
        settings.SafeMode = SettingsSafe.IsChecked == true;
        SaveSettingsToDisk();
        StatusText.Text = "● Settings saved";
    }
}

public sealed class Settings
{
    public string DisplayName { get; set; } = "User";
    public bool SafeMode { get; set; } = true;
    public bool SetupComplete { get; set; }
    public string Theme { get; set; } = "Dark";
    public bool SignedIn { get; set; }
    public string? AccountName { get; set; }
}
