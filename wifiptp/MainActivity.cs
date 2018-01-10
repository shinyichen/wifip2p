using Android.App;
using Android.Widget;
using Android.OS;
using System.Collections.Generic;
using Android.Util;
using wifiptp.Api;
using Android.Net.Nsd;
using Java.IO;
using System.Net;

namespace wifiptp
{
    [Activity(Label = "wifiptp", MainLauncher = true, Icon = "@mipmap/icon")]
    public class MainActivity : Activity, StatusChangedListener
	{

        private const string id = "Backpack-Main";

        private const string serviceName = "backpack";

        private Wifiptp wifiptp;

        private Switch discoverableSwitch;

        private Switch searchSwitch;

        private TextView serverStatusTextView;

        private TextView clientStatusTextView;

        private List<string> foundServices = new List<string>();

        private ListView deviceListView;

        private ArrayAdapter deviceListadapter;

        private ListView fileListView;

        private ArrayAdapter fileListAdapter;

        private Button sendButton;

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

            serverStatusTextView = (TextView)FindViewById(Resource.Id.serverStatus);
            serverStatusTextView.Text = "Disconnected";

            clientStatusTextView = (TextView)FindViewById(Resource.Id.clientStatus);
            clientStatusTextView.Text = "Disconnected";

            // single selection only
            deviceListadapter = new ArrayAdapter(this, Android.Resource.Layout.SimpleListItemSingleChoice);
            deviceListadapter.SetNotifyOnChange(true);

			deviceListView = FindViewById<ListView>(Resource.Id.deviceListView);
            deviceListView.ChoiceMode = ChoiceMode.Single;
            deviceListView.Adapter = deviceListadapter;
            deviceListView.Enabled = false;

            // file list view
            fileListAdapter = new ArrayAdapter(this, Android.Resource.Layout.SimpleListItemMultipleChoice);
            fileListAdapter.SetNotifyOnChange(true);

            fileListView = FindViewById<ListView>(Resource.Id.fileListView);
            fileListView.ChoiceMode = ChoiceMode.Multiple;
            fileListView.Adapter = fileListAdapter;
            fileListView.Enabled = false;

            // app file list 
            File[] files = GetExternalFilesDir(null).ListFiles(new VisibleFilesFilter());
            foreach (File file in files){
                fileListAdapter.Add(new MyFile(file));
            }

            sendButton = FindViewById<Button>(Resource.Id.sendButton);
            sendButton.Click += (sender, e) => {


                // get selected files
                int pos;
                SparseBooleanArray selected = fileListView.CheckedItemPositions;
                List<string> selectedFiles = new List<string>();
                for (int i = 0; i < selected.Size(); i++) {
                    pos = selected.KeyAt(i);
                    if (selected.ValueAt(i)) { // selected
                        selectedFiles.Add(((MyFile)fileListAdapter.GetItem(pos)).File.AbsolutePath);
                    }
                }

                // get selected device
                pos = deviceListView.CheckedItemPosition;
                MyServiceInfo selectedDevice = (MyServiceInfo)deviceListadapter.GetItem(pos);

                // send
                if (selectedDevice != null && selectedFiles.Count > 0) { 
                    sendButton.Enabled = false;
                    IPAddress ipAddress = new IPAddress(selectedDevice.Host.GetAddress());
                    IPEndPoint ipEndPoint = new IPEndPoint(ipAddress, selectedDevice.Port);
                    wifiptp.sendFile(ipAddress, ipEndPoint, selectedFiles);
                    // TODO clear selections
                }

            };

            // TODO enable sendButton if files and device selected
            // TODO clear device list when search is turned off


            wifiptp = new Wifiptp(serviceName, this, this);
         
        }

		protected override void OnResume()
		{
            base.OnResume();
		}

		protected override void OnPause()
		{

            wifiptp.setDiscoverable(false); // this includes stop searching
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

        private void EnableLists() {
            deviceListView.Enabled = true;
            fileListView.Enabled = true;
        }

        private void DisableLists()
        {
            deviceListView.Enabled = false;
            fileListView.Enabled = false;
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
                EnableLists();
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
                deviceListadapter.Clear();
                EnableAllSwitches();
                DisableLists();
            });
        }

        public void DeviceFound(NsdServiceInfo device) {
            RunOnUiThread(() =>
            {
                deviceListadapter.Add(new MyServiceInfo(device.ServiceName, device.Host, device.Port));
            });
        }

        public void DeviceLost(NsdServiceInfo device) {
            RunOnUiThread(() =>
            {
                for (int i = 0; i < deviceListadapter.Count; i++)
                {
                    MyServiceInfo info = (MyServiceInfo)deviceListadapter.GetItem(i);
                    if (device.ServiceName == info.ServiceName)
                    {
                        deviceListadapter.Remove(info);
                        break;
                    }
                }
            });
        }

        public void Connected(bool server)
        {
            RunOnUiThread(() =>
            {
                // stop discovery, disable buttons and list
                sendButton.Enabled = false;
                if (server)
                    serverStatusTextView.Text = "Connected";
                else
                    clientStatusTextView.Text = "Connected";
            });
        }

        public void Disconnected(bool server)
        {
            RunOnUiThread(() =>
            {
                // restart discovery, enable buttons and list
                sendButton.Enabled = true;
                if (server)
                    serverStatusTextView.Text = "Disconnected";
                else
                    clientStatusTextView.Text = "Disconnected";
            });
        }

        public void FilesReceived() {
            // refresh file list
            fileListAdapter.Clear();
            File[] files = GetExternalFilesDir(null).ListFiles(new VisibleFilesFilter());
            foreach (File file in files)
            {
                fileListAdapter.Add(new MyFile(file));
            }
        }

    }

    public class VisibleFilesFilter : Java.Lang.Object, IFileFilter
    {

        public bool Accept(File pathname)
        {
            return !pathname.IsHidden;
        }
    }
}

