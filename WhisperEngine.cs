using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;
using Whisper.net;
using Whisper.net.Ggml;

namespace MouseClickVoice
{
    public class WhisperEngine : ISpeechEngine
    {
        private WhisperFactory? _whisperFactory;
        private WhisperProcessor? _processor;
        private readonly string _modelPath;
        private bool _isInitialized;
        private bool _isModelDownloaded;

        public event EventHandler<string>? StatusChanged;
        public event EventHandler<Exception>? Error;

        public string EngineName => "Whisper";

        public bool IsInitialized => _isInitialized;

        public WhisperEngine()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var modelsDir = Path.Combine(appDir, "models");
            _modelPath = Path.Combine(modelsDir, "ggml-tiny.bin");
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                if (_isInitialized)
                    return true;

                StatusChanged?.Invoke(this, "正在初始化 Whisper 引擎...");

                if (!await DownloadModelAsync())
                    return false;

                _whisperFactory = WhisperFactory.FromPath(_modelPath);
                _processor = _whisperFactory.CreateBuilder()
                    .WithLanguage("zh")
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

        private async Task<bool> DownloadModelAsync()
        {
            try
            {
                if (_isModelDownloaded)
                    return true;

                StatusChanged?.Invoke(this, "正在下载 Whisper tiny 模型...");

                var modelsDir = Path.GetDirectoryName(_modelPath)!;
                if (!Directory.Exists(modelsDir))
                    Directory.CreateDirectory(modelsDir);

                if (File.Exists(_modelPath))
                {
                    _isModelDownloaded = true;
                    return true;
                }

                await using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(GgmlType.Tiny);
                await using var fileStream = File.Create(_modelPath);
                await modelStream.CopyToAsync(fileStream);

                _isModelDownloaded = true;
                StatusChanged?.Invoke(this, "Whisper 模型下载完成");
                return true;
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new Exception($"模型下载失败: {ex.Message}"));
                return false;
            }
        }

        public async Task<string?> RecognizeFromFileAsync(string wavFilePath)
        {
            try
            {
                if (!_isInitialized)
                    await InitializeAsync();

                if (_processor == null || !File.Exists(wavFilePath))
                    return null;

                StatusChanged?.Invoke(this, "正在识别语音...");

                var resultText = string.Empty;
                using var fileStream = File.OpenRead(wavFilePath);
                await foreach (var segment in _processor.ProcessAsync(fileStream))
                {
                    if (!string.IsNullOrWhiteSpace(segment.Text))
                        resultText += segment.Text;
                }

                StatusChanged?.Invoke(this, "识别完成");
                return string.IsNullOrWhiteSpace(resultText) ? null : resultText.Trim();
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new Exception($"语音识别失败: {ex.Message}"));
                return null;
            }
        }

        public async Task<string?> RecognizeFromBufferAsync(byte[] audioBuffer, int sampleRate = 16000)
        {
            if (audioBuffer == null || audioBuffer.Length == 0)
                return null;

            if (!_isInitialized)
                await InitializeAsync();

            var tempFile = Path.GetTempFileName() + ".wav";
            try
            {
                using (var writer = new WaveFileWriter(tempFile, new WaveFormat(sampleRate, 16, 1)))
                {
                    writer.Write(audioBuffer, 0, audioBuffer.Length);
                }

                return await RecognizeFromFileAsync(tempFile);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        public void Dispose()
        {
            _processor?.Dispose();
            _whisperFactory?.Dispose();
            _isInitialized = false;
        }
    }
}
