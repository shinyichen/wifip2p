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
using Android.Net.Nsd;
using System.Text;
using System.Net;

namespace wifiptp
{
    [Activity(Label = "wifiptp", MainLauncher = true, Icon = "@mipmap/icon")]
    public class MainActivity : Activity, ITaskCompleted, P2pServiceListener
	{

        private const string id = "Backpack-Main";

        private ServerService serverService;

        private NsdManager nsdManager;

        private List<NsdServiceInfo>devices = new List<NsdServiceInfo>();

		private Button searchButton;

        private ListView listView;

        protected ArrayAdapter adapter;

        private NsdDiscoveryListener nsdDiscoveryListener;

        private ServiceResolvedListener nsdServiceResolvedListener;

        private string myServiceName;

        //private DiscoveryCompleted discoveryCompletedCallback;

        private BroadcastReceiver p2pServiceBroadcastReceiver;

        private IntentFilter intentFilter;

        private ServiceConnection serviceConnection;

		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

            // layout
			SetContentView(Resource.Layout.Main);
			searchButton = FindViewById<Button>(Resource.Id.discoverButton);
			searchButton.Click += delegate {
				discover();
			};
            //discoveryCompletedCallback = new DiscoveryCompleted(() =>
            //{
            //	searchButton.Enabled = true;
            //});

            // discover -> resolve -> add device
            nsdServiceResolvedListener = new ServiceResolvedListener((NsdServiceInfo info) => {
                Log.Debug(id, "Service resolved: " + info.ServiceName);
                RunOnUiThread(() =>
                {
                    adapter.Add(new MyServiceInfo(info.ServiceName, info.Host, info.Port));
                });
            });
            nsdDiscoveryListener = new NsdDiscoveryListener(nsdManager, (NsdServiceInfo info) =>
            {
                // found new device -> resolve
                string serviceName = info.ServiceName;
                Log.Debug(id, "Resolve service: " + serviceName);
                // don't process duplicates
                if (!serviceName.Equals(myServiceName))
                {
					nsdManager.ResolveService(info, new ServiceResolvedListener((NsdServiceInfo info1) => {
						Log.Debug(id, "Service resolved: " + info1.ServiceName);
						RunOnUiThread(() =>
						{
                            adapter.Add(new MyServiceInfo(info1.ServiceName, info1.Host, info1.Port));
						});
					}));
                }
            }, (NsdServiceInfo info) => {
                // device lost, remove device
                foreach (NsdServiceInfo d in devices) {
                    if (d.ServiceName.Equals(info.ServiceName)) {
                        RunOnUiThread(() => {
							adapter.Remove(d);
                        });
                        break;
                    }
                }
            });

			adapter = new ArrayAdapter(this, Resource.Layout.ListItem, devices);
			adapter.SetNotifyOnChange(true);

			listView = FindViewById<ListView>(Resource.Id.deviceListView);
			listView.Adapter = adapter;

			listView.ItemClick += (sender, e) =>
			{
				int position = e.Position;
                NsdServiceInfo device = (NsdServiceInfo)adapter.GetItem(position);
				Log.Info(id, device.ToString());

                //WifiP2pConfig config = new WifiP2pConfig();
                //config.DeviceAddress = device.DeviceAddress;
                //config.GroupOwnerIntent = 0; // make myself least inclined to be owner, so I can connect to server
                //service.connect(config);

                // connect
                InetAddress host = device.Host;
                int port = device.Port;
                ClientAsyncTask task = new ClientAsyncTask(this, host, port, this);
                task.Execute();
			};



			// listen to broadcast
            p2pServiceBroadcastReceiver = new P2pServiceBroadcastReceiver(this);
			intentFilter = new IntentFilter();
            //intentFilter.AddAction(P2pService.DEVICES_CHANGED);
            //intentFilter.AddAction(P2pService.DISCOVERY_STARTED_ACTION);
            //intentFilter.AddAction(P2pService.DISCOVERY_COMPLETED_ACTION);
            //intentFilter.AddAction(P2pService.CONNECTION_ESTABLISHED_ACTION);
            //intentFilter.AddAction(P2pService.CONNECTION_CLOSED_ACTION);
            intentFilter.AddAction(ServerService.SERVICE_REGISTERED_ACTION);

            // start server service
            StartService(new Intent(this, typeof(ServerService)));

		}

