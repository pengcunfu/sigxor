using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.Ggml;

namespace MouseClickVoice
{
    public class SpeechRecognizer : IDisposable
    {
        private WhisperFactory? _whisperFactory;
        private WhisperProcessor? _processor;
        private readonly string _modelPath;
        private bool _isInitialized;
        private readonly object _lockObject = new object();
        private bool _isModelDownloaded;

        public event EventHandler<string>? TextRecognized;
        public event EventHandler<string>? StatusChanged;
        public event EventHandler<Exception>? Error;

        public SpeechRecognizer()
        {
            // 设置模型路径为程序目录下的 models 文件夹
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var modelsDir = Path.Combine(appDir, "models");
            _modelPath = Path.Combine(modelsDir, "ggml-tiny.bin");

            _isInitialized = false;
            _isModelDownloaded = false;

            StatusChanged?.Invoke(this, "语音识别器初始化完成");
        }

        /// <summary>
        /// 下载 Whisper tiny 模型
        /// </summary>
        public async Task<bool> DownloadModelAsync()
        {
            try
            {
                if (_isModelDownloaded)
                    return true;

                StatusChanged?.Invoke(this, "正在下载 Whisper tiny 模型...");

                // 创建 models 目录
                var modelsDir = Path.GetDirectoryName(_modelPath)!;
                if (!Directory.Exists(modelsDir))
                {
                    Directory.CreateDirectory(modelsDir);
                }

                // 如果模型已存在，不需要重新下载
                if (File.Exists(_modelPath))
                {
                    StatusChanged?.Invoke(this, "模型文件已存在");
                    _isModelDownloaded = true;
                    return true;
                }

                // 使用 Whisper.net 下载模型
                await using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(
                    GgmlType.Tiny
                );

                // 保存模型到本地
                await using var fileStream = File.Create(_modelPath);
                await modelStream.CopyToAsync(fileStream);

                StatusChanged?.Invoke(this, "模型下载完成");
                _isModelDownloaded = true;
                return true;
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new Exception($"模型下载失败: {ex.Message}"));
                return false;
            }
        }

        /// <summary>
        /// 初始化 Whisper 处理器
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                if (_isInitialized)
                    return true;

                StatusChanged?.Invoke(this, "正在初始化 Whisper 引擎...");

                // 确保模型已下载
                if (!await DownloadModelAsync())
                {
                    return false;
                }

                // 创建 Whisper 工厂
                _whisperFactory = WhisperFactory.FromPath(_modelPath);

                // 创建处理器
                _processor = _whisperFactory.CreateBuilder()
                    .WithLanguage("zh")
                    .WithSegmentEventHandler((segment) =>
                    {
                        if (!string.IsNullOrWhiteSpace(segment.Text))
                        {
                            TextRecognized?.Invoke(this, segment.Text);
                        }
                    })
                    .Build();

                _isInitialized = true;
                StatusChanged?.Invoke(this, "Whisper 引擎初始化完成");
                return true;
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new Exception($"Whisper 初始化失败: {ex.Message}"));
                return false;
            }
        }

        /// <summary>
        /// 从 WAV 文件识别语音
        /// </summary>
        public async Task<string?> RecognizeFromFileAsync(string wavFilePath)
        {
            try
            {
                if (!_isInitialized)
                {
                    await InitializeAsync();
                }

                if (_processor == null || !File.Exists(wavFilePath))
                {
                    return null;
                }

                StatusChanged?.Invoke(this, "正在识别语音...");

                var resultText = string.Empty;

                // 处理音频文件
                using var fileStream = File.OpenRead(wavFilePath);
                await foreach (var segment in _processor.ProcessAsync(fileStream))
                {
                    if (!string.IsNullOrWhiteSpace(segment.Text))
                    {
                        resultText += segment.Text;
                    }
                }

                StatusChanged?.Invoke(this, "识别完成");
                return string.IsNullOrWhiteSpace(resultText) ? null : resultText;
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new Exception($"语音识别失败: {ex.Message}"));
                return null;
            }
        }

        /// <summary>
        /// 从音频缓冲区识别语音
        /// </summary>
        public async Task<string?> RecognizeFromBufferAsync(byte[] audioBuffer, int sampleRate = 16000)
        {
            try
            {
                if (!_isInitialized)
                {
                    await InitializeAsync();
                }

                if (_processor == null || audioBuffer == null || audioBuffer.Length == 0)
                {
                    return null;
                }

                StatusChanged?.Invoke(this, "正在识别语音...");

                // 将音频数据保存为临时 WAV 文件
                var tempFile = Path.GetTempFileName() + ".wav";
                try
                {
                    // 将 byte[] 转换为 WAV 文件
                    using (var writer = new NAudio.Wave.WaveFileWriter(tempFile,
                        new NAudio.Wave.WaveFormat(sampleRate, 16, 1)))
                    {
                        writer.Write(audioBuffer, 0, audioBuffer.Length);
                    }

                    // 识别
                    var result = await RecognizeFromFileAsync(tempFile);
                    return result;
                }
                finally
                {
                    // 删除临时文件
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new Exception($"语音识别失败: {ex.Message}"));
                return null;
            }
        }

        public bool IsInitialized => _isInitialized;

        public void Dispose()
        {
            _processor?.Dispose();
            _whisperFactory?.Dispose();
        }
    }
}
