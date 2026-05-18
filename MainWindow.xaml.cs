using System;
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
        private MouseHook? _mouseHook;
        private AudioCapture? _audioCapture;
        private SpeechRecognizer? _speechRecognizer;
        private TextSimulator? _textSimulator;
        private bool _isRecording;
        private bool _isMouseDown;
        private readonly DispatcherTimer _statusTimer;
        private readonly Config _config;

        public MainWindow()
        {
            _config = Config.Instance;
            InitializeComponent(); // 这会自动调用InitializeComponents()
            LoadUserSettings(); // 重命名避免冲突
            InitializeServices();

            _statusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(0.1)
            };
            _statusTimer.Tick += UpdateStatus;
            _statusTimer.Start();
        }

        private void LoadUserSettings()
        {
            // 从配置加载设置，使用延迟加载避免初始化顺序问题
            if (_config != null)
            {
                // 延迟设置以确保UI组件已完全初始化
                Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        if (LongPressSlider != null && LongPressValueText != null)
                        {
                            LongPressSlider.Value = _config.LongPressDuration;
                            LongPressValueText.Text = $"{_config.LongPressDuration:F1}s";
                        }

                        SelectComboBoxByTag(EngineComboBox, _config.RecognitionEngine);
                        SelectComboBoxByTag(LanguageComboBox, _config.RecognitionLanguage);

                        if (ShowNotificationsCheckBox != null && UseClipboardCheckBox != null)
                        {
                            ShowNotificationsCheckBox.IsChecked = _config.ShowNotifications;
                            UseClipboardCheckBox.IsChecked = _config.UseClipboard;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"加载设置失败: {ex.Message}");
                    }
                });
            }
        }

        private void InitializeServices()
        {
            try
            {
                _mouseHook = new MouseHook();
                _mouseHook.MousePressed += OnMousePressed;
                _mouseHook.MouseReleased += OnMouseReleased;
                _mouseHook.LongPressDetected += OnLongPressDetected;

                _audioCapture = new AudioCapture();
                _audioCapture.StatusChanged += OnAudioStatusChanged;

                _speechRecognizer = new SpeechRecognizer();
                _speechRecognizer.TextRecognized += OnTextRecognized;
                _speechRecognizer.StatusChanged += OnRecognitionStatusChanged;
                _speechRecognizer.Error += OnSpeechError;

                _textSimulator = new TextSimulator(_config.TypingDelay);

                RecognitionStatusText.Text = "已初始化";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化服务失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            await StartService();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopService();
        }

        private async Task StartService()
        {
            try
            {
                var engineName = _config.RecognitionEngine.Equals("whisper", StringComparison.OrdinalIgnoreCase)
                    ? "Whisper" : "SenseVoice";
                RecognitionStatusText.Text = $"正在初始化 {engineName}...";
                if (_speechRecognizer != null && !_speechRecognizer.IsInitialized)
                {
                    var initSuccess = await _speechRecognizer.InitializeAsync();
                    if (!initSuccess)
                    {
                        MessageBox.Show($"{engineName} 初始化失败，请检查网络连接和模型下载", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                _mouseHook?.Start();
                await Task.Delay(100);

                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;

                ShowNotification("服务已启动", "按住鼠标左键1.5秒开始语音输入");
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
                _mouseHook?.Stop();
                _audioCapture?.StopRecording();

                _isRecording = false;
                _isMouseDown = false;

                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;

                ShowNotification("服务已停止", "语音输入功能已关闭");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"停止服务失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnMousePressed(object? sender, MouseEventArgs e)
        {
            _isMouseDown = true;
            MouseStatusText.Text = "按下";
        }

        private void OnMouseReleased(object? sender, MouseEventArgs e)
        {
            _isMouseDown = false;
            MouseStatusText.Text = "释放";

            if (_isRecording)
            {
                StopRecording();
            }
        }

        private void OnLongPressDetected(object? sender, MouseEventArgs e)
        {
            if (_isMouseDown && !_isRecording)
            {
                StartRecording();
            }
        }

        private void StartRecording()
        {
            try
            {
                _isRecording = true;
                _audioCapture?.StartRecording(_config.SampleRate, _config.Channels, _config.BitDepth);
            }
            catch (Exception ex)
            {
                ShowNotification("录音启动失败", ex.Message);
                _isRecording = false;
            }
        }

        private async void StopRecording()
        {
            try
            {
                _isRecording = false;
                _audioCapture?.StopRecording();

                // 获取完整的音频数据并进行识别
                var audioData = _audioCapture?.GetCompleteAudio();
                if (audioData != null && _speechRecognizer != null)
                {
                    RecognitionStatusText.Text = "正在识别...";
                    var result = await _speechRecognizer.RecognizeFromBufferAsync(audioData, _config.SampleRate);
                    if (!string.IsNullOrEmpty(result))
                    {
                        OnTextRecognized(this, result);
                    }
                }
            }
            catch (Exception ex)
            {
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
            if (!_isMouseDown && MouseStatusText.Text != "等待中...")
            {
                MouseStatusText.Text = "等待中...";
            }
        }

        private void LongPressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (LongPressValueText != null)
            {
                LongPressValueText.Text = $"{e.NewValue:F1}s";
                _config.LongPressDuration = e.NewValue;
                _config.Save();
            }
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
                    break;
                }
            }
        }

        private void EngineComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (EngineComboBox?.SelectedItem is ComboBoxItem item && item.Tag != null)
                {
                    var newEngine = item.Tag.ToString()!;
                    if (_config.RecognitionEngine == newEngine)
                        return;

                    _config.RecognitionEngine = newEngine;
                    _config.Save();

                    // 切换引擎后需重新初始化
                    _speechRecognizer?.Dispose();
                    _speechRecognizer = new SpeechRecognizer();
                    _speechRecognizer.TextRecognized += OnTextRecognized;
                    _speechRecognizer.StatusChanged += OnRecognitionStatusChanged;
                    _speechRecognizer.Error += OnSpeechError;

                    RecognitionStatusText.Text = "引擎已切换，请重新启动服务";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"引擎选择错误: {ex.Message}");
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (LanguageComboBox?.SelectedItem is ComboBoxItem item && item.Tag != null)
                {
                    _config.RecognitionLanguage = item.Tag.ToString()!;
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
            _config.ShowNotifications = ShowNotificationsCheckBox.IsChecked == true;
            _config.Save();
        }

        private void UseClipboardCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _config.UseClipboard = UseClipboardCheckBox.IsChecked == true;
            _config.Save();
        }

        private void ShowNotification(string title, string message)
        {
            if (_config.ShowNotifications)
            {
                // 这里可以使用Windows通知API
                System.Diagnostics.Debug.WriteLine($"{title}: {message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                StopService();
                _statusTimer?.Stop();

                _mouseHook?.Dispose();
                _audioCapture?.Dispose();
                _speechRecognizer?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理资源时出错: {ex.Message}");
            }

            base.OnClosed(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
            base.OnKeyDown(e);
        }
    }
}