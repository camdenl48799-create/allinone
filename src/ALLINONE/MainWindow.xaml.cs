using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ALLINONE;

public partial class MainWindow : Window
{
    private readonly string dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ALLINONE");
    private readonly string settingsPath;
    private readonly string historyPath;
    private readonly ClerkAuthService clerk;
    private readonly UpdateService updater = new();
    private readonly SearchInOne searchInOne = new();
    private readonly ModelService modelService = new();
    private readonly ApiKeyService apiKeys;
    private readonly AllInOneApiServer apiServer;
    private Settings settings = new();
    private int setupStep = 1;
    private readonly DispatcherTimer orbTimer = new() { Interval = TimeSpan.FromMilliseconds(70) };
    private int orbFrame;
    private bool gameMode;
    private bool searchMode;
    private bool mentionPopupOpen;

    public MainWindow()
    {
        InitializeComponent();
        settingsPath = Path.Combine(dataDir, "settings.json");
        historyPath = Path.Combine(dataDir, "chat-history.json");
        Directory.CreateDirectory(dataDir);
        clerk = new ClerkAuthService(dataDir);
        apiKeys = new ApiKeyService(dataDir);
        apiServer = new AllInOneApiServer(apiKeys, modelService);
        apiServer.Start();
        LoadSettings();
        ApplyTheme(settings.Theme);
        orbTimer.Tick += (_, _) => AnimateOrb();
        orbTimer.Start();
        if (settings.SetupComplete) ShowApp(); else ShowSetup();
        _ = CheckForUpdatesAsync();
    }

