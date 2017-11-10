using System;
using Android.Net.Wifi.P2p;

namespace wifiptp
{
    public interface NsdServiceListener
    {
        // for NSD
        void OnServiceRegistered();
        void OnServiceUnregistered();
    }
}
