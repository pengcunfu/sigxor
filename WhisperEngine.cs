using System;
using System.IO;
using System.Threading;
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

        public event EventHandler<string>? StatusChanged;
        public event EventHandler<Exception>? Error;

        public string EngineName => "Whisper";

        public bool IsInitialized => _isInitialized;

        public WhisperEngine()
        {
            _modelPath = Path.Combine(SpeechModelManager.ModelsDirectory, "ggml-tiny.bin");
        }

        public static bool IsModelInstalled() => File.Exists(GetModelPath());

        public static string GetModelPath() =>
            Path.Combine(SpeechModelManager.ModelsDirectory, "ggml-tiny.bin");

        public static long GetModelSizeOnDisk()
        {
            var path = GetModelPath();
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }

        public static bool DeleteModel()
        {
            var path = GetModelPath();
            if (File.Exists(path))
                File.Delete(path);
            return true;
        }

        public static async Task<bool> DownloadModelAsync(
            Action<string>? status = null,
            CancellationToken cancellationToken = default)
        {
            var engine = new WhisperEngine();
            engine.StatusChanged += (_, msg) => status?.Invoke(msg);
            engine.Error += (_, ex) => status?.Invoke(ex.Message);
            return await engine.DownloadModelFilesAsync(cancellationToken);
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                if (_isInitialized)
                    return true;

                StatusChanged?.Invoke(this, "正在初始化 Whisper 引擎...");

                if (!IsModelInstalled())
                {
                    Error?.Invoke(this, new Exception(
                        "Whisper 模型未下载，请在「工具 → 模型管理」中下载后再启动服务"));
                    return false;
                }

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

        private async Task<bool> DownloadModelFilesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (IsModelInstalled())
                {
                    StatusChanged?.Invoke(this, "Whisper 模型已就绪");
                    return true;
                }

                cancellationToken.ThrowIfCancellationRequested();
                StatusChanged?.Invoke(this, "正在下载 Whisper Tiny 模型...");

                var modelsDir = Path.GetDirectoryName(_modelPath)!;
                Directory.CreateDirectory(modelsDir);

                await using var modelStream = await WhisperGgmlDownloader.Default
                    .GetGgmlModelAsync(GgmlType.Tiny);
                await using var fileStream = File.Create(_modelPath);
                await modelStream.CopyToAsync(fileStream, cancellationToken);

                StatusChanged?.Invoke(this, "Whisper 模型下载完成");
                return true;
            }
            catch (OperationCanceledException)
            {
                if (File.Exists(_modelPath))
                    File.Delete(_modelPath);
                throw;
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
                {
                    Error?.Invoke(this, new Exception("Whisper 引擎未初始化"));
                    return null;
                }

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
            {
                Error?.Invoke(this, new Exception("Whisper 引擎未初始化"));
                return null;
            }

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
