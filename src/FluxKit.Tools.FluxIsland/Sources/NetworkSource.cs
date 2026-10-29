using FluxKit.Tools.FluxIsland.Abstractions;
using Windows.Networking.Connectivity;

namespace FluxKit.Tools.FluxIsland.Sources;

/// <summary>
/// <see cref="INetworkSource"/> over the Windows network status event. Switching networks drops the connection for a
/// moment, so "offline" is only reported once it has lasted a few seconds; "online" is reported at once.
/// </summary>
internal sealed class NetworkSource : INetworkSource
{
    private static readonly TimeSpan OfflineAfter = TimeSpan.FromSeconds(3);

    private readonly Timer _confirm;
    private volatile bool _online = true;

    public NetworkSource() => _confirm = new Timer(_ => Set(HasInternet()), null, Timeout.Infinite, Timeout.Infinite);

    public bool IsOnline => _online;

    public event EventHandler? Changed;

    public void Start()
    {
        _online = HasInternet();
        NetworkInformation.NetworkStatusChanged += OnStatusChanged;
    }

    public void Dispose()
    {
        NetworkInformation.NetworkStatusChanged -= OnStatusChanged;
        _confirm.Dispose();
    }

    private void OnStatusChanged(object sender)
    {
        if (HasInternet())
        {
            _confirm.Change(Timeout.Infinite, Timeout.Infinite);
            Set(true);
        }
        else
        {
            _confirm.Change(OfflineAfter, Timeout.InfiniteTimeSpan);
        }
    }

    private void Set(bool online)
    {
        if (online == _online)
        {
            return;
        }

        _online = online;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A captive portal (hotel Wi-Fi sign-in) still counts as online; the browser handles that.</summary>
    private static bool HasInternet()
    {
        try
        {
            NetworkConnectivityLevel? level = NetworkInformation.GetInternetConnectionProfile()?.GetNetworkConnectivityLevel();
            return level is NetworkConnectivityLevel.InternetAccess or NetworkConnectivityLevel.ConstrainedInternetAccess;
        }
        catch (Exception)
        {
            // If Windows can't say, don't claim the PC is offline.
            return true;
        }
    }
}
