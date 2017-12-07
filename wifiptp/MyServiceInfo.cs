using System;
using Android.Content.PM;
using Java.Net;

namespace wifiptp
{
    public class MyServiceInfo : Java.Lang.Object
    {

        private string serviceName;

        public string ServiceName {
            get {
                return serviceName;
            }
        }

        private InetAddress host;

        public InetAddress Host {
            get {
                return host;
            }
        }

        private int port;

        public int Port {
            get {
                return port;
            }
        }

        public MyServiceInfo(string serviceName, InetAddress host, int port)
        {
            this.serviceName = serviceName;
            this.host = host;
            this.port = port;
        }

        public override string ToString()
        {
            return serviceName;
        }
    }
}
