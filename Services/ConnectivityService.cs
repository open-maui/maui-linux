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
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
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
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			IEnumerable<NetworkInterface> enumerable = from ni in NetworkInterface.GetAllNetworkInterfaces()
				where ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
				select ni;
			if (!enumerable.Any())
			{
				_networkAccess = (NetworkAccess)1;
				_connectionProfiles = Enumerable.Empty<ConnectionProfile>();
				return;
			}
			List<ConnectionProfile> list = new List<ConnectionProfile>();
			using (IEnumerator<NetworkInterface> enumerator = enumerable.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					switch (enumerator.Current.NetworkInterfaceType)
					{
					case NetworkInterfaceType.Ethernet:
					case NetworkInterfaceType.FastEthernetT:
					case NetworkInterfaceType.FastEthernetFx:
					case NetworkInterfaceType.GigabitEthernet:
						list.Add((ConnectionProfile)3);
						break;
					case NetworkInterfaceType.Wireless80211:
						list.Add((ConnectionProfile)4);
						break;
					case NetworkInterfaceType.Ppp:
					case NetworkInterfaceType.Slip:
						list.Add((ConnectionProfile)2);
						break;
					default:
						list.Add((ConnectionProfile)0);
						break;
					}
				}
			}
			_connectionProfiles = list.Distinct().ToList();
			_networkAccess = (NetworkAccess)(CheckInternetAccess() ? 4 : ((!_connectionProfiles.Any()) ? 1 : 2));
		}
		catch
		{
			_networkAccess = (NetworkAccess)0;
			_connectionProfiles = (IEnumerable<ConnectionProfile>)(object)new ConnectionProfile[1];
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
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Expected O, but got Unknown
		NetworkAccess networkAccess = _networkAccess;
		List<ConnectionProfile> first = _connectionProfiles.ToList();
		RefreshConnectivity();
		if (networkAccess != _networkAccess || !first.SequenceEqual(_connectionProfiles))
		{
			this.ConnectivityChanged?.Invoke(this, new ConnectivityChangedEventArgs(_networkAccess, _connectionProfiles));
		}
	}

	private void OnNetworkAddressChanged(object? sender, EventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Expected O, but got Unknown
		NetworkAccess networkAccess = _networkAccess;
		List<ConnectionProfile> first = _connectionProfiles.ToList();
		RefreshConnectivity();
		if (networkAccess != _networkAccess || !first.SequenceEqual(_connectionProfiles))
		{
			this.ConnectivityChanged?.Invoke(this, new ConnectivityChangedEventArgs(_networkAccess, _connectionProfiles));
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
