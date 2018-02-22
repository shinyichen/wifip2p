
using System.Collections.Generic;

using Android.OS;
using Android.Support.V4.App;
using Android.Support.Design.Widget;
using Android.Support.V4.Widget;
using Android.Util;
using Android.Widget;
using System;
using Android.Support.V7.View;
using Android.Views;

namespace wifiptp
{
    public class FilesViewFragment : Fragment
    {
        private MainActivity mainActivity;

        private SwipeRefreshLayout refreshLayout;

        private ListView fileListView;

        private Android.Support.V7.View.ActionMode actionMode = null;

        private View view;


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
            this.view = view;

            // layout
            refreshLayout = (SwipeRefreshLayout)view.FindViewById(Resource.Id.refreshLayout);
            OnRefreshListener refreshListener = new OnRefreshListener(() =>
            {
                if (actionMode == null)
                    mainActivity.RefreshFileList();
                refreshLayout.Refreshing = false;
            });
            refreshLayout.SetOnRefreshListener(refreshListener);

            // file list view
            fileListView = view.FindViewById<ListView>(Resource.Id.fileListView);
            fileListView.ChoiceMode = ChoiceMode.Multiple;
            fileListView.Adapter = mainActivity.FileListAdapter;

            // http://www.androhub.com/android-contextual-action-mode-over-toolbar/
            fileListView.OnItemClickListener = new ItemClickListener((int pos) => {
                if (actionMode != null) {
                    selectListItem(pos);
                }
            });
            fileListView.OnItemLongClickListener = new ItemLongClickListener((int pos) => {
                selectListItem(pos);
            });

        }

        private void clearFileListSelection()
        {
            mainActivity.FileListAdapter.RemoveSelections();
        }

        private void selectListItem(int pos) {
            mainActivity.FileListAdapter.ToggleSelection(pos);

            bool hasCheckedItem = mainActivity.FileListAdapter.getSelectedCount() > 0;
            if (hasCheckedItem && actionMode == null) {
                actionMode = mainActivity.StartSupportActionMode(new ToolbarActionCallback(
                    (Android.Support.V7.View.ActionMode mode, IMenu menu) => {
                        // action created
                        mode.MenuInflater.Inflate(Resource.Menu.FileActionMenu, menu);
                }, (Android.Support.V7.View.ActionMode mode, IMenuItem item) => {
                    // action item clicked

                    switch (item.ItemId) {
                        case Resource.Id.action_delete:
                            deleteSelectedFiles();
                            actionMode.Finish();
                            break;
                        case Resource.Id.action_share:
                            shareSelectedFiles();
                            actionMode.Finish();
                            break;
                            
                    }
                }, () => {
                    // action mode destroyed
                    // clear list selection
                    mainActivity.FileListAdapter.RemoveSelections();
                    actionMode = null;
                }));
            } else if (!hasCheckedItem && actionMode != null) {
                actionMode.Finish();
            }
        }

        private void deleteSelectedFiles() {
            SparseBooleanArray selected = mainActivity.FileListAdapter.getSelectedIds();
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
        }

        private void shareSelectedFiles() {
            int pos;
            SparseBooleanArray selected = mainActivity.FileListAdapter.getSelectedIds();
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
        }

        private class ItemClickListener : Java.Lang.Object, AdapterView.IOnItemClickListener
        {
            private Action<int> itemClickAction;

            public ItemClickListener(Action<int> itemClickAction) {
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
                                        Action actionDestroyAction) {
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


}
