using System;

namespace MouseClickVoice;

public sealed class NoOpKeyboardHook : IKeyboardHookService
{
    public int HoldThresholdMs { get; set; } = 400;
    public bool IsSupported => false;

    public event EventHandler? ShortcutPressed;
    public event EventHandler? ShortcutReleased;
    public event EventHandler? ShortcutHoldDetected;

    public void Start() { }
    public void Stop() { }
    public void Dispose() { }
}
