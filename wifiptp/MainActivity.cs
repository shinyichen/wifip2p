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
    public class MainActivity : Activity//, IActionListener, IPeerListListener
	{

        private const string id = "Backpack-Main";

        /* my server socket */
        //private ServerSocket serverSocket;

        /* my port */
        public const int port = 45288;

		private WifiP2pManager wifiManager;
		private Channel channel;
		private BroadcastReceiver wifiBroadcastReceiver;

		private IntentFilter intentFilter;
        private List<WifiP2pDevice>devices = new List<WifiP2pDevice>();

		private Button searchButton;

        private ListView listView;
        private ArrayAdapter adapter;

        /* a list of devices found */
        //private WifiP2pDevice device;

        /* found device names and ports */
        //private Dictionary<string, int> devicePorts = new Dictionary<string, int>();

		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

			SetContentView(Resource.Layout.Main);
			searchButton = FindViewById<Button>(Resource.Id.discoverButton);
			searchButton.Click += delegate {
				//searchButton.Enabled = false;
				discover();
			};

            // initialize server socket and get an assigned port number
            //serverSocket = new ServerSocket(0); // get next available port
            //port = serverSocket.LocalPort;
            Dictionary<string, string> record = new Dictionary<string, string>();
            record.Add("port", port.ToString());
            WifiP2pDnsSdServiceInfo serviceInfo = WifiP2pDnsSdServiceInfo.NewInstance("_backpack", "_backpack._tcp", record);

            // TODO this should be in a service so it stays running
			wifiManager = (WifiP2pManager)GetSystemService(Context.WifiP2pService);
			channel = wifiManager.Initialize(this, MainLooper, null); //Registers the application with the Wi-Fi framework.
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
                wifiManager.Connect(channel, config, null);
            };



            discover();
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
            adapter.Clear();

            // discover peers
            //     if (wifiManager != null)
            //wifiManager.DiscoverPeers(channel, this);

            // set service response listeners
            wifiManager.SetDnsSdResponseListeners(channel, new ServiceResponseListener(adapter), new RecordAvailableListener(adapter));

            // add service discovery request
            WifiP2pDnsSdServiceRequest serviceRequest = WifiP2pDnsSdServiceRequest.NewInstance();
            wifiManager.AddServiceRequest(channel, serviceRequest, new AddServiceRequestListener());

            // discover service
            wifiManager.DiscoverServices(channel, new DiscoverServicesListener());
		}

		//// IActionListener: discover peers fail
		//public void OnFailure([GeneratedEnum] WifiP2pFailureReason reason)
		//{
  //          searchButton.Enabled = true;
  //          Toast.MakeText(this, "discover peers failed!", ToastLength.Short).Show();
		//}

		//// IActionListener: discover peers success
		//public void OnSuccess()
		//{
  //          Toast.MakeText(this, "discover peers successful, requesting peers", ToastLength.Short).Show();
  //          wifiManager.RequestPeers(channel, this);
		//}

        // IPeerListListener: peers found
        //public void OnPeersAvailable(WifiP2pDeviceList peers)
        //{

        //    // TODO filter only mobile devices with this app (service?)
        //    Toast.MakeText(this, "Found " + peers.DeviceList.Count + " peers", ToastLength.Short).Show();
        //    searchButton.Enabled = true;
        //    adapter.Clear();
        //    int c = 1;

        //    // show devices in list
        //    foreach (WifiP2pDevice d in peers.DeviceList.ToList())
        //    {
        //        adapter.Add(d);
        //        Log.Info(id, "Device " + c + ": " + d.DeviceAddress + " " + d.DeviceName + " " + d.PrimaryDeviceType);
        //        c++;
        //    }
        //}





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
                Log.Info(id, "Got port " + port + " for device: " + srcDevice.DeviceName);
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
			public void OnFailure([GeneratedEnum] WifiP2pFailureReason reason)
			{
                
				Log.Info(id, "DiscoverServices failed: " + reason.ToString());
			}

			public void OnSuccess()
			{
				Log.Info(id, "DiscoverServices successful");
			}
		}

        // start server and client tasks only when connection info is available
        // TODO we only want to start file transfer if connection was established by user manually
        // TODO handle if click on device and was already connected
        // TODO handle if click on device while another connection is running

        public class ConnectionInfoAvailableListener : Java.Lang.Object, IConnectionInfoListener {

            private Context context;

            private WifiP2pManager manager;

            private Channel channel;

            private int myPort;

            public ConnectionInfoAvailableListener(Context context, WifiP2pManager manager, Channel channel, int myPort) {
                this.context = context;
                this.manager = manager;
                this.channel = channel;
                this.myPort = myPort;
            }

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
                    FileServerAsyncTask task = new FileServerAsyncTask(context, manager, channel, myPort);
					task.Execute();
				}
				else
				{
					Log.Info(id, "connected as client");
					ClientAsyncTask task = new ClientAsyncTask(context, info.GroupOwnerAddress, manager, channel);
					task.Execute();
				}
			}
        }

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

