using FluxKit.Platform.Native;
using static FluxKit.Platform.Native.NativeMethods;

namespace FluxKit.Platform.VirtualDesktop.Interop;

/// <summary>Vtable layout of the undocumented shell virtual-desktop interfaces for one Windows build range.</summary>
internal sealed record VdComLayout(string Name, Guid ManagerInternalIid, Guid DesktopIid);

/// <summary>
/// Thin wrapper over the undocumented Explorer virtual desktop COM API.
/// Calls go through raw vtable slots so the IIDs can be chosen at runtime.
/// Must be created and used on a single STA thread (see <see cref="Threading.StaWorker"/>).
/// </summary>
/// <remarks>
/// Interface shapes follow the MIT-licensed community references (MScholtes/VirtualDesktop,
/// Ciantic/VirtualDesktopAccessor). Every layout is validated against the registry before use.
/// </remarks>
internal sealed unsafe class ShellComSession : IDisposable
{
    private static readonly Guid ClsidImmersiveShell = new("C2F03A33-21F5-47FA-B4BB-156362A2F239");
    private static readonly Guid IidServiceProvider = new("6D5140C1-7436-11CE-8034-00AA006009FA");
    private static readonly Guid SidVirtualDesktopManagerInternal = new("C5E0CDCA-7B6E-41B2-9FC4-D93975CC467B");
    private static readonly Guid SidVirtualDesktopPinnedApps = new("B5A399E7-1C87-46B8-88E9-FC5747B171BD");
    private static readonly Guid IidVirtualDesktopPinnedApps = new("4CE81583-1E4C-4632-A621-07A53543148F");
    private static readonly Guid IidApplicationViewCollection = new("1841C6D7-4F9D-42C0-AF41-8747538F10E5");

    // Shared vtable slots (identical for every supported layout).
    private const int SlotQueryService = 3;
    private const int SlotRelease = 2;
    private const int SlotMgrGetCount = 3;
    private const int SlotMgrMoveViewToDesktop = 4;
    private const int SlotMgrCanViewMoveDesktops = 5;
    private const int SlotMgrGetCurrentDesktop = 6;
    private const int SlotMgrGetDesktops = 7;
    private const int SlotMgrSwitchDesktop = 9;
    private const int SlotDesktopGetId = 4;
    private const int SlotArrayGetCount = 3;
    private const int SlotArrayGetAt = 4;
    private const int SlotViewCollectionGetViewForHwnd = 6;
    private const int SlotPinnedIsViewPinned = 6;
    private const int SlotPinnedPinView = 7;

    public static IReadOnlyList<VdComLayout> KnownLayouts { get; } =
    [
        new("Win11-24H2", new("53F5CA0B-158F-4124-900C-057158060B27"), new("3F07F4BE-B107-441A-AF0F-39D82529072C")),
        new("Win11-23H2", new("A3175F2D-239C-4BD2-8AA0-EEBA8B0B138E"), new("3F07F4BE-B107-441A-AF0F-39D82529072C")),
    ];

    private nint _serviceProvider;
    private nint _manager;
    private nint _pinnedApps;
    private nint _viewCollection;

    private ShellComSession(VdComLayout layout) => Layout = layout;

    public VdComLayout Layout { get; }

    /// <summary>Tries each known layout and returns the first that resolves; null when none does.</summary>
    public static ShellComSession? TryCreate(out string diagnostics)
    {
        var log = new List<string>();
        int hr = CoCreateInstance(ClsidImmersiveShell, 0, CLSCTX_LOCAL_SERVER, IidServiceProvider, out nint provider);
        if (hr != S_OK || provider == 0)
        {
            diagnostics = $"ImmersiveShell unavailable (hr=0x{hr:X8})";
            return null;
        }

        foreach (VdComLayout layout in KnownLayouts)
        {
            nint manager = QueryService(provider, SidVirtualDesktopManagerInternal, layout.ManagerInternalIid, out hr);
            if (manager == 0)
            {
                log.Add($"{layout.Name}: QueryService hr=0x{hr:X8}");
                continue;
            }

            var session = new ShellComSession(layout) { _serviceProvider = provider, _manager = manager };
            session._pinnedApps = QueryService(provider, SidVirtualDesktopPinnedApps, IidVirtualDesktopPinnedApps, out _);
            session._viewCollection = QueryService(provider, IidApplicationViewCollection, IidApplicationViewCollection, out _);
            log.Add($"{layout.Name}: resolved");
            diagnostics = string.Join("; ", log);
            return session;
        }

        Release(provider);
        diagnostics = string.Join("; ", log);
        return null;
    }

    public bool CanPin => _pinnedApps != 0 && _viewCollection != 0;

