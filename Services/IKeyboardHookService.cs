using System;

namespace MouseClickVoice;

public interface IKeyboardHookService : IDisposable
{
    int HoldThresholdMs { get; set; }
    event EventHandler? ShortcutPressed;
    event EventHandler? ShortcutReleased;
    event EventHandler? ShortcutHoldDetected;
    void Start();
    void Stop();
    bool IsSupported { get; }
}
