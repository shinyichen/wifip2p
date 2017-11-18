using Android.App;
using Android.Widget;
using Android.OS;
using System.Collections.Generic;
using Android.Util;
using Java.Net;
using System;
using wifiptp.Api;
using Android.Net.Nsd;

namespace wifiptp
{
    [Activity(Label = "wifiptp", MainLauncher = true, Icon = "@mipmap/icon")]
    public class MainActivity : Activity, StatusChangedListener, ITaskCompleted
	{

        private const string id = "Backpack-Main";

        private const string serviceName = "backpack";

        private Wifiptp wifiptp;

        //private bool discoverable = false;

        private Switch discoverableSwitch;

        //private bool searching = false;

        private Switch searchSwitch;

        private List<string> foundServices = new List<string>();

        private ListView listView;

        protected ArrayAdapter adapter;

        private string myServiceName;

     
		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

            // layout
			SetContentView(Resource.Layout.Main);
            Title = "Service Unregistered";

            discoverableSwitch = (Switch)FindViewById(Resource.Id.discoverableSwitch);
            discoverableSwitch.CheckedChange += (sender, e) => {
                if (e.IsChecked) {
                    DisableAllSwitches();
                    wifiptp.setDiscoverable(true);
                } else {
                    DisableAllSwitches();
                    wifiptp.setDiscoverable(false);
                }
            };

            searchSwitch = (Switch)FindViewById(Resource.Id.searchSwitch);
            searchSwitch.Enabled = false;
            searchSwitch.CheckedChange += (sender, e) => {
                if (e.IsChecked)
                {
                    DisableAllSwitches();
                    wifiptp.startDiscoverServices();
                }
                else
                {
                    DisableAllSwitches();
                    wifiptp.stopDiscoverServices();
                }
            };

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


            wifiptp = new Wifiptp(serviceName, this, this);

			
		}

		protected override void OnResume()
		{
            base.OnResume();
		}

		protected override void OnPause()
		{
            // TODO what to do during file transfer? task will run in background?
            wifiptp.setDiscoverable(false);
            wifiptp.stopDiscoverServices();
			base.OnPause();
		}

        protected override void OnStop()
        {
            base.OnStop();
        }

        protected override void OnDestroy()
        {
            Log.Info(id, "Activity destroyed");
            base.OnDestroy();
        }

        private void EnableAllSwitches() {
            discoverableSwitch.Enabled = true;
            searchSwitch.Enabled = true;
        } 

        private void DisableAllSwitches() {
            discoverableSwitch.Enabled = false;
            searchSwitch.Enabled = false;
        } 

        // client async
        public void OnTaskCompleted()
        {
        }



        // updates from Wifiptp
        public void NsdRegistered(string serviceName)
        {
            RunOnUiThread(() => {
                discoverableSwitch.Checked = true;
                EnableAllSwitches();
                myServiceName = serviceName;
                Title = myServiceName;
            });
        }

        public void NsdUnregistered()
        {
            RunOnUiThread(() => {
                discoverableSwitch.Checked = false;
                discoverableSwitch.Enabled = true;
                searchSwitch.Enabled = false;
                myServiceName = null;
                Title = "Service Not Registered";
            });
        }

        public void NsdRegistrationFailed(Wifiptp.Error error)
        {
            RunOnUiThread(() =>
            {
                discoverableSwitch.Checked = false;
                discoverableSwitch.Enabled = true;
                searchSwitch.Enabled = false;

                if (error.Equals(Wifiptp.Error.NoWifi))
                {
                    // TODO let user know
                }
            });
        }

        public void NsdUnregistrationFailed(Wifiptp.Error error)
        {
            RunOnUiThread(() =>
            {
                discoverableSwitch.Checked = true;
                EnableAllSwitches();

                // let user know about error
            });
        }


        public void StartDiscoveryFailed(Wifiptp.Error error)
        {
            searchSwitch.Checked = false;
            EnableAllSwitches();
            if (error.Equals(Wifiptp.Error.NoWifi)) {
                // TODO let user know
            }
        }

        public void DiscoveryStarted()
        {
            RunOnUiThread(() =>
            {
                searchSwitch.Checked = true;
                EnableAllSwitches();
            });
        }

        public void StopDiscoveryFailed(Wifiptp.Error error) {

            RunOnUiThread(() =>
            {
                searchSwitch.Checked = true;
                EnableAllSwitches();
            });
        }

        public void DiscoveryStopped()
        {
            RunOnUiThread(() =>
            {
                searchSwitch.Checked = false;
                adapter.Clear();
                EnableAllSwitches();
            });
        }

        public void DeviceFound(NsdServiceInfo device) {
            RunOnUiThread(() =>
            {
                adapter.Add(new MyServiceInfo(device.ServiceName, device.Host, device.Port));
            });
        }

        public void DeviceLost(NsdServiceInfo device) {
            RunOnUiThread(() =>
            {
                for (int i = 0; i < adapter.Count; i++)
                {
                    MyServiceInfo info = (MyServiceInfo)adapter.GetItem(i);
                    if (device.ServiceName == info.ServiceName)
                    {
                        adapter.Remove(info);
                        break;
                    }
                }
            });
        }

        public void ConnectionReceived()
        {
           // TODO stop discovery, disable buttons and list
        }

        public void ConnectionClosed()
        {
            // TODO restart discovery, enable buttons and list
        }

        public void FileSent()
        {
            throw new NotImplementedException();
        }


    }
}

