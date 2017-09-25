using System;
using Android.Net.Wifi.P2p;

namespace wifiptp
{
    public interface P2pServiceListener
    {
        void OnDevicesChanged();
        void OnDiscoveryStarted();
        void OnDiscoveryStopped();
        void OnConnectionStarted(); // socket connection
        void OnConnectionClosed();
    }
}
