using System;
using System.Collections.Generic;
using Android.Content;
using Android.Graphics;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.Lang;

namespace wifiptp
{
    public class FileListViewAdapter : BaseAdapter<MyFile>
    {

        private Context context;
        private List<MyFile> files;
        private SparseBooleanArray selectedIds = new SparseBooleanArray();

        public FileListViewAdapter(Context context, List<MyFile> files)
        {
            this.context = context;
            this.files = files;
        }

        public override MyFile this[int position] => files[position];

        public override int Count => files.Count;


        public override long GetItemId(int position)
        {
            return position;
        }

        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            LayoutInflater inflator = (LayoutInflater)context.GetSystemService(Context.LayoutInflaterService);
            ViewHolder holder;
            if (convertView == null) {
                convertView = inflator.Inflate(Resource.Layout.ListItem, parent, false);
                holder = new ViewHolder();
                holder.title = (TextView)convertView.FindViewById(Resource.Id.listItemText);
                holder.thumbnail = (ImageView)convertView.FindViewById(Resource.Id.listItemIcon);
                convertView.Tag = holder;
            } else {
                holder = (ViewHolder)convertView.Tag;
            }

            convertView.SetBackgroundColor(selectedIds.Get(position) ? Color.ParseColor("#E0E0E0") : Color.Transparent);

            MyFile f = files[position];
            holder.title.Text = f.ToString();
            if (f.Type == "image/gif" || f.Type == "image/jpeg") {
                if (f.Thumbnail == null)
                {
                    f.Thumbnail = UiUtils.GenerateBitmapFromImage(f.File.AbsolutePath);

                }

                // TODO this will use a lot of memory, save on disk and use path instead?
                holder.thumbnail.SetImageBitmap(f.Thumbnail);
            }

            return convertView;
        }

        public void Add(MyFile file) {
            files.Add(file);
        }

        public void Clear() {
            files.Clear();
        }

        public void ToggleSelection(int pos) {
            selectView(pos, !selectedIds.Get(pos));
        }

        public void RemoveSelections() {
            selectedIds = new SparseBooleanArray();
            NotifyDataSetChanged();
        }

        public int getSelectedCount() {
            return selectedIds.Size();
        }

        public SparseBooleanArray getSelectedIds() {
            return selectedIds;
        }

        private void selectView(int pos, bool select) {
            if (select)
                selectedIds.Put(pos, true);
            else
                selectedIds.Delete(pos);

            NotifyDataSetChanged();
        }


        private class ViewHolder : Java.Lang.Object
        {
            public TextView title;
            public ImageView thumbnail;
        }
    }


}
