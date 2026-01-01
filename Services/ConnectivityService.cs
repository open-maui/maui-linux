using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Maui.Networking;

namespace Microsoft.Maui.Platform.Linux.Services;

public class ConnectivityService : IConnectivity, IDisposable
{
    private static readonly Lazy<ConnectivityService> _instance = new Lazy<ConnectivityService>(() => new ConnectivityService());

    private NetworkAccess _networkAccess;

    private IEnumerable<ConnectionProfile> _connectionProfiles;

    private bool _disposed;

    public static ConnectivityService Instance => _instance.Value;

    public NetworkAccess NetworkAccess
    {
        get
        {
            RefreshConnectivity();
            return _networkAccess;
        }
    }

    public IEnumerable<ConnectionProfile> ConnectionProfiles
    {
        get
        {
            RefreshConnectivity();
            return _connectionProfiles;
        }
    }

    public event EventHandler<ConnectivityChangedEventArgs>? ConnectivityChanged;

    public ConnectivityService()
    {
        _connectionProfiles = new List<ConnectionProfile>();
        RefreshConnectivity();
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
    }

    private void RefreshConnectivity()
    {
        try
        {
            IEnumerable<NetworkInterface> activeInterfaces = from ni in NetworkInterface.GetAllNetworkInterfaces()
                where ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                select ni;

            if (!activeInterfaces.Any())
            {
                _networkAccess = NetworkAccess.None;
                _connectionProfiles = Enumerable.Empty<ConnectionProfile>();
                return;
            }

            List<ConnectionProfile> profiles = new List<ConnectionProfile>();
            foreach (var networkInterface in activeInterfaces)
            {
                switch (networkInterface.NetworkInterfaceType)
                {
                    case NetworkInterfaceType.Ethernet:
                    case NetworkInterfaceType.FastEthernetT:
                    case NetworkInterfaceType.FastEthernetFx:
                    case NetworkInterfaceType.GigabitEthernet:
                        profiles.Add(ConnectionProfile.Ethernet);
                        break;
                    case NetworkInterfaceType.Wireless80211:
                        profiles.Add(ConnectionProfile.WiFi);
                        break;
                    case NetworkInterfaceType.Ppp:
                    case NetworkInterfaceType.Slip:
                        profiles.Add(ConnectionProfile.Cellular);
                        break;
                    default:
                        profiles.Add(ConnectionProfile.Unknown);
                        break;
                }
            }

            _connectionProfiles = profiles.Distinct().ToList();

            if (CheckInternetAccess())
            {
                _networkAccess = NetworkAccess.Internet;
            }
            else if (_connectionProfiles.Any())
            {
                _networkAccess = NetworkAccess.Local;
            }
            else
            {
                _networkAccess = NetworkAccess.None;
            }
        }
        catch
        {
            _networkAccess = NetworkAccess.Unknown;
            _connectionProfiles = new ConnectionProfile[] { ConnectionProfile.Unknown };
        }
    }

    private bool CheckInternetAccess()
    {
        try
        {
            return Dns.GetHostEntry("dns.google").AddressList.Length != 0;
        }
        catch
        {
            try
            {
                foreach (NetworkInterface item in from n in NetworkInterface.GetAllNetworkInterfaces()
                    where n.OperationalStatus == OperationalStatus.Up
                    select n)
                {
                    if (item.GetIPProperties().GatewayAddresses.Any((GatewayIPAddressInformation g) => g.Address.AddressFamily == AddressFamily.InterNetwork))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }
            return false;
        }
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        NetworkAccess previousAccess = _networkAccess;
        List<ConnectionProfile> previousProfiles = _connectionProfiles.ToList();
        RefreshConnectivity();
        if (previousAccess != _networkAccess || !previousProfiles.SequenceEqual(_connectionProfiles))
        {
            ConnectivityChanged?.Invoke(this, new ConnectivityChangedEventArgs(_networkAccess, _connectionProfiles));
        }
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        NetworkAccess previousAccess = _networkAccess;
        List<ConnectionProfile> previousProfiles = _connectionProfiles.ToList();
        RefreshConnectivity();
        if (previousAccess != _networkAccess || !previousProfiles.SequenceEqual(_connectionProfiles))
        {
            ConnectivityChanged?.Invoke(this, new ConnectivityChangedEventArgs(_networkAccess, _connectionProfiles));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
            NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        }
    }
}