    private void LoadSettings()
    {
        try { if (File.Exists(settingsPath)) settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsPath)) ?? new Settings(); }
        catch { settings = new Settings(); }
    }

    private void SaveSettingsToDisk()
    {
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void ShowSetup()
    {
        SetupView.Visibility = Visibility.Visible; AppView.Visibility = Visibility.Collapsed; SetupBack.Visibility = Visibility.Collapsed; RenderSetup();
    }

    private void RenderSetup()
    {
        SetupStep.Text = $"{setupStep:00} / 04";
        SetupBack.Visibility = setupStep > 1 ? Visibility.Visible : Visibility.Collapsed;
        NamePanel.Visibility = setupStep == 2 ? Visibility.Visible : Visibility.Collapsed;
        SafeModeBox.Visibility = setupStep == 3 ? Visibility.Visible : Visibility.Collapsed;
        if (setupStep == 1) { SetupTitle.Text = "Welcome"; SetupDescription.Text = "A native Windows AI workspace with SearchInOne, persistent chat, secure account sessions, and a real update system."; }
        if (setupStep == 2) { SetupTitle.Text = "Make it yours"; SetupDescription.Text = "Choose the name ALLINONE should use for you."; NameBox.Text = settings.DisplayName; }
        if (setupStep == 3) { SetupTitle.Text = "Safety & privacy"; SetupDescription.Text = "Safe Mode adds extra guardrails to AI features."; SafeModeBox.IsChecked = settings.SafeMode; }
        if (setupStep == 4) { SetupTitle.Text = "Ready"; SetupDescription.Text = "Your workspace is configured. The model runtime, CodeInOne, SearchInOne, memory, and tools can evolve independently."; SetupNext.Content = "Launch ALLINONE"; }
    }

    private void SetupNext_Click(object sender, RoutedEventArgs e)
    {
        if (setupStep == 2) settings.DisplayName = string.IsNullOrWhiteSpace(NameBox.Text) ? "User" : NameBox.Text.Trim();
        if (setupStep == 3) settings.SafeMode = SafeModeBox.IsChecked == true;
        if (setupStep < 4) { setupStep++; RenderSetup(); } else { settings.SetupComplete = true; SaveSettingsToDisk(); ShowApp(); }
    }

    private void SetupBack_Click(object sender, RoutedEventArgs e) { if (setupStep > 1) { setupStep--; RenderSetup(); } }

    private void ShowApp()
    {
        SetupView.Visibility = Visibility.Collapsed; AppView.Visibility = Visibility.Visible;
        SettingsName.Text = settings.DisplayName; SettingsSafe.IsChecked = settings.SafeMode;
        RenderApiKeys();
        AuthStatus.Text = settings.SignedIn ? $"Signed in as {settings.AccountName ?? settings.DisplayName}" : (clerk.IsConfigured ? "Not signed in" : "Clerk setup required");
        RestoreHistory();
        if (Messages.Children.Count == 0) AddMessage("ALLINONE", $"Ready, {settings.DisplayName}. What are we building?");
        SetOrbState("idle"); StatusText.Text = modelService.IsConfigured ? $"● {modelService.ModelId}" : "● Local AI model not installed";
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as Button)?.Tag?.ToString();
        ChatPage.Visibility = tag == "Chat" ? Visibility.Visible : Visibility.Collapsed;
        ProjectsPage.Visibility = tag == "Projects" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = tag == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        AccountPage.Visibility = tag == "Account" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SearchMode_Click(object sender, RoutedEventArgs e)
    {
        searchMode = !searchMode;
        SearchButton.Content = searchMode ? "SearchInOne ON" : "SearchInOne";
        StatusText.Text = searchMode ? "● SearchInOne ready" : "● Auto-updater active";
        PromptBox.Focus();
    }

    private void Send_Click(object sender, RoutedEventArgs e) => _ = SendPromptAsync();

    private void PromptBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && mentionPopupOpen) { HideMentionPopup(); e.Handled = true; return; }
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { _ = SendPromptAsync(); e.Handled = true; }
    }

    private void PromptBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = PromptBox.Text;
        var caret = PromptBox.CaretIndex;
        var at = text.LastIndexOf('@', Math.Max(0, caret - 1));
        if (at < 0 || (at > 0 && !char.IsWhiteSpace(text[at - 1]))) { HideMentionPopup(); return; }

        var query = text[(at + 1)..caret];
        if (query.Contains(' ') || query.Contains('\n')) { HideMentionPopup(); return; }

        var options = new[] { "ALLINONE", "SearchInOne", "CodeInOne", "Project", "File", "Website", "YouTube", "Model" }
            .Where(x => x.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
        ShowMentionPopup(options);
    }

    private void ShowMentionPopup(IReadOnlyList<string> options)
    {
        MentionList.Children.Clear();
        if (options.Count == 0) { HideMentionPopup(); return; }

        foreach (var option in options)
        {
            var button = new Button
            {
                Content = option switch
                {
                    "ALLINONE" => "✦  ALLINONE       Use your own ALLINONE AI",
                    "SearchInOne" => "⌕  SearchInOne   Search the internet",
                    "CodeInOne" => "⌘  CodeInOne      Build and code",
                    "Project" => "◈  Project          Attach a project",
                    "File" => "▣  File                Attach a local file",
                    "Website" => "◉  Website         Research a website",
                    "YouTube" => "▶  YouTube        Search YouTube",
                    _ => "✦  Model              Choose model"
                },
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(12, 9),
                Margin = new Thickness(0, 1, 0, 1)
            };
            button.Click += (_, _) => SelectMention(option);
            MentionList.Children.Add(button);
        }

        MentionPopup.Visibility = Visibility.Visible;
        mentionPopupOpen = true;
    }

    private void SelectMention(string mention)
    {
        var text = PromptBox.Text;
        var caret = PromptBox.CaretIndex;
        var at = text.LastIndexOf('@', Math.Max(0, caret - 1));
        if (at < 0) return;

        var replacement = "@" + mention + " ";
        PromptBox.Text = text.Remove(at, caret - at).Insert(at, replacement);
        PromptBox.CaretIndex = at + replacement.Length;
        HideMentionPopup();
        PromptBox.Focus();

        if (mention == "SearchInOne")
        {
            searchMode = true;
            SearchButton.Content = "SearchInOne ON";
            StatusText.Text = "● SearchInOne ready";
        }
    }

    private void HideMentionPopup()
    {
        MentionPopup.Visibility = Visibility.Collapsed;
        mentionPopupOpen = false;
    }

    private async Task SendPromptAsync()
    {
        var prompt = PromptBox.Text.Trim();
        if (prompt.Length == 0) return;
        AddMessage(settings.DisplayName, prompt);
        PromptBox.Clear();

        gameMode = prompt.Contains("game", StringComparison.OrdinalIgnoreCase) || prompt.Contains("gameplay", StringComparison.OrdinalIgnoreCase) || prompt.Contains("video game", StringComparison.OrdinalIgnoreCase);
        SetOrbState(gameMode ? "game" : "thinking");

        var hasSearchMention = Regex.IsMatch(prompt, @"@SearchInOne\b", RegexOptions.IgnoreCase);
        var hasWebsiteMention = Regex.IsMatch(prompt, @"@Website\b", RegexOptions.IgnoreCase);
        var hasYouTubeMention = Regex.IsMatch(prompt, @"@YouTube\b", RegexOptions.IgnoreCase);
        var shouldSearch = searchMode || hasSearchMention || hasWebsiteMention || hasYouTubeMention ||
                           prompt.StartsWith("research ", StringComparison.OrdinalIgnoreCase) ||
                           prompt.StartsWith("search ", StringComparison.OrdinalIgnoreCase);
        string? searchQuery = null;
        if (shouldSearch)
        {
            searchQuery = Regex.Replace(prompt, @"@(SearchInOne|Website|YouTube)\b", "", RegexOptions.IgnoreCase).Trim();
            if (prompt.StartsWith("research ", StringComparison.OrdinalIgnoreCase))
                searchQuery = searchQuery["research ".Length..].Trim();
            else if (prompt.StartsWith("search ", StringComparison.OrdinalIgnoreCase))
                searchQuery = searchQuery["search ".Length..].Trim();

            if (string.IsNullOrWhiteSpace(searchQuery))
                searchQuery = prompt;

            await RunSearchAsync(searchQuery);
        }

        try
        {
            string? searchContext = null;
            if (shouldSearch && searchQuery is not null)
                searchContext = await BuildSearchContextAsync(searchQuery);

            var role = prompt.Contains("@CodeInOne", StringComparison.OrdinalIgnoreCase)
                ? "code"
                : gameMode ? "game"
                : shouldSearch ? "research" : "general";

            StatusText.Text = $"● {modelService.ModelId} thinking…";
            var answer = await modelService.GenerateAsync(prompt, searchContext, role);
            AddMessage("ALLINONE", answer);
        }
        catch (Exception ex)
        {
            AddMessage("ALLINONE", $"The model could not complete that request: {ex.Message}");
        }

        SetOrbState(gameMode ? "game" : "idle");
        SaveHistory();
    }

    private async Task RunSearchAsync(string query)
    {
        try
        {
            StatusText.Text = "● SearchInOne researching…";
            var results = await searchInOne.SearchAsync(query);
            if (results.Count == 0) { AddMessage("SearchInOne", $"No web results were returned for: {query}"); return; }

            AddMessage("SearchInOne", $"Found {results.Count} web results for: {query}");
            foreach (var result in results)
            {
                var button = new Button
                {
                    Content = result.Title,
                    ToolTip = result.Url,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Background = (Brush)FindResource("Panel2Brush"),
                    Margin = new Thickness(0, 0, 0, 7),
                    Padding = new Thickness(12, 9)
                };
                button.Click += (_, _) => OpenUrl(result.Url);
                Messages.Children.Add(button);
            }

            var sourcePanel = new WrapPanel { Margin = new Thickness(0, 4, 0, 12) };
            foreach (var source in searchInOne.BuildSources(query))
            {
                var button = new Button { Content = source.Name, ToolTip = source.Purpose, Padding = new Thickness(10, 6), Margin = new Thickness(0, 0, 7, 7) };
                button.Click += (_, _) => OpenUrl(source.Url);
                sourcePanel.Children.Add(button);
            }
            Messages.Children.Add(sourcePanel);
            MessagesScroll.ScrollToEnd();
        }
        catch (Exception ex) { AddMessage("SearchInOne", $"Internet search failed safely: {ex.Message}"); }
        finally { StatusText.Text = "● Ready"; }
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    private async Task<string?> BuildSearchContextAsync(string query)
    {
        var results = await searchInOne.SearchAsync(query);
        if (results.Count == 0) return null;

        return string.Join("\n", results.Select((r, i) =>
            $"[{i + 1}] {r.Title}\nURL: {r.Url}"));
    }

    private async Task CheckForUpdatesAsync()
    {
        var update = await updater.CheckAsync();
        if (update is null) return;
        await Dispatcher.InvokeAsync(() => StatusText.Text = $"● Updating to {update.Version}…");
        var installed = await updater.InstallAsync(update);
        if (installed) { MessageBox.Show($"ALLINONE {update.Version} was downloaded and will be installed now.", "ALLINONE update", MessageBoxButton.OK, MessageBoxImage.Information); Application.Current.Shutdown(); }
        else await Dispatcher.InvokeAsync(() => StatusText.Text = $"● Update {update.Version} available");
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "● Checking for updates…";
        var update = await updater.CheckNowAsync();
        if (update is null) { StatusText.Text = "● ALLINONE is up to date"; return; }
        StatusText.Text = $"● Updating to {update.Version}…";
        if (await updater.InstallAsync(update)) Application.Current.Shutdown(); else StatusText.Text = $"● Update {update.Version} available";
    }

    private string LocalFallback(string prompt, bool searched)
    {
        if (gameMode) return "CodeInOne game-building mode is ready. A model runtime can generate the game's code, assets, systems, and project files here.";
        if (searched) return "SearchInOne returned web sources above. The next model layer can use those results to produce a sourced answer instead of pretending it already knows everything.";
        if (prompt.Contains("code", StringComparison.OrdinalIgnoreCase)) return "CodeInOne mode activated. The workspace is ready for architecture, debugging, project files, memory, and permission-controlled tools.";
        if (prompt.Contains("hello", StringComparison.OrdinalIgnoreCase) || prompt.Contains("hi", StringComparison.OrdinalIgnoreCase)) return $"Hey {settings.DisplayName}! ALLINONE is ready.";
        return "Request received. The native shell is ready for a high-capability model runtime, persistent memory, retrieval, and permission-controlled tools.";
    }

    private void SetOrbState(string state)
    {
        gameMode = state == "game";
        AIOrbText.Text = state == "game" ? "🎮" : state == "thinking" ? "…" : "AI";
        AIOrbText.FontSize = state == "game" ? 42 : 34;
    }

    private void AnimateOrb()
    {
        if (AIOrb == null) return;
        orbFrame++;
        var phase = orbFrame * (gameMode ? 0.32 : 0.12);
        var pulse = gameMode ? 1.0 + Math.Sin(phase) * 0.055 : 1.0 + Math.Sin(phase) * 0.025;
        AIOrbScale.ScaleX = pulse; AIOrbScale.ScaleY = pulse;
        AIOrbText.RenderTransform = gameMode ? new RotateTransform(Math.Sin(orbFrame * 0.8) * 5) : new RotateTransform(0);
    }

    private void AddMessage(string author, string text)
    {
        WelcomePanel.Visibility = Visibility.Collapsed;
        var border = new Border { Background = (Brush)FindResource("Panel2Brush"), BorderBrush = (Brush)FindResource("BorderBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 12) };
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = author, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("AccentBrush") });
        panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 0), FontSize = 15 });
        border.Child = panel; Messages.Children.Add(border); MessagesScroll.ScrollToEnd();
    }

    private void SaveHistory()
    {
        try
        {
            var entries = Messages.Children.OfType<Border>().Select(b => (b.Child as StackPanel)?.Children.OfType<TextBlock>().ToArray()).Where(x => x?.Length >= 2).Select(x => new ChatEntry(x![0].Text, x[1].Text)).TakeLast(100).ToList();
            File.WriteAllText(historyPath, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private void RestoreHistory()
    {
        try
        {
            if (!File.Exists(historyPath)) return;
            var entries = JsonSerializer.Deserialize<List<ChatEntry>>(File.ReadAllText(historyPath)) ?? [];
            foreach (var entry in entries.TakeLast(100)) AddMessage(entry.Author, entry.Text);
        }
        catch { }
    }

    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as Button)?.Tag?.ToString(); ApplyTheme(tag == "Light" ? "Light" : "Dark"); settings.Theme = tag == "Light" ? "Light" : "Dark"; SaveSettingsToDisk();
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

    private async void GoogleSignUp_Click(object sender, RoutedEventArgs e) => await BeginClerkAuthAsync("Google");
    private async void AppleSignUp_Click(object sender, RoutedEventArgs e) => await BeginClerkAuthAsync("Apple");
    private async void SignIn_Click(object sender, RoutedEventArgs e) => await BeginClerkAuthAsync("Clerk");

    private async Task BeginClerkAuthAsync(string provider)
    {
        if (!clerk.IsConfigured) { AuthStatus.Text = "Clerk isn't configured yet. Add your Clerk frontend API URL and public OAuth client ID."; return; }
        try
        {
            AuthStatus.Text = "Opening Clerk in your browser…";
            var user = await clerk.SignInAsync(provider);
            settings.SignedIn = true; settings.AccountName = user?.name ?? user?.email ?? settings.DisplayName; SaveSettingsToDisk();
            AuthStatus.Text = $"Signed in as {settings.AccountName}";
        }
        catch (OperationCanceledException) { AuthStatus.Text = "Authentication was canceled."; }
        catch (Exception ex) { AuthStatus.Text = $"Authentication failed: {ex.Message}"; }
    }


    private void GenerateApiKey_Click(object sender, RoutedEventArgs e)
    {
        var name = string.IsNullOrWhiteSpace(ApiKeyNameBox.Text) ? "My ALLINONE key" : ApiKeyNameBox.Text.Trim();
        var (_, secret) = apiKeys.Create(name);
        ApiKeyResult.Text = $"API key created:\n{secret}\n\nCopy it now. For security, ALLINONE will not show the secret again.";
        RenderApiKeys();
    }

    private void RenderApiKeys()
    {
        if (ApiKeyList == null) return;
        ApiKeyList.Children.Clear();
        foreach (var key in apiKeys.Keys.OrderByDescending(k => k.CreatedAt))
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var text = new TextBlock { Text = $"{key.Name}  •  {(key.Active ? "Active" : "Revoked")}  •  {key.CreatedAt.LocalDateTime:g}", VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource(key.Active ? "TextBrush" : "MutedBrush") };
            DockPanel.SetDock(text, Dock.Left);
            row.Children.Add(text);
            if (key.Active)
            {
                var revoke = new Button { Content = "Revoke", Padding = new Thickness(9, 5), HorizontalAlignment = HorizontalAlignment.Right };
                revoke.Click += (_, _) => { apiKeys.Revoke(key.Id); RenderApiKeys(); };
                row.Children.Add(revoke);
            }
            ApiKeyList.Children.Add(row);
        }
    }

    private void SignOut_Click(object sender, RoutedEventArgs e)
    {
        clerk.SignOut(); settings.SignedIn = false; settings.AccountName = null; SaveSettingsToDisk(); AuthStatus.Text = "Signed out";
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        settings.DisplayName = string.IsNullOrWhiteSpace(SettingsName.Text) ? "User" : SettingsName.Text.Trim();
        settings.SafeMode = SettingsSafe.IsChecked == true; SaveSettingsToDisk(); StatusText.Text = "● Settings saved";
    }

    private sealed record ChatEntry(string Author, string Text);
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