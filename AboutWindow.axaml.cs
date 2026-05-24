using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MouseClickVoice;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version != null ? $"版本 {version.Major}.{version.Minor}.{version.Build}" : "版本 1.0.0";
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e) => Close();
}
