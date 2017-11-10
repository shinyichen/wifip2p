using Android.App;
using Android.Widget;
using Android.OS;
using Android.Content;
using System.Collections.Generic;
using Android.Util;
using Java.Net;
using System;
using Android.Net.Nsd;
using static Android.App.ActivityManager;
using Java.Lang;

namespace wifiptp
{
    [Activity(Label = "wifiptp", MainLauncher = true, Icon = "@mipmap/icon")]
    public class MainActivity : Activity, ITaskCompleted, NsdServiceListener
	{

        private const string id = "Backpack-Main";

        private ServerService serverService;

        private NsdManager nsdManager;

        private List<string> foundServices = new List<string>();

        private ListView listView;

        protected ArrayAdapter adapter;

        private NsdDiscoveryListener nsdDiscoveryListener;

        private string myServiceName;

        private BroadcastReceiver p2pServiceBroadcastReceiver;

        private IntentFilter intentFilter;

        private ServiceConnection serviceConnection;

        private bool serviceBound = false;

        private bool discovering = false;

     
		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

            // layout
			SetContentView(Resource.Layout.Main);
            Title = "Service Unregistered";

            // discover -> resolve -> add device

            nsdDiscoveryListener = new NsdDiscoveryListener(nsdManager, () => {
                // discovery started
                discovering = true;
            }, () => {
                // discovery stopped
                discovering = false;
            }, (NsdServiceInfo info) =>
            {
                // found new device -> resolve
                string serviceName = info.ServiceName;

                // don't process duplicates
                if (!serviceName.Equals(myServiceName))
                {
                    resolveService(info);
                }
            }, (NsdServiceInfo info) => {
                // device lost, remove device
                for (int i = 0; i < adapter.Count; i++) {
                    MyServiceInfo d = (MyServiceInfo)adapter.GetItem(i);
                    if (d.ServiceName.Equals(info.ServiceName)) {
                        RunOnUiThread(() => {
                            foundServices.Remove(d.ToString());
                            adapter.Remove(d);
                        });
                        break;
                    }
                }
            });


			adapter = new ArrayAdapter(this, Resource.Layout.ListItem);
			adapter.SetNotifyOnChange(true);

			listView = FindViewById<ListView>(Resource.Id.deviceListView);
			listView.Adapter = adapter;

			listView.ItemClick += (sender, e) =>
			{
				int position = e.Position;
                MyServiceInfo device = (MyServiceInfo)adapter.GetItem(position);

                // connect
                InetAddress host = device.Host;
                int port = device.Port;
                ClientAsyncTask task = new ClientAsyncTask(this, host, port, this);
                task.Execute();
			};



			// listen to broadcast
            p2pServiceBroadcastReceiver = new NsdServiceBroadcastReceiver(this);
			intentFilter = new IntentFilter();
            intentFilter.AddAction(ServerService.SERVICE_REGISTERED_ACTION);

            // start server service
            StartService(new Intent(this, typeof(ServerService)));

            // bind to service if service is already running
            serviceConnection = new ServiceConnection((IBinder service) =>
            {
                this.serverService = ((ServerServiceBinder)service).GetServerService();
                nsdManager = serverService.NsdManager;
                Log.Info(id, "service connected");
                if (serverService.ServiceRegistered)
                {
                    myServiceName = serverService.MyServiceInfo.ServiceName;
                    Title = myServiceName;
                }
                else
                {
                    myServiceName = null;
                    Title = "Service Not Registered";
                }
                serviceBound = true;
                discover();
            }, () =>
            {
                this.serverService = null;
                Log.Info(id, "service disconnected");
                serviceBound = false;
            });

            // bind to server service
            if (isServiceRunning())
                // service already started before the app
                BindService(new Intent(this, typeof(ServerService)), serviceConnection, Bind.None);
            // else the service is starting, wait till service broadcast registration complete to bind

		}

		protected override void OnResume()
		{
            RegisterReceiver(p2pServiceBroadcastReceiver, intentFilter);
            if (serviceBound) {
                if (serverService.ServiceRegistered)
                {
                    myServiceName = serverService.MyServiceInfo.ServiceName;
                    Title = myServiceName;
                } else {
                    myServiceName = null;
                    Title = "Service Not Registered";
                }
            }
            if (!discovering)
                discover();
            base.OnResume();
		}

		protected override void OnPause()
		{
            if (discovering)
            {
                nsdManager.StopServiceDiscovery(nsdDiscoveryListener);
                adapter.Clear();
                foundServices.Clear();
            }
            UnregisterReceiver(p2pServiceBroadcastReceiver);
			base.OnPause();
		}

        protected override void OnStop()
        {
            base.OnStop();
        }

