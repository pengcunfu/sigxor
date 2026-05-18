using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MouseClickVoice
{
    public class Config
    {
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MouseClickVoice",
            "config.json"
        );

        // 鼠标长按检测设置
        [JsonPropertyName("longPressDuration")]
        public double LongPressDuration { get; set; } = 1.5;

        // 音频录制设置
        [JsonPropertyName("sampleRate")]
        public int SampleRate { get; set; } = 16000;

        [JsonPropertyName("channels")]
        public int Channels { get; set; } = 1;

        [JsonPropertyName("bitDepth")]
        public int BitDepth { get; set; } = 16;

        // 语音识别设置
        [JsonPropertyName("recognitionEngine")]
        public string RecognitionEngine { get; set; } = "sensevoice";

        [JsonPropertyName("recognitionLanguage")]
        public string RecognitionLanguage { get; set; } = "zh-CN";

        [JsonPropertyName("confidenceThreshold")]
        public double ConfidenceThreshold { get; set; } = 0.6;

        // 输入设置
        [JsonPropertyName("typingDelay")]
        public double TypingDelay { get; set; } = 0.05;

        [JsonPropertyName("useClipboard")]
        public bool UseClipboard { get; set; } = false;

        // 应用程序设置
        [JsonPropertyName("startMinimized")]
        public bool StartMinimized { get; set; } = false;

        [JsonPropertyName("autoStartWithWindows")]
        public bool AutoStartWithWindows { get; set; } = false;

        [JsonPropertyName("showNotifications")]
        public bool ShowNotifications { get; set; } = true;

        // 调试设置
        [JsonPropertyName("debugMode")]
        public bool DebugMode { get; set; } = false;

        [JsonPropertyName("saveAudioFiles")]
        public bool SaveAudioFiles { get; set; } = false;

        private static Config? _instance;
        public static Config Instance
        {
            get
            {
                _instance ??= LoadConfig();
                return _instance;
            }
        }

        private Config()
        {
        }

        public static Config LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    var config = JsonSerializer.Deserialize<Config>(json);
                    return config ?? new Config();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载配置文件失败: {ex.Message}");
            }

            return new Config();
        }

        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存配置文件失败: {ex.Message}");
            }
        }

        public void ResetToDefaults()
        {
            LongPressDuration = 1.5;
            SampleRate = 16000;
            Channels = 1;
            BitDepth = 16;
            RecognitionEngine = "sensevoice";
            RecognitionLanguage = "zh-CN";
            ConfidenceThreshold = 0.6;
            TypingDelay = 0.05;
            UseClipboard = false;
            StartMinimized = false;
            AutoStartWithWindows = false;
            ShowNotifications = true;
            DebugMode = false;
            SaveAudioFiles = false;
        }

        public static string GetAudioSavePath()
        {
            var audioPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MouseClickVoice",
                "Audio"
            );

            if (!Directory.Exists(audioPath))
            {
                Directory.CreateDirectory(audioPath);
            }

            return audioPath;
        }
    }
}