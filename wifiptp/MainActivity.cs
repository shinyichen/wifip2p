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
    public class MainActivity : Activity, IConnectionInfoListener, ITaskCompleted
	{

        private const string id = "Backpack-Main";

        /* my port */
        public const int port = 45288;

		private WifiP2pManager wifiManager;
		private Channel channel;
		private BroadcastReceiver wifiBroadcastReceiver;

		private IntentFilter intentFilter;
        private List<WifiP2pDevice>devices = new List<WifiP2pDevice>();

		private Button searchButton;

        private ListView listView;
        protected ArrayAdapter adapter;


		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

			SetContentView(Resource.Layout.Main);
			searchButton = FindViewById<Button>(Resource.Id.discoverButton);
			searchButton.Click += delegate {
				discover();
			};

            Dictionary<string, string> record = new Dictionary<string, string>();
            record.Add("port", port.ToString());
            WifiP2pDnsSdServiceInfo serviceInfo = WifiP2pDnsSdServiceInfo.NewInstance("_backpack", "_backpack._tcp", record);

            // TODO this should be in a service so it stays running
			wifiManager = (WifiP2pManager)GetSystemService(Context.WifiP2pService);
			channel = wifiManager.Initialize(this, MainLooper, null); //Registers the application with the Wi-Fi framework.
            wifiManager.ClearLocalServices(channel, null);
            wifiManager.AddLocalService(channel, serviceInfo, new ServiceAddedListener());


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
                Log.Info(id, device.ToString());

                WifiP2pConfig config = new WifiP2pConfig();
                config.DeviceAddress = device.DeviceAddress;
                config.GroupOwnerIntent = 15;
                wifiManager.Connect(channel, config, new ConnectListener());
            };



            discover();
		}

		protected override void OnResume()
		{
			base.OnResume();
			RegisterReceiver(wifiBroadcastReceiver, intentFilter);

            // TODO need to call discover?
		}

		protected override void OnPause()
		{
			base.OnPause();
			UnregisterReceiver(wifiBroadcastReceiver);
		}

        protected override void OnStop()
        {
            if (wifiManager != null && channel != null)
            {
                wifiManager.RemoveGroup(channel, new MainActivity.GroupRemovedListener());
            }
            base.OnStop();
        }

        private WifiP2pDnsSdServiceRequest serviceRequest;

		private void discover()
		{

			searchButton.Enabled = false;

			if (serviceRequest != null)
                wifiManager.RemoveServiceRequest(channel, serviceRequest, null);
            
            adapter.Clear();

            // set service response listeners
            wifiManager.SetDnsSdResponseListeners(channel, new ServiceResponseListener(adapter), new RecordAvailableListener(adapter));

            // add service discovery request
            serviceRequest = WifiP2pDnsSdServiceRequest.NewInstance();
            wifiManager.AddServiceRequest(channel, serviceRequest, new AddServiceRequestListener());

            // discover service
            wifiManager.DiscoverServices(channel, new DiscoverServicesListener(searchButton));
		}

        // this callback is called when connection is made and connection info is available
        public void OnConnectionInfoAvailable(WifiP2pInfo info)
        {
			if (info.GroupFormed)
			{
				Log.Info(id, "Group owner: " + info.GroupOwnerAddress.HostAddress);
				Log.Info(id, "Is Group owner: " + info.IsGroupOwner);
			}
			else
			{
				Log.Info(id, "No group");
			}

			if (info.IsGroupOwner)
			{
				Log.Info(id, "connected as server");
				adapter.Clear(); // disallow any more connection
                FileServerAsyncTask task = new FileServerAsyncTask(this, wifiManager, channel, port, this);
				task.Execute();
			}
			else
			{
				Log.Info(id, "connected as client");
				adapter.Clear(); // disallow any more connection
                ClientAsyncTask task = new ClientAsyncTask(this, info.GroupOwnerAddress, wifiManager, channel, this);
				task.Execute();
			}
        }

        // ITaskCompleted: callback for Client and Server tasks
        public void OnTaskCompleted()
        {
            // restart discovery
            discover();
        }

        public class ServiceAddedListener : Java.Lang.Object, IActionListener
        {
            public void OnFailure([GeneratedEnum] WifiP2pFailureReason reason)
            {
                Log.Info(id, "Add Local Service failed: " + reason.ToString());
            }

            public void OnSuccess()
            {
                Log.Info(id, "Local Service added");
            }
        }

        public class RecordAvailableListener : Java.Lang.Object, IDnsSdTxtRecordListener
        {

            private ArrayAdapter adapter;

            public RecordAvailableListener(ArrayAdapter adapter) {
                this.adapter = adapter;
            }

            public void OnDnsSdTxtRecordAvailable(string fullDomainName, IDictionary<string, string> txtRecordMap, WifiP2pDevice srcDevice)
            {
                string deviceName = srcDevice.DeviceName;
                int port = int.Parse(txtRecordMap["port"]);
                //devicePorts.Add(srcDevice.DeviceName, port);
                Log.Info(id, "Got device port: " + srcDevice.DeviceName + ":" + port);
            }
        }

        public class ServiceResponseListener : Java.Lang.Object, IDnsSdServiceResponseListener
        {
			private ArrayAdapter adapter;

			public ServiceResponseListener(ArrayAdapter adapter)
			{
				this.adapter = adapter;
			}

            public void OnDnsSdServiceAvailable(string instanceName, string registrationType, WifiP2pDevice srcDevice)
            {
                adapter.Add(srcDevice);
                Log.Info(id, "Device found: " + srcDevice.DeviceAddress + " " + srcDevice.DeviceName + " " + srcDevice.PrimaryDeviceType);
            }
        }

        public class AddServiceRequestListener : Java.Lang.Object, IActionListener
        {
            public void OnFailure([GeneratedEnum] WifiP2pFailureReason reason)
            {
                Log.Info(id, "AddServiceRequest failed: " + reason.ToString());
            }

            public void OnSuccess()
            {
                Log.Info(id, "AddServiceRequest successful");
            }
        }

		public class DiscoverServicesListener : Java.Lang.Object, IActionListener
		{
            private Button searchButton;

            public DiscoverServicesListener(Button searchButton) {
                this.searchButton = searchButton;   
            }

			public void OnFailure([GeneratedEnum] WifiP2pFailureReason reason)
			{
                searchButton.Enabled = true;
				Log.Info(id, "DiscoverServices failed: " + reason.ToString());
			}

			public void OnSuccess()
			{
                searchButton.Enabled = true;
				Log.Info(id, "DiscoverServices successful");
			}
		}

        public class ConnectListener : Java.Lang.Object, IActionListener
        {
            public void OnFailure([GeneratedEnum] WifiP2pFailureReason reason)
            {
                Log.Info(id, "Connection failed: " + reason.ToString());
            }

            public void OnSuccess()
            {
                Log.Info(id, "Connection success");
            }
        }

        // start server and client tasks only when connection info is available
        // TODO we only want to start file transfer if connection was established by user manually
        // TODO handle if click on device and was already connected
        // TODO handle if click on device while another connection is running


        public class GroupRemovedListener : Java.Lang.Object, IActionListener
        {
            public void OnFailure([GeneratedEnum] WifiP2pFailureReason reason)
            {
                Log.Info(id, "Remove group failed");
            }

            public void OnSuccess()
            {
                Log.Info(id, "Group removed");
            }
        }



    }
}

