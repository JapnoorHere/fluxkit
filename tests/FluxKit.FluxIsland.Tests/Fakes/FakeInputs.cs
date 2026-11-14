using FluxKit.Tools.FluxIsland.Abstractions;

namespace FluxKit.FluxIsland.Tests.Fakes;

internal sealed class FakeLockKeys : ILockKeysSource
{
    public event EventHandler<LockKeyChange>? Changed;

    public void Toggle(bool capsLock, bool on) => Changed?.Invoke(this, new LockKeyChange(capsLock, on));

    public void Start()
    {
    }

    public void Dispose()
    {
    }
}

internal sealed class FakeDevices : IDeviceSource
{
    public event EventHandler<DeviceChange>? Changed;

    public void Change(string name, bool connected) => Changed?.Invoke(this, new DeviceChange(name, connected));

    public void Start()
    {
    }

    public void Dispose()
    {
    }
}

internal sealed class FakeHotkey : IHotkeySource
{
    public event EventHandler? Pressed;

    public void Press() => Pressed?.Invoke(this, EventArgs.Empty);

    public void Start()
    {
    }

    public void Dispose()
    {
    }
}
