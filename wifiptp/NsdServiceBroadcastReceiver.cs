
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
    public class NsdServiceBroadcastReceiver : BroadcastReceiver
    {
        public const string id = "NsdServiceBroadcastReceiver";

        private NsdServiceListener listener;

        public NsdServiceBroadcastReceiver(NsdServiceListener listener) {
            this.listener = listener;    
        }

        public override void OnReceive(Context context, Intent intent)
        {
            string action = intent.Action;

            if (action.Equals(ServerService.SERVICE_REGISTERED_ACTION)) {
                listener.OnServiceRegistered();
            } else if (action.Equals(ServerService.SERVICE_UNREGISTERED_ACTION)) {
                listener.OnServiceUnregistered();
            }
        }
    }
}
