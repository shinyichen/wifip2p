using System;
using Android.Content;
using wifiptp.Api;
using Android.Net.Nsd;
using Java.Net;
using Android.Net.Wifi;
using Android.Util;
using System.Collections.Generic;
using Android.Bluetooth;
using Android.OS;

namespace wifiptp
{
    public class Wifiptp : ITaskCompleted
    {

        public const string ID = "WiFiPTP";

        private string serviceName;

        private string serviceType;

        private WifiStatus wifiStatus;

        private SearchStatus searchStatus = SearchStatus.Stopped;

        private Context context;

        private StatusChangedListener statusListener;

        private NsdManager nsdManager;

        private ServerSocket serverSocket;

        private int port;

        private NsdRegistrationListener nsdRegistrationListener;

        private ServerAsyncTask serverTask;

        private NsdDiscoveryListener nsdDiscoveryListener;

        private NsdServiceInfo myServiceInfo;

        private NsdStatus nsdStatus = NsdStatus.Unregistered;

        private string deviceName;

        private List<NsdServiceInfo> devices = new List<NsdServiceInfo>();

        private List<string> serviceNames = new List<string>();

        public enum WifiStatus
        {
            Connected, Disconnected
        };

        public enum SearchStatus
        {
            Searching, Stopped
        };

        public enum NsdStatus
        {
            Registered, Registering, Unregistered, Unregistering
        };

        public enum Error
        {
            NoWifi, NsdNotRegistered, AlreadyActive, InternalError, FailureMaxLimit, Other
        };

        public Wifiptp(string serviceName, Context context, StatusChangedListener listener)
        {
            // Use bluetooth name for device name
            deviceName = BluetoothAdapter.DefaultAdapter.Name;
            deviceName = deviceName.Replace(" ", "_");

            this.serviceName = serviceName + "_" + deviceName;
            this.serviceType = "_" + serviceName + "._tcp";
            this.context = context;
            this.statusListener = listener;
            nsdManager = (NsdManager)context.GetSystemService(Context.NsdService);

            // init registration listener
            nsdRegistrationListener = new NsdRegistrationListener((NsdServiceInfo info) =>
            {
                // start server task, this will be listener for incoming connection
                serverTask = new ServerAsyncTask(serverSocket);
                serverTask.Execute();

                // service registered
                myServiceInfo = info;
                nsdStatus = NsdStatus.Registered;
                statusListener.NsdRegistered(myServiceInfo.ServiceName);

            }, (NsdServiceInfo info) => // service unregistered
            {

                // socket could be 1) listening 2) transfering files
                if (serverTask != null && !serverTask.GetStatus().Equals(AsyncTask.Status.Finished))
                {
                    // must keep these two line in this sequence
                    // if socket is just listening, close the socket and handle onCanceled
                    // else if files are being transfered, wait until task reaches the end to handle onCanceled
                    serverTask.Cancel(true); // mark interruption
                    if (serverTask.IsListening)
                        serverSocket.Close();    // interrupt the listening socket
                    // else let transfer finish 
                }

                // service unregistered
                myServiceInfo = null;
                nsdStatus = NsdStatus.Unregistered;
                statusListener.NsdUnregistered();
            }, (NsdFailure error) => {

                // registration failed
                statusListener.NsdRegistrationFailed(NsdFailureToError(error));
            }, (NsdFailure error) => {

                // unregistration failed
                statusListener.NsdUnregistrationFailed(NsdFailureToError(error));
            });

            // init discovery listener
            nsdDiscoveryListener = new NsdDiscoveryListener(nsdManager, () => {
                
                // discovery started
                searchStatus = SearchStatus.Searching;
                statusListener.DiscoveryStarted();

            }, () => {
                
                // discovery stopped
                searchStatus = SearchStatus.Stopped;
                devices.Clear();
                serviceNames.Clear();
                statusListener.DiscoveryStopped();

            }, (NsdServiceInfo info) =>
            {
                // found new device -> resolve
                string sName = info.ServiceName;

                // don't process duplicates
                if (!sName.Equals(myServiceInfo.ServiceName))
                {
                    resolveService(info);
                }
            }, (NsdServiceInfo info) => {
                
                // device lost, remove device
                for (int i = 0; i < devices.Count; i++)
                {
                    NsdServiceInfo d = devices[i];
                    if (d.ServiceName.Equals(info.ServiceName))
                    {
                        devices.RemoveAt(i);
                        serviceNames.RemoveAt(i);
                        statusListener.DeviceLost(d);
                        break;
                    }
                }
            }, (NsdFailure error) => {

                // on start discovery failed
                statusListener.StartDiscoveryFailed(NsdFailureToError(error));
                nsdManager.StopServiceDiscovery(nsdDiscoveryListener);

            }, (NsdFailure error) => {

                // on stop discovery failed
                statusListener.StopDiscoveryFailed(NsdFailureToError(error));
                nsdManager.StopServiceDiscovery(nsdDiscoveryListener);
            });

            // listen to wifi state changes
            IntentFilter filter = new IntentFilter();
            filter.AddAction(WifiManager.WifiStateChangedAction);
            filter.AddAction(WifiManager.NetworkStateChangedAction);
            WiFiBroadcastReceiver receiver = new WiFiBroadcastReceiver(() =>
            {
                // enabling
            }, () =>
            {
                // enabled
            }, () =>
            {
                // disabling
            }, () =>
            {
                // disabled
            }, (int networkId) =>
            {

                // connected
                if (networkId != -1)
                {
                    // TODO 
                    wifiStatus = WifiStatus.Connected;

                }
                else
                {
                    // TODO handle during a transfer, while registered, or while searching
                    wifiStatus = WifiStatus.Disconnected;

                }
            }, () =>
            {
                // disconnected
                wifiStatus = WifiStatus.Disconnected;

            });
            context.RegisterReceiver(receiver, filter);
        }

