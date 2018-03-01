using Android.App;
using Android.Widget;
using Android.OS;
using System.Collections.Generic;
using Android.Util;
using wifiptp.Api;
using Android.Net.Nsd;
using Java.IO;
using System.Net;
using Android.Support.V7.App;
using Android.Support.Design.Widget;
using Android.Content.PM;
using Android.Support.V4.Widget;
using System;
using Android.Content;
using Android.Views;

namespace wifiptp
{
    [Activity(Label = "wifiptp", MainLauncher = true, Icon = "@mipmap/icon", ScreenOrientation = ScreenOrientation.Portrait)]
    public class MainActivity : AppCompatActivity, StatusChangedListener
    {

        private const string id = "Backpack-Main";

        private const string serviceName = "backpack";

        private const int FILES_VIEW = 0;

        private const int SEARCH_VIEW = 1;

        private int currentFragment = FILES_VIEW;

        private Wifiptp wifiptp;

        private bool isVisible = false;

        public bool Visible {
            get {
                return isVisible;
            }
        }

        private List<string> foundServices = new List<string>();

        private FileListViewAdapter fileListAdapter;

        public FileListViewAdapter FileListAdapter {
            get {
                return fileListAdapter;
            }
        }

        private ArrayAdapter deviceListadapter;

        public ArrayAdapter DeviceListAdapter {
            get {
                return deviceListadapter;
            }
        }

        private IMenu optionsMenu;

        private List<string> selectedFiles;

        public List<string> SelectedFiles {
            get {
                return selectedFiles;
            }
        }

        private string myServiceName;




        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // layout
            SetContentView(Resource.Layout.MainActivityLayout);

            Android.Support.V7.Widget.Toolbar toolbar = (Android.Support.V7.Widget.Toolbar)FindViewById(Resource.Id.filesActionBar);
            SetSupportActionBar(toolbar);

            // initialize file list adapter
            File[] files = GetExternalFilesDir(null).ListFiles(new VisibleFilesFilter());
            List<MyFile> fileList = new List<MyFile>();
            foreach (File file in files)
            {
                fileList.Add(new MyFile(file));
            }
            fileListAdapter = new FileListViewAdapter(this, fileList);
            fileListAdapter.NotifyDataSetChanged();


            // init device list adapter
            deviceListadapter = new ArrayAdapter(this, Android.Resource.Layout.SimpleListItemSingleChoice);
            deviceListadapter.SetNotifyOnChange(true);



            wifiptp = new Wifiptp(serviceName, this, this);

            Android.Support.V4.App.FragmentTransaction t = SupportFragmentManager.BeginTransaction();
            t.Replace(Resource.Id.fragmentHolder, new FilesViewFragment());
            t.Commit();
            currentFragment = FILES_VIEW;
                                                          
        }

        protected override void OnResume()
        {
            base.OnResume();
        }


        public override bool OnCreateOptionsMenu(Android.Views.IMenu menu)
        {
            optionsMenu = menu;
            MenuInflater.Inflate(Resource.Menu.ActionMenu, menu);
            return base.OnCreateOptionsMenu(menu);
        }


