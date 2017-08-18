using Android.App;
using Android.Widget;
using Android.OS;
using static Android.Net.Wifi.P2p.WifiP2pManager;
using Android.Net.Wifi.P2p;
using Android.Content;
using System.Collections.Generic;
using Android.Runtime;
using System.Linq;
using Android.Util;

namespace wifiptp
{
    [Activity(Label = "wifiptp", MainLauncher = true, Icon = "@mipmap/icon")]
	public class MainActivity : Activity, IActionListener, IPeerListListener
	{

		private WifiP2pManager wifiManager;
		private Channel channel;
		private BroadcastReceiver wifiBroadcastReceiver;

		private IntentFilter intentFilter;
		private List<WifiP2pDevice> devices;

		private Button searchButton;

		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

			SetContentView(Resource.Layout.Main);
			searchButton = FindViewById<Button>(Resource.Id.discoverButton);
			searchButton.Click += delegate {
				searchButton.Enabled = false;
				discover();
			};

			wifiManager = (WifiP2pManager)GetSystemService(Context.WifiP2pService);
			channel = wifiManager.Initialize(this, MainLooper, null); //Registers the application with the Wi-Fi framework.
			wifiBroadcastReceiver = new WiFiDirectBroadcastReceiver(wifiManager, channel, this);

			intentFilter = new IntentFilter();
			intentFilter.AddAction(WifiP2pManager.WifiP2pStateChangedAction);
			intentFilter.AddAction(WifiP2pManager.WifiP2pPeersChangedAction);
			intentFilter.AddAction(WifiP2pManager.WifiP2pConnectionChangedAction);
			intentFilter.AddAction(WifiP2pManager.WifiP2pThisDeviceChangedAction);
		}

		protected override void OnResume()
		{
			base.OnResume();
			RegisterReceiver(wifiBroadcastReceiver, intentFilter);
		}

		protected override void OnPause()
		{
			base.OnPause();
			UnregisterReceiver(wifiBroadcastReceiver);
		}

		private void discover()
		{
			// discover peers
			wifiManager.DiscoverPeers(channel, this);
		}

		// IActionListener: discover peers fail
		public void OnFailure([GeneratedEnum] WifiP2pFailureReason reason)
		{

		}

		// IActionListener: discover peers success
		public void OnSuccess()
		{

		}

		// IPeerListListener: peers found
		public void OnPeersAvailable(WifiP2pDeviceList peers)
		{
			searchButton.Enabled = true;
			devices = peers.DeviceList.ToList();
            int c = 0;

			// TODO show devices in list
			foreach (WifiP2pDevice device in devices)
			{
                Log.Info("WiFiActivity", "Device " + c + ": " + device.DeviceAddress + " " + device.DeviceName + " " + device.PrimaryDeviceType);
                c++;
			}
		}
	}
}

