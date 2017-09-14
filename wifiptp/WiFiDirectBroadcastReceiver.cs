using Android.Content;
using Android.Net;
using Android.Net.Wifi.P2p;
using Android.Util;
using Android.Widget;
using static Android.Net.Wifi.P2p.WifiP2pManager;
using static wifiptp.MainActivity;

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
                //Toast.MakeText(activity, "Request peers", ToastLength.Short).Show();
                //if (manager != null)
                    //manager.RequestPeers(channel, (IPeerListListener)activity);
            }
            else if (action.Equals(WifiP2pManager.WifiP2pConnectionChangedAction))
            {
                // connection established, start file server task
                NetworkInfo info = (NetworkInfo)intent.GetParcelableExtra(WifiP2pManager.ExtraNetworkInfo);
                if (info.IsConnected)
                {
					Log.Info("WifiDirectBroadcastReceiver", "connection established, requesting connection info");
                    manager.RequestConnectionInfo(channel, new ConnectionInfoAvailableListener(context, manager, channel, MainActivity.port));
                } else {
                    Log.Info("WifiDirectBroadcastReceiver", "disconnected");
                }
            }
            else if (action.Equals(WifiP2pManager.WifiP2pThisDeviceChangedAction))
            {
                // Respond to this device's wifi state changing
            }
        }
    }
}