        public SearchStatus getSearchStatus() {
            return searchStatus;
        }


        public void setDiscoverable(bool d)
        {
            // turn on
            // Make this device discoverable by registering NSD service
            if (d)
            {
                // if NSD is not registered & wifi is connected
                if (nsdStatus == NsdStatus.Unregistered && wifiStatus == WifiStatus.Connected) {
                    
                    // Initialize a server socket on the next available port.
                    serverSocket = new ServerSocket(0);
                    port = serverSocket.LocalPort;

                    // register service
                    NsdServiceInfo serviceInfo = new NsdServiceInfo();
                    serviceInfo.ServiceName = serviceName + "_" + deviceName;
                    serviceInfo.ServiceType = serviceType;
                    serviceInfo.Port = port;

                    nsdStatus = NsdStatus.Registering;
                    nsdManager.RegisterService(serviceInfo, NsdProtocol.DnsSd, nsdRegistrationListener);

                    // create server task that listens to incoming connection after reigstration is successful

                } else if (wifiStatus == WifiStatus.Disconnected) { // wifi not connected, throw error
                    
                    statusListener.NsdRegistrationFailed(Error.NoWifi);
                }

            } else  // turn off, make this device undiscoverable by unregistering NSD service
            {

                // stop searching
                if (searchStatus == SearchStatus.Searching) {
                    stopDiscoverServices();
                }

                if (nsdStatus == NsdStatus.Registered)
                {
                    nsdStatus = NsdStatus.Unregistering;
                    nsdManager.UnregisterService(nsdRegistrationListener);
                    Log.Debug(ID, "Closing socket");
                    serverSocket.Close();
                }
            }
                
        }

        public void startDiscoverServices() {

            // start discovery if current registered and not searching
            if (nsdStatus == NsdStatus.Unregistered) {
                statusListener.StartDiscoveryFailed(Error.NsdNotRegistered);
            }
            else if (searchStatus == SearchStatus.Stopped)
            {
                nsdManager.DiscoverServices(serviceType, NsdProtocol.DnsSd, nsdDiscoveryListener);

            }
        }

        public void stopDiscoverServices() {

            // stop discovery if currently searching
            if (searchStatus == SearchStatus.Searching) {
                nsdManager.StopServiceDiscovery(nsdDiscoveryListener);
            }

        }

        public List<NsdServiceInfo> getDevices() {
            return devices;
        }

        private void resolveService(NsdServiceInfo info)
        {
            Log.Debug(ID, "Resolve service: " + info.ServiceName);
            nsdManager.ResolveService(info, new ServiceResolvedListener((NsdServiceInfo info1) =>
            {
                Log.Debug(ID, "Service resolved: " + info1.ServiceName);
                if (!serviceNames.Contains(info1.ToString()))
                {
                    devices.Add(info1);
                    serviceNames.Add(info1.ToString());
                    statusListener.DeviceFound(info1);
                }
            }, (NsdServiceInfo info1) =>
            {
                // resolve failed, try again
                resolveService(info1);
            }));
        }


        public void sendFile(InetAddress host, int port, List<string> filePaths) {

            ClientAsyncTask clientTask = new ClientAsyncTask(host, port, filePaths, this);
            clientTask.ExecuteOnExecutor(AsyncTask.ThreadPoolExecutor); // b/c already one asynctask running
        }

        private Error NsdFailureToError(NsdFailure f) {
            switch (f) {
                case NsdFailure.MaxLimit:
                    return Error.FailureMaxLimit;
                case NsdFailure.AlreadyActive:
                    return Error.AlreadyActive;
                case NsdFailure.InternalError:
                    return Error.InternalError;
                default:
                    return Error.Other;
            }
        }

        // client asyn completed
        public void OnTaskCompleted()
        {
            // notify
            statusListener.FileSent();
        }
    }


    public class NsdRegistrationListener : Java.Lang.Object, NsdManager.IRegistrationListener
    {

        private Action<NsdFailure> onRegistrationFailed, onUnregistrationFailed;
        private Action<NsdServiceInfo> onRegisteredAction, onUnregisteredAction;

