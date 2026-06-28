using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace MouseClickVoice;

public partial class ModelManagementWindow : Window
{
    private readonly ObservableCollection<ModelRowViewModel> _rows = new();
    private CancellationTokenSource? _downloadCts;
    private ModelRowViewModel? _selectedRow;

    public event EventHandler? ModelsUpdated;

    public ModelManagementWindow()
    {
        InitializeComponent();
        ModelsGrid.ItemsSource = _rows;
        ModelsGrid.SelectionChanged += (_, _) =>
        {
            _selectedRow = ModelsGrid.SelectedItem as ModelRowViewModel;
            UpdateButtonStates();
        };
        RefreshList();
    }

    private void RefreshList()
    {
        SpeechModelManager.EnsureDefaultVisibility();
        _rows.Clear();
        foreach (var model in SpeechModelManager.AllModels)
            _rows.Add(new ModelRowViewModel(model));

        if (_rows.Count > 0 && ModelsGrid.SelectedIndex < 0)
            ModelsGrid.SelectedIndex = 0;

        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        var row = _selectedRow;
        var busy = _downloadCts != null;

        DownloadButton.IsEnabled = !busy && row != null && !row.IsInstalled;
        DeleteButton.IsEnabled = !busy && row != null && row.IsInstalled;
        OpenFolderButton.IsEnabled = !busy;
    }

    private async void DownloadButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedRow == null)
            return;

        _downloadCts = new CancellationTokenSource();
        DownloadButton.IsEnabled = false;
        DeleteButton.IsEnabled = false;
        StatusText.Text = "准备下载...";

        try
        {
            var id = _selectedRow.Id;
            var ok = await SpeechModelManager.DownloadAsync(
                id,
                msg => Dispatcher.UIThread.Post(() => StatusText.Text = msg),
                _downloadCts.Token);

            if (ok)
            {
                StatusText.Text = "下载完成";
                RefreshRow(id);
                ModelsUpdated?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                StatusText.Text = "下载失败";
            }
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "已取消下载";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"下载失败: {ex.Message}";
            await DialogHelper.ShowWarningAsync(this, ex.Message, "下载失败");
        }
        finally
        {
            _downloadCts?.Dispose();
            _downloadCts = null;
            UpdateButtonStates();
        }
    }

    private async void DeleteButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedRow == null)
            return;

        var name = _selectedRow.DisplayName;
        if (!await DialogHelper.ShowYesNoAsync(this,
                $"确定删除「{name}」的本地模型文件吗？\n删除后需重新下载才能使用该引擎。",
                "确认删除"))
            return;

        if (!SpeechModelManager.Delete(_selectedRow.Id, out var error))
        {
            await DialogHelper.ShowWarningAsync(this, error ?? "删除失败");
            return;
        }

        StatusText.Text = "已删除";
        RefreshRow(_selectedRow.Id);
        ModelsUpdated?.Invoke(this, EventArgs.Empty);
        UpdateButtonStates();
    }

    private void OpenFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        var dir = SpeechModelManager.ModelsDirectory;
        Directory.CreateDirectory(dir);

        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
        else if (OperatingSystem.IsLinux())
        {
            Process.Start("xdg-open", dir);
        }
        else if (OperatingSystem.IsMacOS())
        {
            Process.Start("open", dir);
        }
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _downloadCts?.Cancel();
        base.OnClosing(e);
    }

    private void RefreshRow(string id)
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            if (!_rows[i].Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                continue;

            var model = SpeechModelManager.GetModel(id);
            if (model != null)
                _rows[i] = new ModelRowViewModel(model);
            ModelsGrid.SelectedIndex = i;
            break;
        }
    }

    private sealed class ModelRowViewModel : INotifyPropertyChanged
    {
        private bool _isVisibleInDropdown;

        public ModelRowViewModel(SpeechModelInfo model)
        {
            Id = model.Id;
            DisplayName = model.DisplayName;
            Description = model.Description;
            StatusText = model.StatusText;
            SizeOnDiskText = model.SizeOnDiskText;
            IsInstalled = model.IsInstalled;
            _isVisibleInDropdown = model.IsVisibleInDropdown;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string StatusText { get; private set; }
        public string SizeOnDiskText { get; private set; }
        public bool IsInstalled { get; private set; }

        public bool IsVisibleInDropdown
        {
            get => _isVisibleInDropdown;
            set
            {
                if (_isVisibleInDropdown == value)
                    return;

                _isVisibleInDropdown = value;
                SpeechModelManager.SetVisibleInDropdown(Id, value);
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