    public int GetCount()
    {
        int count;
        ThrowIfFailed(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(_manager, SlotMgrGetCount))(_manager, &count));
        return count;
    }

    public Guid GetCurrentDesktopId()
    {
        nint desktop;
        ThrowIfFailed(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(_manager, SlotMgrGetCurrentDesktop))(_manager, &desktop));
        try
        {
            return GetDesktopId(desktop);
        }
        finally
        {
            Release(desktop);
        }
    }

    public IReadOnlyList<Guid> GetDesktopIds()
    {
        var ids = new List<Guid>();
        ForEachDesktop((_, id, _) =>
        {
            ids.Add(id);
            return false;
        });
        return ids;
    }

    /// <summary>Switches to the desktop with <paramref name="id"/>; returns false if it no longer exists.</summary>
    public bool SwitchTo(Guid id)
    {
        bool found = false;
        ForEachDesktop((_, desktopId, desktop) =>
        {
            if (desktopId != id)
            {
                return false;
            }

            ThrowIfFailed(((delegate* unmanaged[Stdcall]<nint, nint, int>)Slot(_manager, SlotMgrSwitchDesktop))(_manager, desktop));
            found = true;
            return true;
        });
        return found;
    }

    public bool CanMoveWindows => _viewCollection != 0;

    /// <summary>
    /// Moves another process's top-level window to the desktop with <paramref name="desktopId"/>.
    /// Returns a reason string on failure (null on success). Windows of elevated apps can't be moved.
    /// </summary>
    public string? MoveWindow(nint hwnd, Guid desktopId)
    {
        if (_viewCollection == 0)
        {
            return "Window moves aren't available on this Windows build.";
        }

        nint viewOut;
        int hr = ((delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)Slot(_viewCollection, SlotViewCollectionGetViewForHwnd))(_viewCollection, hwnd, &viewOut);
        nint view = viewOut; // captured by the lambda below, so it can't be the local whose address was taken
        if (hr != S_OK || view == 0)
        {
            return "This window can't be moved to another desktop.";
        }

        try
        {
            int canMove;
            hr = ((delegate* unmanaged[Stdcall]<nint, nint, int*, int>)Slot(_manager, SlotMgrCanViewMoveDesktops))(_manager, view, &canMove);
            if (hr == S_OK && canMove == 0)
            {
                return "This window can't be moved to another desktop.";
            }

            string? error = "That desktop no longer exists.";
            ForEachDesktop((_, id, desktop) =>
            {
                if (id != desktopId)
                {
                    return false;
                }

                int moveHr = ((delegate* unmanaged[Stdcall]<nint, nint, nint, int>)Slot(_manager, SlotMgrMoveViewToDesktop))(_manager, view, desktop);
                error = moveHr == S_OK ? null : $"Windows refused to move the window (0x{moveHr:X8}).";
                return true;
            });
            return error;
        }
        finally
        {
            Release(view);
        }
    }

    /// <summary>Pins a top-level window so it is visible on every desktop.</summary>
    public bool PinWindow(nint hwnd)
    {
        if (!CanPin)
        {
            return false;
        }

        nint view;
        int hr = ((delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)Slot(_viewCollection, SlotViewCollectionGetViewForHwnd))(_viewCollection, hwnd, &view);
        if (hr != S_OK || view == 0)
        {
            return false;
        }

        try
        {
            int pinned;
            hr = ((delegate* unmanaged[Stdcall]<nint, nint, int*, int>)Slot(_pinnedApps, SlotPinnedIsViewPinned))(_pinnedApps, view, &pinned);
            if (hr == S_OK && pinned != 0)
            {
                return true;
            }

            hr = ((delegate* unmanaged[Stdcall]<nint, nint, int>)Slot(_pinnedApps, SlotPinnedPinView))(_pinnedApps, view);
            return hr == S_OK;
        }
        finally
        {
            Release(view);
        }
    }

    public void Dispose()
    {
        Release(_viewCollection);
        Release(_pinnedApps);
        Release(_manager);
        Release(_serviceProvider);
        _viewCollection = _pinnedApps = _manager = _serviceProvider = 0;
    }

    /// <summary>Visits each desktop in order; the callback returns true to stop.</summary>
    private void ForEachDesktop(Func<int, Guid, nint, bool> visit)
    {
        nint array;
        ThrowIfFailed(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(_manager, SlotMgrGetDesktops))(_manager, &array));
        try
        {
            uint count;
            ThrowIfFailed(((delegate* unmanaged[Stdcall]<nint, uint*, int>)Slot(array, SlotArrayGetCount))(array, &count));
            Guid desktopIid = Layout.DesktopIid;
            for (uint i = 0; i < count; i++)
            {
                nint desktop;
                ThrowIfFailed(((delegate* unmanaged[Stdcall]<nint, uint, Guid*, nint*, int>)Slot(array, SlotArrayGetAt))(array, i, &desktopIid, &desktop));
                try
                {
                    if (visit((int)i, GetDesktopId(desktop), desktop))
                    {
                        return;
                    }
                }
                finally
                {
                    Release(desktop);
                }
            }
        }
        finally
        {
            Release(array);
        }
    }

    private static Guid GetDesktopId(nint desktop)
    {
        Guid id;
        ThrowIfFailed(((delegate* unmanaged[Stdcall]<nint, Guid*, int>)Slot(desktop, SlotDesktopGetId))(desktop, &id));
        return id;
    }

    private static nint QueryService(nint provider, Guid service, Guid iid, out int hr)
    {
        nint result;
        hr = ((delegate* unmanaged[Stdcall]<nint, Guid*, Guid*, nint*, int>)Slot(provider, SlotQueryService))(provider, &service, &iid, &result);
        return hr == S_OK ? result : 0;
    }

    private static void* Slot(nint comObject, int index) => (*(void***)comObject)[index];

    private static void Release(nint comObject)
    {
        if (comObject != 0)
        {
            _ = ((delegate* unmanaged[Stdcall]<nint, uint>)Slot(comObject, SlotRelease))(comObject);
        }
    }

    private static void ThrowIfFailed(int hr)
    {
        if (hr < 0)
        {
            throw new ShellComException(hr);
        }
    }
}

/// <summary>A failed HRESULT from the shell virtual desktop API.</summary>
internal sealed class ShellComException(int hresult)
    : Exception($"Shell virtual desktop call failed (hr=0x{hresult:X8})")
{
    public int Code { get; } = hresult;

    public bool IsDisconnected => Code is NativeMethods.RPC_E_DISCONNECTED or NativeMethods.RPC_E_SERVER_DIED_DNE;
}
