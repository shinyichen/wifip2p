
using System;

using Android.App;
using Android.Content;
using Android.OS;

namespace wifiptp
{
    [Service(Label = "WiFiDirectService")]
    [IntentFilter(new String[] { "com.yourname.WiFiDirectService" })]
    public class WiFiDirectService : Service
    {
        IBinder binder;

        public override StartCommandResult OnStartCommand(Android.Content.Intent intent, StartCommandFlags flags, int startId)
        {
            // start your service logic here

            // Return the correct StartCommandResult for the type of service you are building
            return StartCommandResult.NotSticky;
        }

        public override IBinder OnBind(Intent intent)
        {
            binder = new WiFiDirectServiceBinder(this);
            return binder;
        }
    }

    public class WiFiDirectServiceBinder : Binder
    {
        readonly WiFiDirectService service;

        public WiFiDirectServiceBinder(WiFiDirectService service)
        {
            this.service = service;
        }

        public WiFiDirectService GetWiFiDirectService()
        {
            return service;
        }
    }
}