        public override bool OnOptionsItemSelected(Android.Views.IMenuItem item)
        {

            switch (item.ItemId)
            {
                case Resource.Id.action_visible:
                    // TODO tell fragement to disable buttons
                    if (isVisible) 
                        wifiptp.setDiscoverable(false);
                    else
                        wifiptp.setDiscoverable(true);
                    return true;
            }

            return base.OnOptionsItemSelected(item);
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

        /**************** private methods *************/

        private void clearBackstack() {
            for (int i = 0; i < SupportFragmentManager.BackStackEntryCount; i++) {
                SupportFragmentManager.PopBackStack();
            }
        }


        private void openFile(File f) {
            Uri uri;
        }
      
        /**************** public methods **************/


        public void RefreshFileList()
        {
            fileListAdapter.Clear();
            File[] files = GetExternalFilesDir(null).ListFiles(new VisibleFilesFilter());
            foreach (File file in files)
            {
                fileListAdapter.Add(new MyFile(file));
            }
            fileListAdapter.NotifyDataSetChanged();
        }

        // called by FilesViewFragment when use click share
        public void Share(List<string> files) {
            selectedFiles = files;
            //Android.Support.V4.App.FragmentTransaction t = SupportFragmentManager.BeginTransaction();
            //SearchFragment f = new SearchFragment();
            //t.Replace(Resource.Id.fragmentHolder, f);
            //t.AddToBackStack(null);
            //t.Commit();
            //currentFragment = SEARCH_VIEW;
            SearchFragment f = new SearchFragment();
            f.Show(SupportFragmentManager, "");
        }

        public void StartDiscovery() {
            wifiptp.startDiscoverServices();
        }

        public void StopDiscovery() {
            wifiptp.stopDiscoverServices();
        }

        public void GoBack() {
            SupportFragmentManager.PopBackStack();
        }

        public void Send(MyServiceInfo device) {

            // send
            if (device != null)
            {
                IPAddress ipAddress = new IPAddress(device.Host.GetAddress());
                IPEndPoint ipEndPoint = new IPEndPoint(ipAddress, device.Port);
                wifiptp.sendFile(ipAddress, ipEndPoint, selectedFiles);
            }

        }


        /**************************** status listener **************************/

        // updates from Wifiptp
        public void NsdRegistered(string serviceName)
        {
            RunOnUiThread(() =>
            {
                // set action icon to visible
                optionsMenu.GetItem(0).SetIcon(Resource.Drawable.design_ic_visibility);

                // TODO update with fragment (they need to enable buttons)
                isVisible = true;
                myServiceName = serviceName;
                SupportActionBar.Title = myServiceName;

                // if user is at search view and turns on visibility
                // start searching
                if (currentFragment == SEARCH_VIEW)
                {
                    wifiptp.startDiscoverServices();
                }
            });
        }

        public void NsdUnregistered()
        {
            RunOnUiThread(() =>
            {
                // set action icon to invisible
                optionsMenu.GetItem(0).SetIcon(Resource.Drawable.design_ic_visibility_off);

                // TODO update with fragment (they need to enable buttons)
                isVisible = false;
                myServiceName = null;
                SupportActionBar.Title = "Not Visible";
            });
        }

        public void NsdRegistrationFailed(Wifiptp.Error error)
        {
            RunOnUiThread(() =>
            {
                // TODO update with fragment (they need to enable buttons)
                if (error.Equals(Wifiptp.Error.NoWifi))
                {
                    // let user know
                    Snackbar.Make(FindViewById(Resource.Id.mainCoordinatorLayout), "Network Service Registration Failed", Snackbar.LengthLong).Show();
                }
            });
        }

        public void NsdUnregistrationFailed(Wifiptp.Error error)
        {
            RunOnUiThread(() =>
            {
                // TODO update with fragment (they need to enable buttons)

                // let user know about error
                Snackbar.Make(FindViewById(Resource.Id.mainCoordinatorLayout), "Network Service Unregistration Failed", Snackbar.LengthLong).Show();
            });
        }


        public void StartDiscoveryFailed(Wifiptp.Error error)
        {
            // TODO update with fragment (they need to enable buttons)

            if (error.Equals(Wifiptp.Error.NoWifi))
            {
                // let user know
                Snackbar.Make(FindViewById(Resource.Id.mainCoordinatorLayout), "Error, no WIFI", Snackbar.LengthLong).Show();
            }
        }

        public void DiscoveryStarted()
        {
            RunOnUiThread(() =>
            {
                // TODO update with fragment (they need to enable buttons)
                Snackbar.Make(FindViewById(Resource.Id.mainCoordinatorLayout), "Discovery Started", Snackbar.LengthShort).Show();
            });
        }

        public void StopDiscoveryFailed(Wifiptp.Error error)
        {

            RunOnUiThread(() =>
            {
                // TODO update with fragment (they need to enable buttons)
                Snackbar.Make(FindViewById(Resource.Id.mainCoordinatorLayout), "Discovery Stopped", Snackbar.LengthShort).Show();
            });
        }

        public void DiscoveryStopped()
        {
            RunOnUiThread(() =>
            {
                deviceListadapter.Clear();
                // TODO update with fragment (they need to enable buttons, clear device list)
            });
        }

        public void DeviceFound(NsdServiceInfo device)
        {
            RunOnUiThread(() =>
            {
                deviceListadapter.Add(new MyServiceInfo(device.ServiceName, device.Host, device.Port));
            });
        }

        public void DeviceLost(NsdServiceInfo device)
        {
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
                Snackbar.Make(FindViewById(Resource.Id.mainCoordinatorLayout), "Connected, transfering", Snackbar.LengthLong).Show();

                // stop discovery, disable buttons and list
                // TODO disable buttons
            });
        }

        public void Disconnected(bool server)
        {
            RunOnUiThread(() =>
            {
                // TODO enable buttons

                clearBackstack();

                if (!server) {
                    // go  to main screen, clear list selection, stop discovery
                    StopDiscovery();
                    selectedFiles.Clear();

                    // go to main screen
                    Android.Support.V4.App.FragmentTransaction t = SupportFragmentManager.BeginTransaction();
                    FilesViewFragment f = new FilesViewFragment();
                    t.Replace(Resource.Id.fragmentHolder, f);
                    t.Commit();
                    currentFragment = FILES_VIEW;
                }

                Snackbar.Make(FindViewById(Resource.Id.mainCoordinatorLayout), "Transfer complete, disconnecting", Snackbar.LengthLong).Show();
            });
        }

        public void FilesReceived()
        {
            // refresh file list
            fileListAdapter.Clear();
            File[] files = GetExternalFilesDir(null).ListFiles(new VisibleFilesFilter());
            foreach (File file in files)
            {
                fileListAdapter.Add(new MyFile(file));
            }
            fileListAdapter.NotifyDataSetChanged();
        }

    }

    public class VisibleFilesFilter : Java.Lang.Object, IFileFilter
    {

        public bool Accept(File pathname)
        {
            return !pathname.IsHidden;
        }
    }

    public class OnRefreshListener : Java.Lang.Object, SwipeRefreshLayout.IOnRefreshListener
    {
        private Action refresh;

        public OnRefreshListener(Action refresh) {
            this.refresh = refresh;
        }

        public void OnRefresh()
        {
            refresh();
        }
    }
}

