using Android.App;
using Android.Widget;
using Android.OS;
using System.Collections.Generic;
using Android.Util;
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
using wifip2pApi.Android;

namespace wifiptp
{
    [Activity(Label = "wifiptp", MainLauncher = true, Icon = "@mipmap/icon", ScreenOrientation = ScreenOrientation.Portrait)]
    public class MainActivity : AppCompatActivity, StatusChangedListener
    {

        private const string id = "Backpack-Main";

        private const string serviceName = "backpack";

        private Wifiptp wifiptp;

        private bool isVisible = false;

        public bool Visible {
            get {
                return isVisible;
            }
        }

        private SwipeRefreshLayout refreshLayout;


        private FileListViewAdapter fileListAdapter;

        private ListView fileListView;

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

        private Android.Support.V7.View.ActionMode actionMode = null;



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

            // refresh layout
            refreshLayout = (SwipeRefreshLayout)FindViewById(Resource.Id.refreshLayout);
            OnRefreshListener refreshListener = new OnRefreshListener(() =>
            {
                if (actionMode == null)
                    refreshFileList();
                refreshLayout.Refreshing = false;
            });
            refreshLayout.SetOnRefreshListener(refreshListener);

            // file list view
            fileListView = FindViewById<ListView>(Resource.Id.fileListView);
            fileListView.ChoiceMode = ChoiceMode.Multiple;
            fileListView.Adapter = fileListAdapter;

            // http://www.androhub.com/android-contextual-action-mode-over-toolbar/
            fileListView.OnItemClickListener = new ItemClickListener((int pos) => {
                if (actionMode != null)
                {
                    selectListItem(pos);
                } else {
                    // open file
                    OpenSelectedFile(pos);
                }
            });
            fileListView.OnItemLongClickListener = new ItemLongClickListener((int pos) => {
                selectListItem(pos);
            });

            wifiptp = new Wifiptp(serviceName, this, this);
                                                          
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

        private void OpenSelectedFile(int pos) {
            // TODO
            MyFile file = (MyFile)fileListAdapter.GetItem(pos);
            OpenFile(file);
        }

        private void selectListItem(int pos)
        {
            fileListAdapter.ToggleSelection(pos);

            bool hasCheckedItem = fileListAdapter.getSelectedCount() > 0;
            if (hasCheckedItem && actionMode == null)
            {
                actionMode = StartSupportActionMode(new ToolbarActionCallback(
                    (Android.Support.V7.View.ActionMode mode, IMenu menu) => {
                        // action created
                        mode.MenuInflater.Inflate(Resource.Menu.FileActionMenu, menu);
                    }, (Android.Support.V7.View.ActionMode mode, IMenuItem item) => {
                        // action item clicked

                        switch (item.ItemId)
                        {
                            case Resource.Id.action_delete:
                                deleteSelectedFiles();
                                actionMode.Finish();
                                break;
                            case Resource.Id.action_share:
                                shareSelectedFiles();
                                break;

                        }
                    }, () => {
                        // action mode destroyed
                        // clear list selection
                        fileListAdapter.RemoveSelections();
                        actionMode = null;
                    }));
            }
            else if (!hasCheckedItem && actionMode != null)
            {
                actionMode.Finish();
            }
        }

        private void deleteSelectedFiles()
        {
            SparseBooleanArray selected = fileListAdapter.getSelectedIds();

            if (selected.Size() == 0)
            {
                Snackbar.Make(FindViewById(Resource.Id.filesCoordinateLayout), "Must select files.", Snackbar.LengthShort).Show();
            }
            else
            {
                // delete files
                int pos;
                for (int i = 0; i < selected.Size(); i++)
                {
                    pos = selected.KeyAt(i);
                    if (selected.ValueAt(i))
                    { // selected
                        ((MyFile)fileListAdapter.GetItem(pos)).File.Delete();
                    }
                }

                // clear selection
                fileListAdapter.RemoveSelections();

                refreshFileList();
            }
        }

        private void shareSelectedFiles()
        {
            int pos;
            SparseBooleanArray selected = fileListAdapter.getSelectedIds();
            List<string> selectedF = new List<string>();
            for (int i = 0; i < selected.Size(); i++)
            {
                pos = selected.KeyAt(i);
                if (selected.ValueAt(i))
                { // selected
                    selectedF.Add(((MyFile)fileListAdapter.GetItem(pos)).File.AbsolutePath);
                }
            }

            if (!isVisible)
            {
                Snackbar.Make(FindViewById(Resource.Id.mainCoordinatorLayout), "Must set device to visible.", Snackbar.LengthShort).Show();
            }
            else if (selectedF.Count == 0)
            {
                Snackbar.Make(FindViewById(Resource.Id.mainCoordinatorLayout), "Must select files.", Snackbar.LengthShort).Show();
            }
            else
            {
                // go to search view
                share(selectedF);
            }
        }