		protected override void OnResume()
		{
			base.OnResume();
            RegisterReceiver(p2pServiceBroadcastReceiver, intentFilter);

            if (serverService == null) {
				// start P2pService if it hasn't been started
				serviceConnection = new ServiceConnection((IBinder service) =>
				{
					this.serverService = ((ServerServiceBinder)service).GetServerService();
                    nsdManager = serverService.NsdManager;
                    if (serverService.MyServiceInfo != null)
                        myServiceName = serverService.MyServiceInfo.ServiceName;
					Log.Info(id, "service connected");
                    Title = myServiceName;
					discover();
				}, () =>
				{
					this.serverService = null;
					Log.Info(id, "service disconnected");
				});

                // bind to server service
                BindService(new Intent(this, typeof(ServerService)), serviceConnection, Bind.AutoCreate);

            } else {
                discover();
            }


		}

		protected override void OnPause()
		{
            nsdManager.StopServiceDiscovery(nsdDiscoveryListener);
            UnregisterReceiver(p2pServiceBroadcastReceiver);
            UnbindService(serviceConnection);
			base.OnPause();
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

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }

        private void discover() {
            if (nsdManager != null) {
                nsdManager.DiscoverServices("_backpack._tcp", NsdProtocol.DnsSd, nsdDiscoveryListener);

			}
        }

        //public void OnDevicesChanged()
        //{
        //    // update array adapter
        //    adapter.Clear();
        //    adapter.AddAll(service.Devices);
        //}

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
            // disable UI when connection is established
            adapter.Clear();
            searchButton.Enabled = false;
        }

        public void OnConnectionClosed()
        {
            
        }

        public void OnTaskCompleted()
        {
            //discover();
        }

        public void OnDevicesChanged()
        {
            
        }

        public void OnServiceRegistered()
        {
            myServiceName = serverService.MyServiceInfo.ServiceName;
            Title = myServiceName;
            Log.Debug(id, "Service Registered: " + myServiceName);
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



		public class NsdDiscoveryListener : Java.Lang.Object, NsdManager.IDiscoveryListener
		{

			private NsdManager nsdManager;
			private Action<NsdServiceInfo> onServiceFoundAction;
            private Action<NsdServiceInfo> onServiceLostAction;

            public NsdDiscoveryListener(NsdManager nsdManager, Action<NsdServiceInfo> onServiceFoundAction, Action<NsdServiceInfo> onServiceLostAction)
			{
				this.nsdManager = nsdManager;
				this.onServiceFoundAction = onServiceFoundAction;
                this.onServiceLostAction = onServiceLostAction;
			}

			public void OnDiscoveryStarted(string serviceType)
			{
				Log.Debug(id, "Nsd Discovery Started");
			}

			public void OnDiscoveryStopped(string serviceType)
			{
				Log.Debug(id, "Nsd Discovery Stopped");
			}

			public void OnServiceFound(NsdServiceInfo serviceInfo)
			{
				Log.Debug(id, "Service Found: " + serviceInfo);
				onServiceFoundAction(serviceInfo);
			}

			public void OnServiceLost(NsdServiceInfo serviceInfo)
			{
				Log.Debug(id, "Service Lost: " + serviceInfo);
			}

			public void OnStartDiscoveryFailed(string serviceType, NsdFailure errorCode)
			{
				Log.Debug(id, "On Start Discovery Failed: " + errorCode.ToString());
				nsdManager.StopServiceDiscovery(this);
			}

			public void OnStopDiscoveryFailed(string serviceType, NsdFailure errorCode)
			{
				Log.Debug(id, "On Stop Discovery Failed: " + errorCode.ToString());
				nsdManager.StopServiceDiscovery(this);
			}
		}


		public class ServiceResolvedListener : Java.Lang.Object, NsdManager.IResolveListener
		{
			private Action<NsdServiceInfo> serviceResolvedAction;

			public ServiceResolvedListener(Action<NsdServiceInfo> serviceResolvedAction)
			{
				this.serviceResolvedAction = serviceResolvedAction;
			}
			public void OnResolveFailed(NsdServiceInfo serviceInfo, NsdFailure errorCode)
			{
				Log.Error(id, "Resolve Service Failed: " + errorCode.ToString());
			}

			public void OnServiceResolved(NsdServiceInfo serviceInfo)
			{
				Log.Debug(id, "Service Resolved: " + serviceInfo);
				serviceResolvedAction(serviceInfo);
			}
		}

        // start server and client tasks only when connection info is available
        // TODO we only want to start file transfer if connection was established by user manually






    }
}

