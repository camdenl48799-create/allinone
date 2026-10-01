using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ALLINONE;

public partial class MainWindow : Window
{
    private readonly string dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ALLINONE");
    private readonly string settingsPath;
    private Settings settings = new();
    private int setupStep = 1;

    public MainWindow()
    {
        InitializeComponent();
        settingsPath = Path.Combine(dataDir, "settings.json");
        Directory.CreateDirectory(dataDir);
        LoadSettings();
        if (settings.SetupComplete) ShowApp(); else ShowSetup();
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

    private void ShowSetup() { SetupView.Visibility=Visibility.Visible; AppView.Visibility=Visibility.Collapsed; SetupBack.Visibility=Visibility.Collapsed; RenderSetup(); }

    private void RenderSetup()
    {
        SetupStep.Text=$"{setupStep} / 4";
        SetupBack.Visibility=setupStep>1 ? Visibility.Visible : Visibility.Collapsed;
        NamePanel.Visibility=setupStep==2 ? Visibility.Visible : Visibility.Collapsed;
        SafeModeBox.Visibility=setupStep==3 ? Visibility.Visible : Visibility.Collapsed;
        if(setupStep==1){SetupTitle.Text="Welcome";SetupDescription.Text="A native Windows AI workspace with local-first storage and a clean setup.";}
        if(setupStep==2){SetupTitle.Text="Make it yours";SetupDescription.Text="Choose the name ALLINONE should use for you.";NameBox.Text=settings.DisplayName;}
        if(setupStep==3){SetupTitle.Text="Safety & privacy";SetupDescription.Text="Safe Mode adds extra guardrails to AI features.";SafeModeBox.IsChecked=settings.SafeMode;}
        if(setupStep==4){SetupTitle.Text="Ready";SetupDescription.Text="Your workspace is configured. A high-capability model backend can plug into this native shell without changing the UI or storage layer.";SetupNext.Content="Launch ALLINONE";}
    }

    private void SetupNext_Click(object sender,RoutedEventArgs e)
    {
        if(setupStep==2){settings.DisplayName=string.IsNullOrWhiteSpace(NameBox.Text)?"User":NameBox.Text.Trim();}
        if(setupStep==3){settings.SafeMode=SafeModeBox.IsChecked==true;}
        if(setupStep<4){setupStep++;RenderSetup();}else{settings.SetupComplete=true;SaveSettingsToDisk();ShowApp();}
    }
    private void SetupBack_Click(object sender,RoutedEventArgs e){if(setupStep>1){setupStep--;RenderSetup();}}
    private void ShowApp(){SetupView.Visibility=Visibility.Collapsed;AppView.Visibility=Visibility.Visible;SettingsName.Text=settings.DisplayName;SettingsSafe.IsChecked=settings.SafeMode;AddMessage("ALLINONE",$"Ready, {settings.DisplayName}. The native workspace is running.");}
    private void Nav_Click(object sender,RoutedEventArgs e){var tag=(sender as Button)?.Tag?.ToString();ChatPage.Visibility=tag=="Chat"?Visibility.Visible:Visibility.Collapsed;ProjectsPage.Visibility=tag=="Projects"?Visibility.Visible:Visibility.Collapsed;SettingsPage.Visibility=tag=="Settings"?Visibility.Visible:Visibility.Collapsed;}
    private void Send_Click(object sender,RoutedEventArgs e)=>SendPrompt();
    private void PromptBox_KeyDown(object sender,KeyEventArgs e){if(e.Key==Key.Enter&&Keyboard.Modifiers==ModifierKeys.Control){SendPrompt();e.Handled=true;}}
    private void SendPrompt(){var prompt=PromptBox.Text.Trim();if(prompt.Length==0)return;AddMessage(settings.DisplayName,prompt);PromptBox.Clear();AddMessage("ALLINONE",LocalFallback(prompt));}
    private string LocalFallback(string prompt)
    {
        if(prompt.Contains("code",StringComparison.OrdinalIgnoreCase)) return "ALLINONE is ready for code architecture, debugging, planning, and file-by-file implementation. The model runtime is the next layer.";
        if(prompt.Contains("hello",StringComparison.OrdinalIgnoreCase)||prompt.Contains("hi",StringComparison.OrdinalIgnoreCase)) return $"Hey {settings.DisplayName}! ALLINONE is ready.";
        return "Request received. The native shell is ready for a high-capability model runtime, persistent memory, retrieval, and permission-controlled tools.";
    }
    private void AddMessage(string author,string text){var border=new Border{Background=new SolidColorBrush(Color.FromRgb(24,28,38)),CornerRadius=new CornerRadius(12),Padding=new Thickness(16),Margin=new Thickness(0,0,0,12)};var panel=new StackPanel();panel.Children.Add(new TextBlock{Text=author,FontWeight=FontWeights.SemiBold,Foreground=(Brush)FindResource("AccentBrush")});panel.Children.Add(new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,7,0,0),FontSize=15});border.Child=panel;Messages.Children.Add(border);}
    private void SaveSettings_Click(object sender,RoutedEventArgs e){settings.DisplayName=string.IsNullOrWhiteSpace(SettingsName.Text)?"User":SettingsName.Text.Trim();settings.SafeMode=SettingsSafe.IsChecked==true;SaveSettingsToDisk();StatusText.Text="Settings saved • AI engine: model-ready";}
}
public sealed class Settings{public string DisplayName{get;set;}="User";public bool SafeMode{get;set;}=true;public bool SetupComplete{get;set;}}