        private void refreshFileList()
        {
            fileListAdapter.Clear();
            File[] files = GetExternalFilesDir(null).ListFiles(new VisibleFilesFilter());
            foreach (File file in files)
            {
                fileListAdapter.Add(new MyFile(file));
            }
            fileListAdapter.NotifyDataSetChanged();
        }

        private void OpenFile(MyFile file) {

            Android.Net.Uri uri = Android.Net.Uri.FromFile(file.File);

            string type = file.Type;

            Intent intent = new Intent(Intent.ActionView);
            intent.SetDataAndType(uri, type);
            intent.AddFlags(ActivityFlags.NewTask);
            StartActivity(intent);
        }

        private void share(List<string> files)
        {
            // open search fragment in a popup dialog
            selectedFiles = files;
            SearchFragment f = new SearchFragment();
            f.Show(SupportFragmentManager, "");

            // TODO disable all actions until disconnection
        }
      
        /**************** public methods **************/

        public void StartDiscovery() {
            wifiptp.startDiscoverServices();
        }

        public void StopDiscovery() {
            wifiptp.stopDiscoverServices();
        }

        public void Send(MyServiceInfo device) {

            // send
            if (device != null)
            {
                IPAddress ipAddress = new IPAddress(device.Host.GetAddress());
                IPEndPoint ipEndPoint = new IPEndPoint(ipAddress, device.Port);
                wifiptp.sendFile(device.Host, device.Port, selectedFiles);
                actionMode.Finish();
                Window.AddFlags(WindowManagerFlags.NotTouchable);
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

                // disable touch action if server
                // client should already have disabled action before connection
                if (server) {
                    Window.AddFlags(WindowManagerFlags.NotTouchable);
                }
                // stop discovery, disable buttons and list
                // TODO disable buttons
            });
        }

        public void Disconnected(bool server)
        {
            RunOnUiThread(() =>
            {
                // TODO enable buttons

                //clearBackstack();

                if (!server) {
                    // go  to main screen, clear list selection, stop discovery
                    StopDiscovery();
                    selectedFiles.Clear();

                    // go to main screen
                    //Android.Support.V4.App.FragmentTransaction t = SupportFragmentManager.BeginTransaction();
                    //FilesViewFragment f = new FilesViewFragment();
                    //t.Replace(Resource.Id.fragmentHolder, f);
                    //t.Commit();
                    //currentFragment = FILES_VIEW;

                }

                Snackbar.Make(FindViewById(Resource.Id.mainCoordinatorLayout), "Transfer complete, disconnecting", Snackbar.LengthLong).Show();

                // enable touch screen
                Window.ClearFlags(WindowManagerFlags.NotTouchable);
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

        private class ItemClickListener : Java.Lang.Object, AdapterView.IOnItemClickListener
        {
            private Action<int> itemClickAction;

            public ItemClickListener(Action<int> itemClickAction)
            {
                this.itemClickAction = itemClickAction;
            }

            public void OnItemClick(AdapterView parent, View view, int position, long id)
            {
                itemClickAction(position);
            }
        }

        private class ItemLongClickListener : Java.Lang.Object, AdapterView.IOnItemLongClickListener
        {
            private Action<int> itemClickAction;

            public ItemLongClickListener(Action<int> itemClickAction)
            {
                this.itemClickAction = itemClickAction;
            }

            public bool OnItemLongClick(AdapterView parent, View view, int position, long id)
            {
                itemClickAction(position);
                return true;
            }
        }

        private class ToolbarActionCallback : Java.Lang.Object, Android.Support.V7.View.ActionMode.ICallback
        {
            private Action<Android.Support.V7.View.ActionMode, IMenu> actionCreatedAction;
            private Action<Android.Support.V7.View.ActionMode, IMenuItem> actionItemClickedAction;
            private Action actionDestroyAction;

            public ToolbarActionCallback(Action<Android.Support.V7.View.ActionMode, IMenu> actionCreatedAction,
                                         Action<Android.Support.V7.View.ActionMode, IMenuItem> actionItemClickedAction,
                                        Action actionDestroyAction)
            {
                this.actionCreatedAction = actionCreatedAction;
                this.actionItemClickedAction = actionItemClickedAction;
                this.actionDestroyAction = actionDestroyAction;
            }

            public bool OnActionItemClicked(Android.Support.V7.View.ActionMode mode, IMenuItem item)
            {
                actionItemClickedAction(mode, item);
                return true;
            }

            public bool OnCreateActionMode(Android.Support.V7.View.ActionMode mode, IMenu menu)
            {
                actionCreatedAction(mode, menu);
                return true;
            }

            public void OnDestroyActionMode(Android.Support.V7.View.ActionMode mode)
            {
                actionDestroyAction();
            }

            public bool OnPrepareActionMode(Android.Support.V7.View.ActionMode mode, IMenu menu)
            {
                return true;
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

