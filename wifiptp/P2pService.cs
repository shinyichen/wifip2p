
using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Net.Wifi.P2p;
using Android.Net.Wifi.P2p.Nsd;
using Android.OS;
using Android.Util;
using static Android.Net.Wifi.P2p.WifiP2pManager;

namespace wifiptp
{
    [Service(Label = "P2pService")]
    [IntentFilter(new String[] { "com.yourname.P2pService" })]

    // service should run in backgroun and not stop when app stops
    // TODO service should start when device starts

    public class P2pService : IntentService, IConnectionInfoListener, ITaskCompleted, IChannelListener
    {

        private const string id = "P2pService";

        IBinder binder;

		private WifiP2pManager wifiManager;
        public WifiP2pManager WifiManager {
            get {
                return wifiManager;
            }
        }

		private Channel channel;

		public const int port = 45288;
		
        private BroadcastReceiver wifiBroadcastReceiver;

        private IntentFilter intentFilter;

        private List<WifiP2pDevice> devices = new List<WifiP2pDevice>();

        public override StartCommandResult OnStartCommand(Android.Content.Intent intent, StartCommandFlags flags, int startId)
        {

            Log.Info(id, "Starting service");
			// initialize local service
			Dictionary<string, string> record = new Dictionary<string, string>();
			record.Add("port", port.ToString());
			WifiP2pDnsSdServiceInfo serviceInfo = WifiP2pDnsSdServiceInfo.NewInstance("_backpack", "_backpack._tcp", record);

			// add backpack service
			wifiManager = (WifiP2pManager)GetSystemService(Context.WifiP2pService);
			channel = wifiManager.Initialize(this, Looper.MyLooper(), null); //Registers the application with the Wi-Fi framework.
			Log.Info(id, "Channel: " + channel);
			wifiManager.ClearLocalServices(channel, null);
			wifiManager.AddLocalService(channel, serviceInfo, new ServiceAddedListener());

			// Listen to wifi direct broadcasts
			wifiBroadcastReceiver = new WiFiDirectBroadcastReceiver(wifiManager, channel, this);
			intentFilter = new IntentFilter();
			intentFilter.AddAction(WifiP2pManager.WifiP2pStateChangedAction);
			intentFilter.AddAction(WifiP2pManager.WifiP2pPeersChangedAction);
			intentFilter.AddAction(WifiP2pManager.WifiP2pConnectionChangedAction);
			intentFilter.AddAction(WifiP2pManager.WifiP2pThisDeviceChangedAction);
			RegisterReceiver(wifiBroadcastReceiver, intentFilter);

            // constant running service
            return StartCommandResult.Sticky;
        }

		protected override void OnHandleIntent(Intent intent)
		{
			
		}

        public override IBinder OnBind(Intent intent)
        {
            binder = new P2pServiceBinder(this);
            return binder;
        }


        public void discover()
		{
            devices.Clear();
            // TODO broadcast
            Log.Info(id, "device cleared");

			// set service response listeners
            Log.Info(id, "channel: " + channel.ToString());
			wifiManager.SetDnsSdResponseListeners(channel, new ServiceResponseListener(devices), new RecordAvailableListener());

            Log.Info(id, "SetDnsSdResponseListeners");

			wifiManager.ClearServiceRequests(channel, new ClearServiceRequestListener(() => {

				// clear service request successful
				Log.Info(id, "ClearServiceRequests successful");

				// add service discovery request
				WifiP2pDnsSdServiceRequest serviceRequest = WifiP2pDnsSdServiceRequest.NewInstance();
				wifiManager.AddServiceRequest(channel, serviceRequest, new AddServiceRequestListener(() => {

					// add service request successful
					Log.Info(id, "AddServiceRequest successful");

					// discover service
					wifiManager.DiscoverServices(channel, new DiscoverServicesListener(() => {
                        // discovery successful
                        // TODO broadcast 
						Log.Info(id, "DiscoverServices successful");

					}, (string reason) => {
						// discovery failed
						// TODO broadcast
						Log.Info(id, "DiscoverServices failed: " + reason.ToString());
					}));

				}, (string reason) => {
					// add service request failed
					Log.Info(id, "AddServiceRequest failed: " + reason.ToString());
				}));
			}, (string reason) => {
				// service request cleared
				Log.Info(id, "ClearServiceRequests failed: " + reason.ToString());
			}));

		}

        public void connect(WifiP2pConfig config) {
            wifiManager.Connect(channel, config, new ConnectListener());
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
				devices.Clear(); // disallow any more connection, if main is active
                // TODO braodcast
				FileServerAsyncTask task = new FileServerAsyncTask(this, wifiManager, channel, port, this);
				task.Execute();
			}
			else
			{
				Log.Info(id, "connected as client");
				devices.Clear(); // disallow any more connection, if main is active
                // TODO broadcast
				ClientAsyncTask task = new ClientAsyncTask(this, info.GroupOwnerAddress, wifiManager, channel, this);
				task.Execute();
			}
		}

