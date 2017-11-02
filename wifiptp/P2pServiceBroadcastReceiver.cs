
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace wifiptp
{
    //[BroadcastReceiver]
    public class P2pServiceBroadcastReceiver : BroadcastReceiver
    {
        public const string id = "P2pServiceBroadcastReceiver";

        private P2pServiceListener listener;

        public P2pServiceBroadcastReceiver(P2pServiceListener listener) {
            this.listener = listener;    
        }

        public override void OnReceive(Context context, Intent intent)
        {
            string action = intent.Action;

            if (action.Equals(P2pService.DEVICES_CHANGED)) {
                Log.Info(id, "new device");
                listener.OnDevicesChanged();
            } else if (action.Equals(P2pService.DISCOVERY_STARTED_ACTION)) {
                Log.Info(id, "discovery started");
                listener.OnDiscoveryStarted();
            } else if (action.Equals(P2pService.DISCOVERY_COMPLETED_ACTION)) {
                Log.Info(id, "discovery ended");
                listener.OnDiscoveryStopped();
            } else if (action.Equals(P2pService.CONNECTION_ESTABLISHED_ACTION)) {
                Log.Info(id, "conenction established");
                listener.OnConnectionStarted();
            } else if (action.Equals(P2pService.CONNECTION_CLOSED_ACTION)) {
                Log.Info(id, "connection closed");
                listener.OnConnectionClosed();
            } else if (action.Equals(ServerService.SERVICE_REGISTERED_ACTION)) {
                listener.OnServiceRegistered();
            } else if (action.Equals(ServerService.SERVICE_UNREGISTERED_ACTION)) {
                listener.OnServiceUnregistered();
            }
        }
    }
}
