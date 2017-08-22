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
using System;

namespace wifiptp
{
    [Activity(Label = "wifiptp", MainLauncher = true, Icon = "@mipmap/icon")]
	public class MainActivity : Activity, IActionListener, IPeerListListener
	{

		private WifiP2pManager wifiManager;
		private Channel channel;
		private BroadcastReceiver wifiBroadcastReceiver;

		private IntentFilter intentFilter;
        private List<WifiP2pDevice>devices = new List<WifiP2pDevice>();

		private Button searchButton;

        private ListView listView;
        private ArrayAdapter adapter;

		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

			SetContentView(Resource.Layout.Main);
			searchButton = FindViewById<Button>(Resource.Id.discoverButton);
			searchButton.Click += delegate {
				searchButton.Enabled = false;
				discover();
			};

            // TODO this should be in a service so it stays running
			wifiManager = (WifiP2pManager)GetSystemService(Context.WifiP2pService);
			channel = wifiManager.Initialize(this, MainLooper, null); //Registers the application with the Wi-Fi framework.
			wifiBroadcastReceiver = new WiFiDirectBroadcastReceiver(wifiManager, channel, this);

			intentFilter = new IntentFilter();
			intentFilter.AddAction(WifiP2pManager.WifiP2pStateChangedAction);
			intentFilter.AddAction(WifiP2pManager.WifiP2pPeersChangedAction);
			intentFilter.AddAction(WifiP2pManager.WifiP2pConnectionChangedAction);
			intentFilter.AddAction(WifiP2pManager.WifiP2pThisDeviceChangedAction);

            adapter = new ArrayAdapter(this, Resource.Layout.ListItem, devices);
            adapter.SetNotifyOnChange(true);
            listView = FindViewById<ListView>(Resource.Id.deviceListView);
            listView.Adapter = adapter;

            listView.ItemClick += (sender, e) =>
            {
                int position = e.Position;
                WifiP2pDevice device = (WifiP2pDevice)adapter.GetItem(position);
                Log.Info("MainActivity", device.ToString());

                WifiP2pConfig config = new WifiP2pConfig();
                config.DeviceAddress = device.DeviceAddress;
                wifiManager.Connect(channel, config, new ConnectedListener());
            };
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
            if (wifiManager != null)
			    wifiManager.DiscoverPeers(channel, this);
		}

		// IActionListener: discover peers fail
		public void OnFailure([GeneratedEnum] WifiP2pFailureReason reason)
		{
            searchButton.Enabled = true;
            Toast.MakeText(this, "discover peers failed!", ToastLength.Short).Show();
		}

		// IActionListener: discover peers success
		public void OnSuccess()
		{
            Toast.MakeText(this, "discover peers successful, requesting peers", ToastLength.Short).Show();
            wifiManager.RequestPeers(channel, this);
		}

		// IPeerListListener: peers found
		public void OnPeersAvailable(WifiP2pDeviceList peers)
		{
            
            Toast.MakeText(this, "Found " + peers.DeviceList.Count + " peers", ToastLength.Short).Show();
            searchButton.Enabled = true;
            adapter.Clear();
            int c = 1;

			// TODO show devices in list
			foreach (WifiP2pDevice device in peers.DeviceList.ToList())
			{
                adapter.Add(device);
                Log.Info("WiFiActivity", "Device " + c + ": " + device.DeviceAddress + " " + device.DeviceName + " " + device.PrimaryDeviceType);
                c++;
			}
		}

        public class ConnectedListener : Java.Lang.Object, IActionListener
        {

            void IActionListener.OnFailure(WifiP2pFailureReason reason)
            {
                Log.Info("WifiActivity", "connection failed: " + reason.ToString());
            }

            void IActionListener.OnSuccess()
            {
                Log.Info("WifiActivity", "connection established");
            }
        }
    }
}