		// ITaskCompleted: callback for Client and Server tasks
		public void OnTaskCompleted()
		{

			// remove from group (disconnect)
			Log.Info(id, "Removing group");
			wifiManager.RemoveGroup(channel, new GroupRemovedListener(() => {
				Log.Info(id, "RemoveGroup successful, discovery service");
				// restart discovery
				discover();
			}, (string reason) => {
				Log.Info(id, "RemoveGroup failed: " + reason);
				discover();
			}));


		}

        public void OnChannelDisconnected()
        {
            Log.Info(id, "Channel disconnected");
        }

        private class ServiceAddedListener : Java.Lang.Object, IActionListener
		{
			public void OnFailure(WifiP2pFailureReason reason)
			{
				Log.Info(id, "Add Local Service failed: " + reason.ToString());
			}

			public void OnSuccess()
			{
				Log.Info(id, "Local Service added");
			}
		}

		public class ServiceResponseListener : Java.Lang.Object, IDnsSdServiceResponseListener
		{

            private List<WifiP2pDevice> devices;

            public ServiceResponseListener(List<WifiP2pDevice> devices)
			{
				this.devices = devices;
			}

			public void OnDnsSdServiceAvailable(string instanceName, string registrationType, WifiP2pDevice srcDevice)
			{
				devices.Add(srcDevice);
                // TODO tell main activity if it exists, broadcast?
				Log.Info(id, "Device found: " + srcDevice.DeviceAddress + " " + srcDevice.DeviceName + " " + srcDevice.PrimaryDeviceType);
			}
		}

		public class RecordAvailableListener : Java.Lang.Object, IDnsSdTxtRecordListener
		{


			public void OnDnsSdTxtRecordAvailable(string fullDomainName, IDictionary<string, string> txtRecordMap, WifiP2pDevice srcDevice)
			{
				string deviceName = srcDevice.DeviceName;
				int port = int.Parse(txtRecordMap["port"]);
				Log.Info(id, "Got device port: " + srcDevice.DeviceName + ":" + port);
			}
		}

		// responds to manager.ClearServiceRequests
		public class ClearServiceRequestListener : Java.Lang.Object, IActionListener
		{
			private readonly Action success;

			private readonly Action<string> failure;

			public ClearServiceRequestListener(Action success, Action<string> failure)
			{
				this.success = success;
				this.failure = failure;
			}

			public void OnFailure(WifiP2pFailureReason reason)
			{
				failure(reason.ToString());
			}

			public void OnSuccess()
			{
				success();
			}
		}

		public class AddServiceRequestListener : Java.Lang.Object, IActionListener
		{

			private readonly Action success;

			private readonly Action<string> failure;

			public AddServiceRequestListener(Action success, Action<string> failure)
			{
				this.success = success;
				this.failure = failure;
			}

			public void OnFailure(WifiP2pFailureReason reason)
			{
				failure(reason.ToString());
			}

			public void OnSuccess()
			{
				success();
			}
		}

		public class DiscoverServicesListener : Java.Lang.Object, IActionListener
		{
			private readonly Action success;

			private readonly Action<string> failure;

			public DiscoverServicesListener(Action success, Action<string> failure)
			{
				this.success = success;
				this.failure = failure;
			}

			public void OnFailure(WifiP2pFailureReason reason)
			{
				failure(reason.ToString());
			}

			public void OnSuccess()
			{
				success();
			}
		}

		public class ConnectListener : Java.Lang.Object, IActionListener
		{
			public void OnFailure(WifiP2pFailureReason reason)
			{
				Log.Info(id, "Connection failed: " + reason.ToString());
			}

			public void OnSuccess()
			{
				Log.Info(id, "Connection success");
			}
		}

		public class GroupRemovedListener : Java.Lang.Object, IActionListener
		{

			private readonly Action success;

			private readonly Action<string> failure;

			public GroupRemovedListener(Action success, Action<string> failure)
			{
				this.success = success;
				this.failure = failure;
			}

			public void OnFailure(WifiP2pFailureReason reason)
			{
				failure(reason.ToString());
			}

			public void OnSuccess()
			{
				success();
			}
		}

    }

    public class P2pServiceBinder : Binder
    {
        readonly P2pService service;

        public P2pServiceBinder(P2pService service)
        {
            this.service = service;
        }

        public P2pService GetP2pService()
        {
            return service;
        }
    }

}
