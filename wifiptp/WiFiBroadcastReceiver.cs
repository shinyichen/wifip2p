
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Net;
using Android.Net.Wifi;
using Android.Net.Wifi.P2p;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace wifiptp
{
    public class WiFiBroadcastReceiver : BroadcastReceiver
    {
        private string id = "WifiBroadcastReceiver";
        private Action enabling, enabled, disabling, disabled, disconnected;
        private Action<int> connected;

        public WiFiBroadcastReceiver(Action enabling, Action enabled, Action disabling, Action disabled, Action<int> connected, Action disconnected) {
            this.enabling = enabling;
            this.enabled = enabled;
            this.disabling = disabling;
            this.disabled = disabled;
            this.connected = connected;
            this.disconnected = disconnected;
        }
        public override void OnReceive(Context context, Intent intent)
        {
            string action = intent.Action;
            if (action.Equals(WifiManager.WifiStateChangedAction)) {
                int state = intent.GetIntExtra(WifiManager.ExtraWifiState, -1);
                if (state == WifiState.Disabled.GetHashCode()) {
                    //Log.Info(id, "Wifi Disabled");
                    disabled();
                } else if (state == WifiState.Disabling.GetHashCode()) {
                    //Log.Info(id, "Wifi Disabling");
                    disabling();
                } else if (state == WifiState.Enabled.GetHashCode()) {
                    //Log.Info(id, "Wifi Enabled");
                    enabled();
                } else if (state == WifiState.Enabling.GetHashCode()) {
                    //Log.Info(id, "Wifi Enabling");
                    enabling();
                } else {
                    // unknown state
                }
            } else if (action.Equals(WifiManager.NetworkStateChangedAction)) {
                NetworkInfo nwInfo = (NetworkInfo)intent.GetParcelableExtra(WifiManager.ExtraNetworkInfo);
                WifiInfo wifiInfo = (WifiInfo)intent.GetParcelableExtra(WifiManager.ExtraWifiInfo);
                if (nwInfo.IsConnected) {
                    Log.Info(id, "Wifi Connected: " + wifiInfo.NetworkId);
                    connected(wifiInfo.NetworkId);
                } else {
                    Log.Info(id, "Wifi Disconnected");
                    disconnected();
                }

            }
        }
    }
}