        protected override void OnDestroy()
        {
            if (serviceBound)
            {
                UnbindService(serviceConnection);
                serviceBound = false;
            }
            Log.Info(id, "Activity destroyed");
            base.OnDestroy();
        }

        private void discover() {
            if (nsdManager != null) {
                adapter.Clear();
                foundServices.Clear();
                nsdManager.DiscoverServices("_backpack._tcp", NsdProtocol.DnsSd, nsdDiscoveryListener);
			}
        }

        private bool isServiceRunning()
        {
            ActivityManager manager = (ActivityManager)GetSystemService(Context.ActivityService);
            string className = "edu.isi.wifiptp.ServerService";
            foreach (RunningServiceInfo service in manager.GetRunningServices(Integer.MaxValue))
            {
                //Log.Info(id, "Found " + service.Service.ClassName);
                if (className.Equals(service.Service.ClassName))
                {
                    Log.Debug(id, "Service is already running");
                    return true;
                }
            }
            Log.Debug(id, "Service has not started");
            return false;
        }

        private void resolveService(NsdServiceInfo info) {
            Log.Debug(id, "Resolve service: " + info.ServiceName);
            if (foundServices.IndexOf(info.ToString()) == -1)
            { // avoid duplicates

                nsdManager.ResolveService(info, new ServiceResolvedListener((NsdServiceInfo info1) =>
                {
                    Log.Debug(id, "Service resolved: " + info1.ServiceName);
                    RunOnUiThread(() =>
                    {
                        if (!foundServices.Contains(info1.ToString()))
                        {
                            adapter.Add(new MyServiceInfo(info1.ServiceName, info1.Host, info1.Port));
                            foundServices.Add(info1.ToString());
                        }
                    });
                }, (NsdServiceInfo info1) =>
                {
                    // resolve failed, try again
                    resolveService(info1);
                }));
            }
        }

        public void OnTaskCompleted()
        {
            //discover();
        }

        public void OnServiceRegistered()
        {
            // if not yet bind to service
            if (!serviceBound) {
                BindService(new Intent(this, typeof(ServerService)), serviceConnection, Bind.None);
            } else {
                myServiceName = serverService.MyServiceInfo.ServiceName;
                Title = myServiceName;
            }
        }

        public void OnServiceUnregistered() {
            myServiceName = null;
            Title = "Service Not Registered";
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
            private Action onDiscoveryStartedAction;
            private Action onDiscoveryStoppedAction;
			private Action<NsdServiceInfo> onServiceFoundAction;
            private Action<NsdServiceInfo> onServiceLostAction;

            public NsdDiscoveryListener(NsdManager nsdManager, Action onDiscoveryStartedAction, Action onDiscoveryStoppedAction, Action<NsdServiceInfo> onServiceFoundAction, Action<NsdServiceInfo> onServiceLostAction)
			{
				this.nsdManager = nsdManager;
                this.onDiscoveryStartedAction = onDiscoveryStartedAction;
                this.onDiscoveryStoppedAction = onDiscoveryStoppedAction;
				this.onServiceFoundAction = onServiceFoundAction;
                this.onServiceLostAction = onServiceLostAction;
			}

			public void OnDiscoveryStarted(string serviceType)
			{
				Log.Debug(id, "Nsd Discovery Started");
                onDiscoveryStartedAction();
			}

			public void OnDiscoveryStopped(string serviceType)
			{
				Log.Debug(id, "Nsd Discovery Stopped");
                onDiscoveryStoppedAction();
			}

			public void OnServiceFound(NsdServiceInfo serviceInfo)
			{
				Log.Debug(id, "Service Found: " + serviceInfo);
				onServiceFoundAction(serviceInfo);
			}

			public void OnServiceLost(NsdServiceInfo serviceInfo)
			{
                Log.Debug(id, "Service Lost: " + serviceInfo);
                onServiceLostAction(serviceInfo);
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
            private Action<NsdServiceInfo> serviceResolvedAction, resolveFailedAction;

            public ServiceResolvedListener(Action<NsdServiceInfo> serviceResolvedAction, Action<NsdServiceInfo> resolveFailedAction)
			{
				this.serviceResolvedAction = serviceResolvedAction;
                this.resolveFailedAction = resolveFailedAction;
			}
			public void OnResolveFailed(NsdServiceInfo serviceInfo, NsdFailure errorCode)
			{
                Log.Error(id, "Resolve " + serviceInfo.ServiceName + " Failed: " + errorCode.ToString());
                resolveFailedAction(serviceInfo);
			}

			public void OnServiceResolved(NsdServiceInfo serviceInfo)
			{
				//Log.Debug(id, "Service Resolved: " + serviceInfo);
				serviceResolvedAction(serviceInfo);
			}
		}
    }
}