        public NsdRegistrationListener(Action<NsdServiceInfo> onRegisteredAction, 
                                       Action<NsdServiceInfo> onUnregisteredAction,
                                       Action<NsdFailure> onRegistrationFailed, 
                                       Action<NsdFailure> onUnregistrationFailed)
        {
            this.onRegisteredAction = onRegisteredAction;
            this.onUnregisteredAction = onUnregisteredAction;
            this.onRegistrationFailed = onRegistrationFailed;
            this.onUnregistrationFailed = onUnregistrationFailed;
        }

        public void OnRegistrationFailed(NsdServiceInfo serviceInfo, NsdFailure errorCode)
        {
            Log.Debug(Wifiptp.ID, "NSD Registration Failed");
            onRegistrationFailed(errorCode);
        }

        public void OnServiceRegistered(NsdServiceInfo serviceInfo)
        {
            Log.Debug(Wifiptp.ID, "NSD Service Registered: " + serviceInfo.ServiceName);
            onRegisteredAction(serviceInfo);
        }

        public void OnServiceUnregistered(NsdServiceInfo serviceInfo)
        {
            Log.Debug(Wifiptp.ID, "NSD Service Unregistered");
            onUnregisteredAction(serviceInfo);
        }

        public void OnUnregistrationFailed(NsdServiceInfo serviceInfo, NsdFailure errorCode)
        {
            Log.Debug(Wifiptp.ID, "NSD Unregistration Failed");
            onUnregistrationFailed(errorCode);
        }
    }

    public class NsdDiscoveryListener : Java.Lang.Object, NsdManager.IDiscoveryListener
    {

        private NsdManager nsdManager;
        private Action onDiscoveryStartedAction;
        private Action onDiscoveryStoppedAction;
        private Action<NsdServiceInfo> onServiceFoundAction;
        private Action<NsdServiceInfo> onServiceLostAction;
        private Action<NsdFailure> onStartDiscoveryFailed, onStopDiscoveryFailed;

        public NsdDiscoveryListener(NsdManager nsdManager, 
                                    Action onDiscoveryStartedAction, 
                                    Action onDiscoveryStoppedAction, 
                                    Action<NsdServiceInfo> onServiceFoundAction, 
                                    Action<NsdServiceInfo> onServiceLostAction,
                                    Action<NsdFailure> onStartDiscoveryFailed,
                                    Action<NsdFailure> onStopDiscoveryFailed)
        {
            this.nsdManager = nsdManager;
            this.onDiscoveryStartedAction = onDiscoveryStartedAction;
            this.onDiscoveryStoppedAction = onDiscoveryStoppedAction;
            this.onServiceFoundAction = onServiceFoundAction;
            this.onServiceLostAction = onServiceLostAction;
            this.onStartDiscoveryFailed = onStartDiscoveryFailed;
            this.onStopDiscoveryFailed = onStopDiscoveryFailed;
        }

        public void OnDiscoveryStarted(string serviceType)
        {
            Log.Debug(Wifiptp.ID, "Nsd Discovery Started");
            onDiscoveryStartedAction();
        }

        public void OnDiscoveryStopped(string serviceType)
        {
            Log.Debug(Wifiptp.ID, "Nsd Discovery Stopped");
            onDiscoveryStoppedAction();
        }

        public void OnServiceFound(NsdServiceInfo serviceInfo)
        {
            Log.Debug(Wifiptp.ID, "Service Found: " + serviceInfo);
            onServiceFoundAction(serviceInfo);
        }

        public void OnServiceLost(NsdServiceInfo serviceInfo)
        {
            Log.Debug(Wifiptp.ID, "Service Lost: " + serviceInfo);
            onServiceLostAction(serviceInfo);
        }

        public void OnStartDiscoveryFailed(string serviceType, NsdFailure errorCode)
        {
            Log.Debug(Wifiptp.ID, "On Start Discovery Failed: " + errorCode.ToString());
            nsdManager.StopServiceDiscovery(this);
        }

        public void OnStopDiscoveryFailed(string serviceType, NsdFailure errorCode)
        {
            Log.Debug(Wifiptp.ID, "On Stop Discovery Failed: " + errorCode.ToString());
            nsdManager.StopServiceDiscovery(this);
        }
    }

    public class ServiceResolvedListener : Java.Lang.Object, NsdManager.IResolveListener
    {
        private Action<NsdServiceInfo> serviceResolvedAction, resolveFailedAction;

        public ServiceResolvedListener(Action<NsdServiceInfo> serviceResolvedAction, Action<NsdServiceInfo> resolveFailedAction)
        {
            this.serviceResolvedAction = serviceResolvedAction;
            this.resolveFailedAction = resolveFailedAction;
        }
        public void OnResolveFailed(NsdServiceInfo serviceInfo, NsdFailure errorCode)
        {
            Log.Error(Wifiptp.ID, "Resolve " + serviceInfo.ServiceName + " Failed: " + errorCode.ToString());
            resolveFailedAction(serviceInfo);
        }

        public void OnServiceResolved(NsdServiceInfo serviceInfo)
        {
            Log.Debug(Wifiptp.ID, "Service Resolved: " + serviceInfo);
            serviceResolvedAction(serviceInfo);
        }
    }
}
