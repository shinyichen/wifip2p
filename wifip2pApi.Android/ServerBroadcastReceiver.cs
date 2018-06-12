
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace wifip2pApi.Android
{
    [BroadcastReceiver]
    public class ServerBroadcastReceiver : BroadcastReceiver
    {

        public static string ACTION_CONNECTED = "wifip2pApi.android.connected";

        public static string ACTION_DISCONNECTED = "wifip2pApi.android.disconnected";

        public static string ACTION_FILE_RECEIVED = "wifip2pApi.android.received";

        public static string ACTION_STATUS_MESSAGE = "wifip2pApi.android.status";

        public static string EXTRA_MESSAGE = "message";

        private Action connectedAction, disconnectedAction, receivedAction;
        private Action<string> statusAction;

        public ServerBroadcastReceiver() { }

        public ServerBroadcastReceiver(Action connectedAction, Action disconnectedAction, Action receivedAction, Action<string> statusAction) {
            this.connectedAction = connectedAction;
            this.disconnectedAction = disconnectedAction;
            this.receivedAction = receivedAction;
            this.statusAction = statusAction;
        }

        public override void OnReceive(Context context, Intent intent)
        {
            string action = intent.Action;
            if (action == ACTION_CONNECTED) {
                connectedAction();
            } else if (action == ACTION_DISCONNECTED) {
                disconnectedAction();
            } else if (action == ACTION_FILE_RECEIVED) {
                receivedAction();
            } else if (action == ACTION_STATUS_MESSAGE) {
                string message = intent.GetStringExtra(EXTRA_MESSAGE);
                statusAction(message);
            }
        }
    }
}
