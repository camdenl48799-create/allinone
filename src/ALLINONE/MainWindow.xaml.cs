using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Documents;
using Microsoft.Win32;

namespace ALLINONE;

public partial class MainWindow : Window
{
    private readonly string dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ALLINONE");
    private readonly string settingsPath;
    private readonly ClerkAuthService clerk;
    private readonly UpdateService updater = new();
    private readonly SearchInOne searchInOne = new();
    private readonly ProjectStore projects;
    private readonly FileContextService fileContext = new();
    private readonly HttpClient web = new() { Timeout = TimeSpan.FromSeconds(15) };
    private Settings settings = new();
    private int setupStep = 1;
    private bool gameMode;
    private ProjectInfo? currentProject;

    public MainWindow()
    {
        InitializeComponent();
        settingsPath = Path.Combine(dataDir, "settings.json");
        Directory.CreateDirectory(dataDir);
        clerk = new ClerkAuthService(dataDir);
        projects = new ProjectStore(dataDir);
        LoadSettings();
        ApplyTheme(settings.Theme);
        ModelSelector.ItemsSource = ModelCatalog.All;
        ModelSelector.SelectedItem = ModelCatalog.Get(settings.SelectedModel);
        RenderModelStatus();
        LoadProjects();

        if (settings.SetupComplete) ShowApp(); else ShowSetup();
        _ = CheckForUpdatesAsync();
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

        if (setupStep == 1) { SetupTitle.Text = "Welcome"; SetupDescription.Text = "A native Windows AI workspace with modular models, real search, local files, projects, and persistent settings."; }
        if (setupStep == 2) { SetupTitle.Text = "Make it yours"; SetupDescription.Text = "Choose the name ALLINONE should use for you."; NameBox.Text = settings.DisplayName; }
        if (setupStep == 3) { SetupTitle.Text = "Safety & privacy"; SetupDescription.Text = "Safe Mode applies guardrails before requests are routed to tools or model providers."; SafeModeBox.IsChecked = settings.SafeMode; }
        if (setupStep == 4) { SetupTitle.Text = "Ready"; SetupDescription.Text = "Only connected capabilities are exposed as available. Unimplemented roadmap models remain clearly unavailable."; SetupNext.Content = "Launch ALLINONE"; }
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
        SettingsName.Text = settings.DisplayName;
        SettingsSafe.IsChecked = settings.SafeMode;
        AuthStatus.Text = settings.SignedIn
            ? $"Signed in as {settings.AccountName ?? settings.DisplayName}"
            : (clerk.IsConfigured ? "Not signed in" : "Clerk setup required");
        if (Messages.Children.Count == 0)
            AddMessage("ALLINONE", $"Ready, {settings.DisplayName}. Select a model or use @SearchInOne, @CodeInOne, @MathInOne, @File, @Website, @YouTube, @Project, or @Model.");
        StatusText.Text = $"● {ModelCatalog.Get(settings.SelectedModel).DisplayName}";
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as Button)?.Tag?.ToString();
        ChatPage.Visibility = tag == "Chat" ? Visibility.Visible : Visibility.Collapsed;
        ProjectsPage.Visibility = tag == "Projects" ? Visibility.Visible : Visibility.Collapsed;
        FilesPage.Visibility = tag == "Files" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = tag == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        AccountPage.Visibility = tag == "Account" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PromptBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = PromptBox.Text;
        var at = text.LastIndexOf('@');
        if (at < 0 || (at > 0 && !char.IsWhiteSpace(text[at - 1]))) { MentionList.Visibility = Visibility.Collapsed; return; }
        var fragment = text[(at + 1)..].ToLowerInvariant();
        var options = MentionRouter.Options.Where(x => x.Key.ToLowerInvariant().StartsWith(fragment)).ToList();
        MentionList.ItemsSource = options;
        MentionList.Visibility = options.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void MentionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MentionList.SelectedItem is not MentionOption option) return;
        var text = PromptBox.Text;
        var at = text.LastIndexOf('@');
        if (at >= 0) PromptBox.Text = text[..at] + option.Label + " ";
        PromptBox.CaretIndex = PromptBox.Text.Length;
        MentionList.SelectedItem = null;
        MentionList.Visibility = Visibility.Collapsed;
        PromptBox.Focus();
    }

    private void Send_Click(object sender, RoutedEventArgs e) => _ = SendPromptAsync();

    private void PromptBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _ = SendPromptAsync();
            e.Handled = true;
        }
    }

    private async Task SendPromptAsync()
    {
        var raw = PromptBox.Text.Trim();
        if (raw.Length == 0) return;

        AddMessage(settings.DisplayName, raw);
        PromptBox.Clear();
        SetBusy(true);

        try
        {
            var (target, text) = MentionRouter.Parse(raw);
            switch (target)
            {
                case "SearchInOne": await RunSearchAsync(text.Length > 0 ? text : raw); break;
                case "CodeInOne": await RunCodeAsync(text); break;
                case "File": await RunFileAsync(text); break;
                case "Website": await RunWebsiteAsync(text); break;
                case "YouTube": await RunYouTubeAsync(text); break;
                case "Project": RunProject(text); break;
                case "Model": RunModel(text); break;
                default: await RunLocalCoreAsync(raw); break;
            }
        }
        catch (Exception ex)
        {
            AddMessage("ALLINONE", $"Request failed safely: {ex.Message}");
        }
        finally { SetBusy(false); }
    }

    private async Task RunLocalCoreAsync(string prompt)
    {
        if (settings.SafeMode && ContainsUnsafeRequest(prompt))
        {
            AddMessage("ALLINONE Safety", "Safe Mode blocked this request. Try a safe, age-appropriate version of the task.");
            return;
        }

        var expression = ExtractMath(prompt);
        if (expression is not null)
        {
            try
            {
                var value = EvaluateMath(expression);
                AddMessage("MathInOne", $"{expression} = {value}\n\nThis result was calculated locally by ALLINONE; no model was used.");
                return;
            }
            catch { }
        }

        if (prompt.Contains("code", StringComparison.OrdinalIgnoreCase))
        {
            AddMessage("CodeInOne", "CodeInOne routing is active, but no CodeInOne model runtime is installed in this build. The app will not fabricate a generated answer. Use @CodeInOne after connecting an approved model provider.");
            return;
        }

        AddMessage("ALLINONE", "Local Core is active. This build does not have a connected generative model, so it will not pretend to generate AI answers. Real tools such as SearchInOne, local math, projects, and file analysis are available.");
        await Task.CompletedTask;
    }

    private async Task RunSearchAsync(string query)
    {
        AddMessage("SearchInOne", $"Searching the internet for: {query}");
        var results = await searchInOne.SearchAsync(query);
        if (results.Count == 0) { AddMessage("SearchInOne", "No search results were returned. Nothing was fabricated."); return; }

        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = $"Search results for “{query}”", FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("AccentBrush") });
        foreach (var result in results)
        {
            var link = new Hyperlink(new Run(result.Title)) { NavigateUri = new Uri(result.Url) };
            link.RequestNavigate += (_, _) => OpenUrl(result.Url);
            var block = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
            block.Inlines.Add(link);
            block.Inlines.Add(new Run($"\n{result.Url}") { Foreground = (Brush)FindResource("MutedBrush") });
            panel.Children.Add(block);
        }
        AddPanelMessage("SearchInOne", panel);
    }

    private async Task RunWebsiteAsync(string input)
    {
        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            AddMessage("@Website", "Provide a full http:// or https:// website URL. The site must be publicly retrievable.");
            return;
        }

        using var response = await web.GetAsync(uri);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<script[\\s\\S]*?</script>|<style[\\s\\S]*?</style>|<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(text, "\\s+", " ")).Trim();
        if (text.Length > 5000) text = text[..5000] + "…";
        AddMessage("@Website", $"Retrieved content from {uri.Host}:\n\n{text}\n\nSource: {uri}");
    }

    private async Task RunYouTubeAsync(string input)
    {
        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri) || !uri.Host.Contains("youtube", StringComparison.OrdinalIgnoreCase))
        {
            AddMessage("@YouTube", "Provide a YouTube URL. This build retrieves public metadata only; it does not invent transcripts.");
            return;
        }
        var oembed = $"https://www.youtube.com/oembed?url={Uri.EscapeDataString(uri.ToString())}&format=json";
        using var response = await web.GetAsync(oembed);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var title = root.TryGetProperty("title", out var t) ? t.GetString() : "Unknown";
        var author = root.TryGetProperty("author_name", out var a) ? a.GetString() : "Unknown";
        AddMessage("@YouTube", $"Public metadata retrieved:\n\nTitle: {title}\nChannel: {author}\nURL: {uri}\n\nNo transcript was claimed or fabricated.");
    }

    private async Task RunFileAsync(string input)
    {
        var path = input.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(path))
        {
            AddMessage("@File", "Use the Files page to add a file, or provide its full local path after @File.");
            return;
        }
        try
        {
            var content = await fileContext.ReadAsync(path);
            if (content.Length > 6000) content = content[..6000] + "\n… [local preview truncated]";
            AddMessage("@File", $"Actual local file content from {Path.GetFileName(path)}:\n\n{content}");
        }
        catch (Exception ex) { AddMessage("@File", $"The file was not analyzed: {ex.Message}"); }
    }

    private void RunProject(string input)
    {
        if (currentProject is null)
        {
            AddMessage("@Project", "No project is selected. Open Projects and choose one first.");
            return;
        }
        AddMessage("@Project", $"Current project: {currentProject.Name}\nFiles: {currentProject.Files.Count}\nUpdated: {currentProject.UpdatedUtc.ToLocalTime():g}\n\nProject context is kept separate from unrelated workspaces.");
    }

    private void RunModel(string input)
    {
        var selected = ModelCatalog.Get(settings.SelectedModel);
        AddMessage("@Model", $"Selected model: {selected.DisplayName}\nStatus: {(selected.Available ? "Available" : "Unavailable")}\n{selected.Description}");
    }

    private async Task RunCodeAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            AddMessage("@CodeInOne", "CodeInOne is selected, but no coding prompt was provided.");
            return;
        }
        AddMessage("@CodeInOne", "CodeInOne is not connected to a generative model runtime in this build. I won't fabricate code generation. The project architecture is ready for a real provider to be connected.");
        await Task.CompletedTask;
    }

    private void ModelSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModelSelector.SelectedItem is not ModelDescriptor model) return;
        settings.SelectedModel = model.Id;
        SaveSettingsToDisk();
        StatusText.Text = $"● {model.DisplayName}" + (model.Available ? "" : " — unavailable");
    }

    private void RenderModelStatus()
    {
        ModelStatusList.Items.Clear();
        foreach (var model in ModelCatalog.All)
        {
            var text = new TextBlock
            {
                Text = $"{(model.Available ? "● Available" : "○ Unavailable")}  {model.DisplayName} — {model.Reason ?? model.Description}",
                Foreground = (Brush)FindResource(model.Available ? "AccentBrush" : "MutedBrush"),
                Margin = new Thickness(0, 5, 0, 0)
            };
            ModelStatusList.Items.Add(text);
        }
    }

    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new InputDialog("New ALLINONE project", "Project name:", "My Project");
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Value)) return;
        currentProject = projects.Create(dialog.Value.Trim());
        ProjectStatus.Text = $"Project: {currentProject.Name}";
        LoadProjects();
        FileStatus.Text = "Project created locally.";
    }

    private void LoadProjects()
    {
        ProjectGrid.Children.Clear();
        foreach (var project in projects.List())
        {
            var button = new Button { Content = $"{project.Name}\n{project.Files.Count} file(s)", Width = 260, Height = 90, Margin = new Thickness(0, 0, 12, 12), Tag = project };
            button.Click += (_, _) =>
            {
                currentProject = (ProjectInfo)button.Tag;
                ProjectStatus.Text = $"Project: {currentProject.Name}";
                LoadFiles();
            };
            ProjectGrid.Children.Add(button);
        }
    }

    private void AddFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Multiselect = false, Filter = "Supported text/code|*.txt;*.md;*.json;*.csv;*.xml;*.html;*.htm;*.css;*.js;*.ts;*.tsx;*.jsx;*.cs;*.cpp;*.h;*.hpp;*.py;*.java;*.go;*.rs;*.swift;*.kt;*.xaml;*.yml;*.yaml;*.sql;*.log|All files|*.*" };
        if (dialog.ShowDialog() != true) return;

        if (currentProject is null)
        {
            AddMessage("Files", "Create/select a project before attaching a file so its context stays isolated.");
            return;
        }

        projects.AddFile(currentProject, dialog.FileName);
        currentProject = projects.List().FirstOrDefault(p => p.Id == currentProject.Id);
        FileStatus.Text = $"Added {Path.GetFileName(dialog.FileName)}";
        LoadFiles();
        LoadProjects();
    }

    private void LoadFiles()
    {
        FileList.Items.Clear();
        if (currentProject is null) return;
        foreach (var file in currentProject.Files)
            FileList.Items.Add(file);
    }

    private async Task CheckForUpdatesAsync()
    {
        var update = await updater.CheckAsync();
        if (update is null) return;
        await Dispatcher.InvokeAsync(() => StatusText.Text = $"● Updating to {update.Version}…");
        var installed = await updater.InstallAsync(update);
        if (installed)
        {
            MessageBox.Show($"ALLINONE {update.Version} was downloaded and verified. Restart ALLINONE to complete the update.", "ALLINONE update", MessageBoxButton.OK, MessageBoxImage.Information);
            Application.Current.Shutdown();
        }
        else await Dispatcher.InvokeAsync(() => StatusText.Text = $"● Update {update.Version} available");
    }

    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as Button)?.Tag?.ToString() == "Light" ? "Light" : "Dark";
        ApplyTheme(tag);
        settings.Theme = tag;
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

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        settings.DisplayName = string.IsNullOrWhiteSpace(SettingsName.Text) ? "User" : SettingsName.Text.Trim();
        settings.SafeMode = SettingsSafe.IsChecked == true;
        SaveSettingsToDisk();
        StatusText.Text = "● Settings saved";
    }

    private async void GoogleSignUp_Click(object sender, RoutedEventArgs e) => await BeginClerkAuthAsync();
    private async void AppleSignUp_Click(object sender, RoutedEventArgs e) => await BeginClerkAuthAsync();
    private async void SignIn_Click(object sender, RoutedEventArgs e) => await BeginClerkAuthAsync();

    private async Task BeginClerkAuthAsync()
    {
        if (!clerk.IsConfigured) { AuthStatus.Text = "Clerk isn't configured yet. Add the required Clerk settings."; return; }
        try
        {
            AuthStatus.Text = "Opening Clerk in your browser…";
            var user = await clerk.SignInAsync();
            settings.SignedIn = true;
            settings.AccountName = user?.name ?? user?.email ?? settings.DisplayName;
            SaveSettingsToDisk();
            AuthStatus.Text = $"Signed in as {settings.AccountName}";
        }
        catch (OperationCanceledException) { AuthStatus.Text = "Authentication was canceled."; }
        catch (Exception ex) { AuthStatus.Text = $"Authentication failed: {ex.Message}"; }
    }

    private void SignOut_Click(object sender, RoutedEventArgs e)
    {
        clerk.SignOut();
        settings.SignedIn = false;
        settings.AccountName = null;
        SaveSettingsToDisk();
        AuthStatus.Text = "Signed out";
    }

    private void AddMessage(string author, string text)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = author, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("AccentBrush") });
        panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 0), FontSize = 15 });
        AddPanelMessage(author, panel);
    }

    private void AddPanelMessage(string author, UIElement content)
    {
        var border = new Border { Background = (Brush)FindResource("Panel2Brush"), BorderBrush = (Brush)FindResource("BorderBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 12) };
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = author, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("AccentBrush") });
        panel.Children.Add(content);
        border.Child = panel;
        Messages.Children.Add(border);
        MessageScroller.ScrollToEnd();
    }

    private void SetBusy(bool busy)
    {
        StatusText.Text = busy ? "● Working…" : $"● {ModelCatalog.Get(settings.SelectedModel).DisplayName}";
        PromptBox.IsEnabled = !busy;
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    private static bool ContainsUnsafeRequest(string prompt) =>
        prompt.Contains("how to hurt", StringComparison.OrdinalIgnoreCase)
        || prompt.Contains("self harm", StringComparison.OrdinalIgnoreCase)
        || prompt.Contains("suicide", StringComparison.OrdinalIgnoreCase);

    private static string? ExtractMath(string prompt)
    {
        var value = System.Text.RegularExpressions.Regex.Replace(prompt.ToLowerInvariant(), @"^(what is|calculate|compute|evaluate|solve)\\s+", "").Trim().TrimEnd('?', '=');
        return System.Text.RegularExpressions.Regex.IsMatch(value, @"^[0-9+\\-*/%^().\\s]+$") && value.Any(char.IsDigit) && value.Any(c => "+-*/%^".Contains(c)) ? value : null;
    }

    private static double EvaluateMath(string expression)
    {
        var tokens = new List<string>();
        var i = 0;
        while (i < expression.Length)
        {
            if (char.IsWhiteSpace(expression[i])) { i++; continue; }
            if (char.IsDigit(expression[i]) || expression[i] == '.')
            {
                var start = i++;
                while (i < expression.Length && (char.IsDigit(expression[i]) || expression[i] == '.')) i++;
                tokens.Add(expression[start..i]);
            }
            else tokens.Add(expression[i++].ToString());
        }

        var values = new Stack<double>();
        var ops = new Stack<string>();
        int Prec(string op) => op is "+" or "-" ? 1 : op is "*" or "/" or "%" ? 2 : op == "^" ? 3 : 0;
        void Apply()
        {
            var op = ops.Pop();
            var b = values.Pop();
            var a = values.Pop();
            values.Push(op switch { "+" => a + b, "-" => a - b, "*" => a * b, "/" when b != 0 => a / b, "%" when b != 0 => a % b, "^" => Math.Pow(a, b), _ => throw new InvalidOperationException("Invalid expression") });
        }
        foreach (var token in tokens)
        {
            if (double.TryParse(token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n)) values.Push(n);
            else if (token == "(") ops.Push(token);
            else if (token == ")") { while (ops.Count > 0 && ops.Peek() != "(") Apply(); if (ops.Count == 0) throw new InvalidOperationException(); ops.Pop(); }
            else
            {
                while (ops.Count > 0 && ops.Peek() != "(" && (Prec(ops.Peek()) > Prec(token) || (Prec(ops.Peek()) == Prec(token) && token != "^"))) Apply();
                ops.Push(token);
            }
        }
        while (ops.Count > 0) { if (ops.Peek() == "(") throw new InvalidOperationException(); Apply(); }
        return values.Count == 1 ? values.Pop() : throw new InvalidOperationException();
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
    public string SelectedModel { get; set; } = "local-core";
}

public sealed class InputDialog : Window
{
    public string Value => box.Text;
    private readonly TextBox box = new();
    public InputDialog(string title, string prompt, string initial)
    {
        Title = title; Width = 420; Height = 190; WindowStartupLocation = WindowStartupLocation.CenterOwner; Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock { Text = prompt, Margin = new Thickness(0,0,0,8) });
        box.Text = initial; panel.Children.Add(box);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,18,0,0) };
        var cancel = new Button { Content = "Cancel", Width = 90, Margin = new Thickness(0,0,8,0) }; cancel.Click += (_,_) => { DialogResult=false; Close(); };
        var ok = new Button { Content = "Create", Width = 90, Background = new SolidColorBrush(Color.FromRgb(224,30,43)) }; ok.Click += (_,_) => { DialogResult=true; Close(); };
        buttons.Children.Add(cancel); buttons.Children.Add(ok); panel.Children.Add(buttons); Content = panel;
    }
}
