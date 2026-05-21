using System;
using System.Collections.ObjectModel;
using System.IO;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace MouseClickVoice
{
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
            {
                _rows.Add(new ModelRowViewModel(model));
            }

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

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
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
                    msg => Dispatcher.Invoke(() => StatusText.Text = msg),
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
                MessageBox.Show(ex.Message, "下载失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                _downloadCts?.Dispose();
                _downloadCts = null;
                UpdateButtonStates();
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRow == null)
                return;

            var name = _selectedRow.DisplayName;
            if (MessageBox.Show(
                    $"确定删除「{name}」的本地模型文件吗？\n删除后需重新下载才能使用该引擎。",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            if (!SpeechModelManager.Delete(_selectedRow.Id, out var error))
            {
                MessageBox.Show(error ?? "删除失败", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StatusText.Text = "已删除";
            RefreshRow(_selectedRow.Id);
            ModelsUpdated?.Invoke(this, EventArgs.Empty);
            UpdateButtonStates();
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dir = SpeechModelManager.ModelsDirectory;
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        protected override void OnClosing(CancelEventArgs e)
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
}
