using System;
using Android.Content.PM;
using Java.Net;

namespace wifiptp
{
    public class MyServiceInfo : Java.Lang.Object
    {

        private string serviceName;

        private InetAddress host;

        private int port;

        public MyServiceInfo(string serviceName, InetAddress host, int port)
        {
            this.serviceName = serviceName;
            this.host = host;
            this.port = port;
        }

        public override string ToString()
        {
            return serviceName + "\n" + host + ":" + port;
        }
    }
}
