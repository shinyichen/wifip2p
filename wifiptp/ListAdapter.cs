using System;
using System.Collections.Generic;
using Android.Net.Wifi.P2p;
using Android.Views;
using Android.Widget;
using Java.Lang;

namespace wifiptp
{
    public class ListAdapter : BaseAdapter
    {

        private List<WifiP2pDevice> devices;

        public ListAdapter()
        {
        }

        public override int Count {
            get {
                return devices.Count;
            }
        }

        public override Java.Lang.Object GetItem(int position)
        {
            return devices[position];
        }

        public override long GetItemId(int position)
        {
            return position;
        }

        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            throw new NotImplementedException();
        }
    }
}
