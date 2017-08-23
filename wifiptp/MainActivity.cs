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
using Java.Net;
using System.IO;

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

        private WifiP2pDevice device;

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
                device = (WifiP2pDevice)adapter.GetItem(position);
                Log.Info("MainActivity", device.ToString());

                WifiP2pConfig config = new WifiP2pConfig();
                config.DeviceAddress = device.DeviceAddress;
                config.GroupOwnerIntent = 15;
                wifiManager.Connect(channel, config, null);
                //wifiManager.StopPeerDiscovery(channel, new DiscoveryCanceledListener(this, wifiManager, device, channel));
                //wifiManager.CreateGroup(channel, new GroupCreatedListener(this, wifiManager, device, channel));
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
			foreach (WifiP2pDevice d in peers.DeviceList.ToList())
			{
                adapter.Add(d);
                Log.Info("WiFiActivity", "Device " + c + ": " + d.DeviceAddress + " " + d.DeviceName + " " + d.PrimaryDeviceType);
                c++;
			}
		}

        /* dicovery canceled to start group creation */
        public class DiscoveryCanceledListener : Java.Lang.Object, IActionListener
        {
			private Context context;

			private WifiP2pManager manager;

			private WifiP2pDevice device; // device the group creator wants to to connect to

			private Channel channel;

            public DiscoveryCanceledListener(Context context, WifiP2pManager manager, WifiP2pDevice device, Channel channel)
            {
                this.context = context;
                this.manager = manager;
                this.device = device;
                this.channel = channel;
            }

            public void OnFailure([GeneratedEnum] WifiP2pFailureReason reason)
            {
                Log.Info("Main", "Discovery cancel failed: " + reason.ToString());
            }

            public void OnSuccess()
            {
                Log.Info("Main", "Discovery successfully canceled");
				manager.CreateGroup(channel, new GroupCreatedListener(context, manager, device, channel));
            }
        }

        /* Group created, establish connection */
        public class GroupCreatedListener : Java.Lang.Object, IActionListener
		{

            private Context context;

			private WifiP2pManager manager;

			private WifiP2pDevice device; // device the group creator wants to to connect to

            private Channel channel;

            public GroupCreatedListener(Context context, WifiP2pManager manager, WifiP2pDevice device, Channel channel)
			{
                this.context = context;
				this.manager = manager;
				this.device = device;
                this.channel = channel;
			}
			public void OnFailure([GeneratedEnum] WifiP2pFailureReason reason)
			{
				Log.Info("Main", "Create group failed: " + reason.ToString());
			}

			public void OnSuccess()
			{
				Log.Info("Main", "Group Created, now establishing connection to device " + device.DeviceName);
				WifiP2pConfig config = new WifiP2pConfig();
				config.DeviceAddress = device.DeviceAddress;
                manager.Connect(channel, config, new ConnectedListener(context, manager, channel));

			}
		}

        /* connection started by this device has been established */
        public class ConnectedListener : Java.Lang.Object, IActionListener
        {

            private Context context;

            private WifiP2pManager manager;

            private Channel channel;

            public ConnectedListener(Context context, WifiP2pManager manager, Channel channel) {
                this.context = context;
                this.manager = manager;
                this.channel = channel;    
            }
            void IActionListener.OnFailure(WifiP2pFailureReason reason)
            {
                Log.Info("Main", "connection failed: " + reason.ToString());
            }

            void IActionListener.OnSuccess()
            {
                // I made a connection with another device
                Log.Info("Main", "connection established, requesting connection info");
                manager.RequestConnectionInfo(channel, new ConnectionInfoAvailableListener(context, manager, channel));
            }
        }

        public class ConnectionInfoAvailableListener : Java.Lang.Object, IConnectionInfoListener {

            private Context context;

            private WifiP2pManager manager;

            private Channel channel;

            public ConnectionInfoAvailableListener(Context context, WifiP2pManager manager, Channel channel) {
                this.context = context;
                this.manager = manager;
                this.channel = channel;
            }

			public void OnConnectionInfoAvailable(WifiP2pInfo info)
			{
				if (info.GroupFormed)
				{
                    Log.Info("Main", "Group owner: " + info.GroupOwnerAddress.HostAddress);
					Log.Info("Main", "Is Group owner: " + info.IsGroupOwner);
				}
				else
				{
					Log.Info("Main", "No group");
				}

				if (info.IsGroupOwner)
				{
					Log.Info("WifiDirectBroadcastReceiver", "connected as server");
                    FileServerAsyncTask task = new FileServerAsyncTask(context, manager, channel);
					task.Execute();
				}
				else
				{
					Log.Info("WifiDirectBroadcastReceiver", "connected as client");
					ClientAsyncTask task = new ClientAsyncTask(context, info.GroupOwnerAddress, manager, channel);
					task.Execute();
				}
			}
        }

        public class GroupRemovedListener : Java.Lang.Object, IActionListener
        {
            public void OnFailure([GeneratedEnum] WifiP2pFailureReason reason)
            {
                Log.Info("Main", "Remove group failed");
            }

            public void OnSuccess()
            {
                Log.Info("Main", "Group removed");
            }
        }



    }
}

