
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Net.Wifi.P2p;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using static Android.Net.Wifi.P2p.WifiP2pManager;

namespace wifiptp
{
    //[BroadcastReceiver(Enabled = true, Exported = false)]
    public class WiFiDirectBroadcastReceiver : BroadcastReceiver
    {
        private WifiP2pManager manager;
        private Channel channel;
        private MainActivity activity;

        public WiFiDirectBroadcastReceiver(WifiP2pManager manager, Channel channel, MainActivity activity)
        {
            this.manager = manager;
            this.channel = channel;
            this.activity = activity;
        }

        public override void OnReceive(Context context, Intent intent)
        {
            //Toast.MakeText(context, "Received intent!", ToastLength.Short).Show();
            string action = intent.Action;
            if (action.Equals(WifiP2pManager.WifiP2pStateChangedAction))
            {
                int state = intent.GetIntExtra(WifiP2pManager.ExtraWifiState, -1);
                if (state == WifiP2pState.Enabled.GetHashCode())
                {
                    // WIFI enabled
                }
                else
                {
                    // WIFI disabled
                }
            }
            else if (action.Equals(WifiP2pManager.WifiP2pPeersChangedAction))
            {
                // Call WifiP2pManager.requestPeers() to get a list of current peers
                // Discover was called and found
                if (manager != null)
                    manager.RequestPeers(channel, (IPeerListListener)activity);
            }
            else if (action.Equals(WifiP2pManager.WifiP2pConnectionChangedAction))
            {
                // Respond to new connection or disconnections
            }
            else if (action.Equals(WifiP2pManager.WifiP2pThisDeviceChangedAction))
            {
                // Respond to this device's wifi state changing
            }
        }
    }
}
