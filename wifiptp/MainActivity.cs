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
using Java.Net;
using Android.Net.Wifi.P2p.Nsd;
using System;

namespace wifiptp
{
    [Activity(Label = "wifiptp", MainLauncher = true, Icon = "@mipmap/icon")]
    public class MainActivity : Activity, P2pServiceListener
	{

        private const string id = "Backpack-Main";

        private P2pService service;

        private List<WifiP2pDevice>devices = new List<WifiP2pDevice>();

		private Button searchButton;

        private ListView listView;

        protected ArrayAdapter adapter;

        private DiscoveryCompleted discoveryCompletedCallback;

        private BroadcastReceiver p2pServiceBroadcastReceiver;

        private IntentFilter intentFilter;


		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

            // layout
			SetContentView(Resource.Layout.Main);
			searchButton = FindViewById<Button>(Resource.Id.discoverButton);
			searchButton.Click += delegate {
				discover();
			};
			discoveryCompletedCallback = new DiscoveryCompleted(() =>
			{
				searchButton.Enabled = true;
			});

			adapter = new ArrayAdapter(this, Resource.Layout.ListItem, devices);
			adapter.SetNotifyOnChange(true);

			listView = FindViewById<ListView>(Resource.Id.deviceListView);
			listView.Adapter = adapter;

			listView.ItemClick += (sender, e) =>
			{
				int position = e.Position;
				WifiP2pDevice device = (WifiP2pDevice)adapter.GetItem(position);
				Log.Info(id, device.ToString());

				WifiP2pConfig config = new WifiP2pConfig();
				config.DeviceAddress = device.DeviceAddress;
				//config.GroupOwnerIntent = 0; // make myself least inclined to be owner, so I can connect to server
				service.connect(config);
			};

            // start P2pService if it hasn't been started
            ServiceConnection serviceConnection = new ServiceConnection((IBinder service) =>
            {
                this.service = ((P2pServiceBinder)service).GetP2pService();
                Log.Info(id, "service connected");
            }, () =>
            {
                this.service = null;
                Log.Info(id, "service disconnected");
            });
            Intent intent = new Intent(this, typeof(P2pService));
            StartService(intent);

			// listen to broadcast
            p2pServiceBroadcastReceiver = new P2pServiceBroadcastReceiver(this);
			intentFilter = new IntentFilter();
            intentFilter.AddAction(P2pService.DEVICES_CHANGED);
            intentFilter.AddAction(P2pService.DISCOVERY_STARTED_ACTION);
            intentFilter.AddAction(P2pService.DISCOVERY_COMPLETED_ACTION);
            intentFilter.AddAction(P2pService.CONNECTION_ESTABLISHED_ACTION);
            intentFilter.AddAction(P2pService.CONNECTION_CLOSED_ACTION);
            BindService(intent, serviceConnection, Bind.AutoCreate);
		}

		protected override void OnResume()
		{
			base.OnResume();
            RegisterReceiver(p2pServiceBroadcastReceiver, intentFilter);
            if (service != null) 
                discover();
		}

		protected override void OnPause()
		{
			base.OnPause();
            UnregisterReceiver(p2pServiceBroadcastReceiver);
		}

        protected override void OnStop()
        {
            //if (wifiManager != null && channel != null)
            //{
            //    wifiManager.RemoveGroup(channel, new GroupRemovedListener(() => {
            //        Log.Info(id, "RemoveGroup successful");
            //    }, (string reason) => {
            //        Log.Info(id, "RemoveGroup failed: " + reason);
            //    }));
            //}
            base.OnStop();
        }

        private void discover() {
            if (service != null)
            {
                service.discover();
            }
        }

        public void OnDevicesChanged()
        {
            // update array adapter
            adapter.Clear();
            adapter.AddAll(service.Devices);
        }

        public void OnDiscoveryStarted()
        {
            searchButton.Enabled = false;
        }

        public void OnDiscoveryStopped()
        {
            searchButton.Enabled = true;
        }

        public void OnConnectionStarted()
        {
            adapter.Clear();
            searchButton.Enabled = false;
        }

        public void OnConnectionClosed()
        {
            
        }

        private class DiscoveryCompleted : Java.Lang.Object, ITaskCompleted
        {
            private readonly Action action;

            public DiscoveryCompleted(Action action) {
                this.action = action;
            }

            public void OnTaskCompleted()
            {
                action();
            }
        }


        private class ServiceConnection : Java.Lang.Object, IServiceConnection
        {

            private readonly Action<IBinder> connected;

            private readonly Action disconnected;

            public ServiceConnection(Action<IBinder> connected, Action disconnected) 
            {
                this.connected = connected;
                this.disconnected = disconnected;
            }

            public void OnServiceConnected(ComponentName name, IBinder service)
            {
                connected(service);
            }

            public void OnServiceDisconnected(ComponentName name)
            {
                disconnected();
            }
        }




		



        // start server and client tasks only when connection info is available
        // TODO we only want to start file transfer if connection was established by user manually
        // TODO handle if click on device and was already connected
        // TODO handle if click on device while another connection is running






    }
}

