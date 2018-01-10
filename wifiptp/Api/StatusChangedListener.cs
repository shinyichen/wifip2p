using System;
using Android.Net.Nsd;

namespace wifiptp.Api
{
    public interface StatusChangedListener
    {
        void NsdRegistered(string serviceName);
        void NsdUnregistered();
        void NsdRegistrationFailed(Wifiptp.Error error);
        void NsdUnregistrationFailed(Wifiptp.Error error);

        void StartDiscoveryFailed(Wifiptp.Error error);
        void DiscoveryStarted();
        void StopDiscoveryFailed(Wifiptp.Error error);
        void DiscoveryStopped();

        void DeviceFound(NsdServiceInfo device);
        void DeviceLost(NsdServiceInfo device);

        void Connected(bool server);
        void Disconnected(bool server);
        void FilesReceived();
    }
}
