
using System.Collections.Generic;

using Android.OS;
using Android.Support.V4.App;
using Android.Support.Design.Widget;
using Android.Support.V4.Widget;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace wifiptp
{
    public class FilesViewFragment : Fragment
    {
        private MainActivity mainActivity;

        private SwipeRefreshLayout refreshLayout;

        private ListView fileListView;

        private Button deleteButton;

        private Button shareButton;

        public override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Create your fragment here
        }

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Use this to return your custom view for this Fragment
            // return inflater.Inflate(Resource.Layout.YourFragment, container, false);

            return inflater.Inflate(Resource.Layout.FilesView, container, false);
        }

        public override void OnViewCreated(View view, Bundle savedInstanceState)
        {

            base.OnViewCreated(view, savedInstanceState);

            mainActivity = (MainActivity)Activity;

            // layout
            refreshLayout = (SwipeRefreshLayout)view.FindViewById(Resource.Id.refreshLayout);
            OnRefreshListener refreshListener = new OnRefreshListener(() =>
            {
                mainActivity.RefreshFileList();
                refreshLayout.Refreshing = false;
            });
            refreshLayout.SetOnRefreshListener(refreshListener);

            // file list view
            fileListView = view.FindViewById<ListView>(Resource.Id.fileListView);
            fileListView.ChoiceMode = ChoiceMode.Multiple;
            fileListView.Adapter = mainActivity.FileListAdapter;

            deleteButton = (Button)view.FindViewById(Resource.Id.deleteButton);
            deleteButton.Click += (sender, e) =>
            {
                SparseBooleanArray selected = fileListView.CheckedItemPositions;
                List<string> selectedFiles = new List<string>();

                if (selected.Size() == 0)
                {
                    Snackbar.Make(view.FindViewById(Resource.Id.filesCoordinateLayout), "Must select files.", Snackbar.LengthShort).Show();
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
                            ((MyFile)mainActivity.FileListAdapter.GetItem(pos)).File.Delete();
                        }
                    }
                    clearFileListSelection();
                    mainActivity.RefreshFileList();
                }
            };

            shareButton = view.FindViewById<Button>(Resource.Id.shareButton);
            shareButton.Click += (sender, e) =>
            {
                int pos;
                SparseBooleanArray selected = fileListView.CheckedItemPositions;
                List<string> selectedFiles = new List<string>();
                for (int i = 0; i < selected.Size(); i++)
                {
                    pos = selected.KeyAt(i);
                    if (selected.ValueAt(i))
                    { // selected
                        selectedFiles.Add(((MyFile)mainActivity.FileListAdapter.GetItem(pos)).File.AbsolutePath);
                    }
                }

                if (!mainActivity.Visible)
                {
                    Snackbar.Make(view.FindViewById(Resource.Id.filesCoordinateLayout), "Must set device to visible.", Snackbar.LengthShort).Show();
                }
                else if (selectedFiles.Count == 0)
                {
                    Snackbar.Make(view.FindViewById(Resource.Id.filesCoordinateLayout), "Must select files.", Snackbar.LengthShort).Show();
                }
                else
                {
                    // go to search view
                    mainActivity.Share(selectedFiles);
                }
            };

        }

        private void clearFileListSelection()
        {
            fileListView.SetItemChecked(0, true);
            fileListView.ClearChoices();
        }
    }
}
