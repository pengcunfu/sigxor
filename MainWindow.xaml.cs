using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace MouseClickVoice
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private enum RecordingTrigger { None, KeyboardHold, KeyboardToggle }

        private KeyboardHook? _keyboardHook;
        private AudioCapture? _audioCapture;
        private SpeechRecognizer? _speechRecognizer;
        private TextSimulator? _textSimulator;
        private VoiceInputOverlay? _voiceOverlay;
        private TrayIconManager? _trayIcon;
        private bool _serviceRunning;
        private bool _isExiting;
        private bool _isLoadingSettings;
        private bool _isRecording;
        private bool _isShortcutDown;
        private bool _altHoldTriggeredThisPress;
        private bool _keyboardToggleActive;
        private RecordingTrigger _activeTrigger = RecordingTrigger.None;
        private readonly DispatcherTimer _statusTimer;
        private readonly Config _config;

        public MainWindow()
        {
            _config = Config.Instance;
            SpeechModelManager.EnsureDefaultVisibility();
            InitializeComponent(); // 这会自动调用InitializeComponents()
            SpeechModelManager.ModelsChanged += OnModelsChanged;
            LoadUserSettings(); // 重命名避免冲突
            InitializeServices();

            _statusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(0.1)
            };
            _statusTimer.Tick += UpdateStatus;
            _statusTimer.Start();

            Loaded += OnWindowLoaded;
        }

        public void PrepareSilentStartup()
        {
            Hide();
            ShowInTaskbar = false;
        }

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnWindowLoaded;
            await StartService();
        }

        private void ShowMainWindow()
        {
            Show();
            ShowInTaskbar = true;
            WindowState = WindowState.Normal;
            Activate();
        }

        private void ShowAboutDialog()
        {
            var about = new AboutWindow { Owner = IsVisible ? this : null };
            about.ShowDialog();
        }

        private void ExitApplication()
        {
            _isExiting = true;
            StopService();
            _trayIcon?.Dispose();
            _trayIcon = null;
            System.Windows.Application.Current.Shutdown();
        }

        private void UpdateServiceMenuState()
        {
            StartServiceMenuItem.IsEnabled = !_serviceRunning;
            StopServiceMenuItem.IsEnabled = _serviceRunning;
            _trayIcon?.SetServiceRunning(_serviceRunning);
        }

        private async void StartServiceMenuItem_Click(object sender, RoutedEventArgs e) => await StartService();

        private void StopServiceMenuItem_Click(object sender, RoutedEventArgs e) => StopService();

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e) => ShowAboutDialog();

        private void ModelManagementMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var window = new ModelManagementWindow { Owner = IsVisible ? this : null };
            window.ModelsUpdated += (_, _) => RefreshEngineComboBox();
            window.ShowDialog();
            RefreshEngineComboBox();
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => ExitApplication();

        private void OnModelsChanged() =>
            Dispatcher.BeginInvoke(RefreshEngineComboBox);

        private void LoadUserSettings()
        {
            // 从配置加载设置，使用延迟加载避免初始化顺序问题
            if (_config != null)
            {
                // 延迟设置以确保UI组件已完全初始化
                Dispatcher.BeginInvoke(() =>
                {
                    _isLoadingSettings = true;
                    try
                    {
                        RefreshEngineComboBox();
                        SelectComboBoxByTag(LanguageComboBox, _config.RecognitionLanguage);

                        if (ShowNotificationsCheckBox != null)
                        {
                            ShowNotificationsCheckBox.IsChecked = _config.ShowNotifications;
                            UseClipboardCheckBox.IsChecked = _config.UseClipboard;
                            SilentStartCheckBox.IsChecked = _config.SilentStart;
                            MinimizeToTrayCheckBox.IsChecked = _config.MinimizeToTray;
                            var autoStart = _config.AutoStartWithWindows;
                            if (autoStart != StartupHelper.IsEnabled())
                            {
                                try { StartupHelper.SetEnabled(autoStart, _config.SilentStart); }
                                catch { AutoStartCheckBox.IsChecked = StartupHelper.IsEnabled(); }
                            }
                            AutoStartCheckBox.IsChecked = StartupHelper.IsEnabled();
                        }

                        UpdateServiceMenuState();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"加载设置失败: {ex.Message}");
                    }
                    finally
                    {
                        _isLoadingSettings = false;
                    }
                });
            }
        }

        private void InitializeServices()
        {
            try
            {
                _keyboardHook = new KeyboardHook();
                _keyboardHook.HoldThresholdMs = (int)(_config.AltHoldThreshold * 1000);
                _keyboardHook.ShortcutPressed += OnShortcutPressed;
                _keyboardHook.ShortcutReleased += OnShortcutReleased;
                _keyboardHook.ShortcutHoldDetected += OnShortcutHoldDetected;

                _audioCapture = new AudioCapture();
                _audioCapture.StatusChanged += OnAudioStatusChanged;

                _speechRecognizer = new SpeechRecognizer();
                _speechRecognizer.StatusChanged += OnRecognitionStatusChanged;
                _speechRecognizer.Error += OnSpeechError;

                _textSimulator = new TextSimulator(_config.TypingDelay);
                _voiceOverlay = new VoiceInputOverlay();

                _trayIcon = new TrayIconManager();
                _trayIcon.ShowWindowRequested += (_, _) => ShowMainWindow();
                _trayIcon.StartServiceRequested += async (_, _) => await StartService();
                _trayIcon.StopServiceRequested += (_, _) => StopService();
                _trayIcon.AboutRequested += (_, _) => ShowAboutDialog();
                _trayIcon.ExitRequested += (_, _) => ExitApplication();

                RecognitionStatusText.Text = "已初始化";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化服务失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task StartService()
        {
            try
            {
                var engineTag = _config.RecognitionEngine;
                var model = SpeechModelManager.GetModel(engineTag);
                var engineName = model?.DisplayName ?? engineTag;

                if (!SpeechModelManager.IsInstalled(engineTag))
                {
                    MessageBox.Show(
                        $"当前选择的「{engineName}」尚未下载。\n\n请在「工具 → 模型管理」中下载模型后再启动服务。",
                        "模型未就绪",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    RecognitionStatusText.Text = "模型未下载";
                    return;
                }

                RecognitionStatusText.Text = $"正在初始化 {engineName}...";
                if (_speechRecognizer != null && !_speechRecognizer.IsInitialized)
                {
                    var initSuccess = await _speechRecognizer.InitializeAsync();
                    if (!initSuccess)
                    {
                        MessageBox.Show(
                            $"{engineName} 初始化失败。\n\n" +
                            "请尝试：\n" +
                            "1. 在「工具 → 模型管理」中删除并重新下载模型\n" +
                            "2. 切换到其他已下载的识别引擎",
                            "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                _keyboardHook?.Start();
                await Task.Delay(100);

                _serviceRunning = true;
                UpdateServiceMenuState();

                ShowNotification("服务已启动", "使用右 Alt 键进行语音输入");
                RecognitionStatusText.Text = $"{_speechRecognizer?.EngineName ?? engineName} 就绪";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动服务失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopService()
        {
            try
            {
                _keyboardHook?.Stop();
                _audioCapture?.StopRecording();

                _isRecording = false;
                _isShortcutDown = false;
                _altHoldTriggeredThisPress = false;
                _keyboardToggleActive = false;
                _activeTrigger = RecordingTrigger.None;
                _voiceOverlay?.HideOverlay();

                _serviceRunning = false;
                UpdateServiceMenuState();

                ShowNotification("服务已停止", "语音输入功能已关闭");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"停止服务失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RunOnUiThread(Action action)
        {
            if (Dispatcher.CheckAccess())
                action();
            else
                Dispatcher.BeginInvoke(action);
        }

        private void OnShortcutPressed(object? sender, EventArgs e)
        {
            RunOnUiThread(() =>
            {
                _isShortcutDown = true;
                _altHoldTriggeredThisPress = false;
                ShortcutStatusText.Text = _keyboardToggleActive ? "录音中" : "按下";
            });
        }

        private void OnShortcutHoldDetected(object? sender, EventArgs e)
        {
            RunOnUiThread(() =>
            {
                if (!_isShortcutDown || _isRecording)
                    return;

                _altHoldTriggeredThisPress = true;
                ShortcutStatusText.Text = "长按录音";
                StartRecording(RecordingTrigger.KeyboardHold);
            });
        }

        private void OnShortcutReleased(object? sender, EventArgs e)
        {
            RunOnUiThread(() =>
            {
                _isShortcutDown = false;

                if (_altHoldTriggeredThisPress)
                {
                    ShortcutStatusText.Text = "释放";
                    if (_isRecording && _activeTrigger == RecordingTrigger.KeyboardHold)
                        StopRecording();
                    return;
                }

                // 短按：点击切换长录音
                if (_keyboardToggleActive)
                {
                    ShortcutStatusText.Text = "结束";
                    StopRecording();
                }
                else if (!_isRecording)
                {
                    ShortcutStatusText.Text = "录音中(再按结束)";
                    StartRecording(RecordingTrigger.KeyboardToggle);
                    _keyboardToggleActive = true;
                }
            });
        }

        private void StartRecording(RecordingTrigger trigger)
        {
            try
            {
                _activeTrigger = trigger;
                _isRecording = true;
                _audioCapture?.StartRecording(_config.SampleRate, _config.Channels, _config.BitDepth);
                _voiceOverlay?.ShowRecording();
            }
            catch (Exception ex)
            {
                ShowNotification("录音启动失败", ex.Message);
                _isRecording = false;
                _activeTrigger = RecordingTrigger.None;
                _voiceOverlay?.HideOverlay();
            }
        }

        private async void StopRecording()
        {
            if (!_isRecording)
                return;

            try
            {
                _isRecording = false;
                _activeTrigger = RecordingTrigger.None;
                _keyboardToggleActive = false;
                _audioCapture?.StopRecording();
                _voiceOverlay?.ShowProcessing();

                // 获取完整的音频数据并进行识别
                var audioData = _audioCapture?.GetCompleteAudio();
                if (audioData != null && _speechRecognizer != null)
                {
                    RecognitionStatusText.Text = "正在识别...";
                    var result = await _speechRecognizer.RecognizeFromBufferAsync(audioData, _config.SampleRate);
                    if (!string.IsNullOrEmpty(result))
                        OnTextRecognized(this, result);
                }

                _voiceOverlay?.HideOverlay();
            }
            catch (Exception ex)
            {
                _voiceOverlay?.HideOverlay();
                ShowNotification("录音停止失败", ex.Message);
            }
        }

        private async void OnTextRecognized(object? sender, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            await Dispatcher.InvokeAsync(() =>
            {
                LastRecognizedText.Text = text;
            });

            try
            {
                if (_config.UseClipboard)
                {
                    _textSimulator?.InsertText(text);
                }
                else
                {
                    await _textSimulator?.TypeTextAsync(text)!;
                }

                ShowNotification("文字输入完成", text);
            }
            catch (Exception ex)
            {
                ShowNotification("文字输入失败", ex.Message);
            }
        }

        private void OnAudioStatusChanged(object? sender, string status)
        {
            Dispatcher.InvokeAsync(() =>
            {
                RecordingStatusText.Text = status;
            });
        }

        private void OnRecognitionStatusChanged(object? sender, string status)
        {
            Dispatcher.InvokeAsync(() =>
            {
                RecognitionStatusText.Text = status;
            });
        }

        private void OnSpeechError(object? sender, Exception error)
        {
            Dispatcher.InvokeAsync(() =>
            {
                ShowNotification("语音识别错误", error.Message);
            });
        }

        private void UpdateStatus(object? sender, EventArgs e)
        {
            if (!_isShortcutDown && ShortcutStatusText.Text != "等待中...")
                ShortcutStatusText.Text = "等待中...";
        }

        private static void SelectComboBoxByTag(System.Windows.Controls.ComboBox? comboBox, string tagValue)
        {
            if (comboBox == null)
                return;

            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Tag?.ToString() == tagValue)
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
        }

        private void RefreshEngineComboBox()
        {
            if (EngineComboBox == null)
                return;

            _isLoadingSettings = true;
            try
            {
                var selectable = SpeechModelManager.GetSelectableModels().ToList();
                var savedEngine = _config.RecognitionEngine;

                EngineComboBox.Items.Clear();

                if (selectable.Count == 0)
                {
                    EngineComboBox.IsEnabled = false;
                    EngineComboBox.Items.Add(new ComboBoxItem
                    {
                        Content = "请先下载模型",
                        Tag = "",
                        IsEnabled = false
                    });
                    EngineComboBox.SelectedIndex = 0;
                    return;
                }

                EngineComboBox.IsEnabled = true;
                foreach (var model in selectable)
                {
                    EngineComboBox.Items.Add(new ComboBoxItem
                    {
                        Content = model.DisplayName,
                        Tag = model.EngineTag
                    });
                }

                var target = selectable.FirstOrDefault(m =>
                    m.EngineTag.Equals(savedEngine, StringComparison.OrdinalIgnoreCase));

                if (target == null)
                {
                    target = selectable[0];
                    _config.RecognitionEngine = target.EngineTag;
                    _config.Save();
                }

                SelectComboBoxByTag(EngineComboBox, target.EngineTag);
            }
            finally
            {
                _isLoadingSettings = false;
            }
        }

        private void EngineComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings)
                return;

            try
            {
                if (EngineComboBox?.SelectedItem is not ComboBoxItem item
                    || string.IsNullOrEmpty(item.Tag?.ToString()))
                    return;

                var newEngine = item.Tag.ToString()!;
                if (_config.RecognitionEngine == newEngine)
                    return;

                if (!SpeechModelManager.IsInstalled(newEngine))
                {
                    MessageBox.Show(
                        "该引擎尚未下载，请先在「工具 → 模型管理」中下载。",
                        "无法切换",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    RefreshEngineComboBox();
                    return;
                }

                _config.RecognitionEngine = newEngine;
                _config.Save();

                if (_serviceRunning)
                    StopService();

                _speechRecognizer?.Dispose();
                _speechRecognizer = new SpeechRecognizer();
                _speechRecognizer.StatusChanged += OnRecognitionStatusChanged;
                _speechRecognizer.Error += OnSpeechError;

                RecognitionStatusText.Text = "引擎已切换，请重新启动服务";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"引擎选择错误: {ex.Message}");
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings)
                return;

            try
            {
                if (LanguageComboBox?.SelectedItem is ComboBoxItem item && item.Tag != null)
                {
                    var newLanguage = item.Tag.ToString()!;
                    if (_config.RecognitionLanguage == newLanguage)
                        return;

                    _config.RecognitionLanguage = newLanguage;
                    _config.Save();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"语言选择错误: {ex.Message}");
            }
        }

        private void ShowNotificationsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings)
                return;

            _config.ShowNotifications = ShowNotificationsCheckBox.IsChecked == true;
            _config.Save();
        }

        private void UseClipboardCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings)
                return;

            _config.UseClipboard = UseClipboardCheckBox.IsChecked == true;
            _config.Save();
        }

        private void ShowNotification(string title, string message)
        {
            if (_config.ShowNotifications)
                _trayIcon?.ShowBalloon(title, message);

            System.Diagnostics.Debug.WriteLine($"{title}: {message}");
        }

        private void SilentStartCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings || SilentStartCheckBox == null)
                return;

            _config.SilentStart = SilentStartCheckBox.IsChecked == true;
            _config.Save();

            if (StartupHelper.IsEnabled())
            {
                try
                {
                    StartupHelper.SetEnabled(true, _config.SilentStart);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"更新开机自启动参数失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void MinimizeToTrayCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings || MinimizeToTrayCheckBox == null)
                return;

            _config.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked == true;
            _config.Save();
        }

        private void AutoStartCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings || AutoStartCheckBox == null)
                return;

            try
            {
                var enabled = AutoStartCheckBox.IsChecked == true;
                StartupHelper.SetEnabled(enabled, _config.SilentStart);
                _config.AutoStartWithWindows = enabled;
                _config.Save();
            }
            catch (Exception ex)
            {
                AutoStartCheckBox.IsChecked = StartupHelper.IsEnabled();
                MessageBox.Show($"设置开机自启动失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isExiting && _config.MinimizeToTray)
            {
                e.Cancel = true;
                Hide();
                ShowInTaskbar = false;
                ShowNotification("语音输入", "程序已最小化到托盘");
                return;
            }

            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                if (!_isExiting)
                    StopService();

                _statusTimer?.Stop();
                _keyboardHook?.Dispose();
                _audioCapture?.Dispose();
                _speechRecognizer?.Dispose();
                _voiceOverlay?.Close();
                _voiceOverlay = null;
                _trayIcon?.Dispose();
                _trayIcon = null;
                SpeechModelManager.ModelsChanged -= OnModelsChanged;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理资源时出错: {ex.Message}");
            }

            base.OnClosed(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape && IsVisible)
                Hide();
            base.OnKeyDown(e);
        }
    }
}