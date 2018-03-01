using Android.OS;
using Android.Views;
using Android.Widget;
using Android.Support.V4.App;
using System.Collections.Generic;
using Android.Support.Design.Widget;

namespace wifiptp
{
    public class SearchFragment : DialogFragment
    {
        private MainActivity mainActivity;

        private List<string> files;

        private ListView deviceListView;

        private Button backButton;

        private Button sendButton;


        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Use this to return your custom view for this Fragment
            // return inflater.Inflate(Resource.Layout.YourFragment, container, false);


            View view = inflater.Inflate(Resource.Layout.SearchView, container, false);
            mainActivity = (MainActivity)Activity;

            if (mainActivity.Visible)
            {
                mainActivity.StartDiscovery();
            }

            // layout
            deviceListView = view.FindViewById<ListView>(Resource.Id.deviceListView);
            deviceListView.ChoiceMode = ChoiceMode.Single;
            deviceListView.Adapter = mainActivity.DeviceListAdapter;

            View emptyView = view.FindViewById(Resource.Id.deviceEmptyView);
            deviceListView.EmptyView = emptyView;

            backButton = (Button)view.FindViewById(Resource.Id.backButton);
            backButton.Click += (sender, e) => {
                //mainActivity.GoBack();
                Dismiss();
            };

            sendButton = (Button)view.FindViewById(Resource.Id.sendButton);
            sendButton.Click += (sender, e) => {

                // get selected device
                int pos = deviceListView.CheckedItemPosition;
                if (pos == -1)
                {
                    Snackbar.Make(view.FindViewById(Resource.Id.myCoordinatorLayout), "Must select a device.", Snackbar.LengthShort).Show();

                }
                else
                {
                    MyServiceInfo selectedDevice = (MyServiceInfo)mainActivity.DeviceListAdapter.GetItem(pos);
                    mainActivity.Send(selectedDevice);
                    Dismiss();
                }

            };

            return view;
        }


        public override void OnPause()
        {
            base.OnPause();

            mainActivity.StopDiscovery();
        }
    }
}
