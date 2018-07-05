﻿using System;
using Android.Content;
using Android.Net.Nsd;
using Android.Net.Wifi;
using Android.Util;
using System.Collections.Generic;
using Android.Bluetooth;
using Android.OS;
using Java.Net;

namespace wifip2pApi.Android     
{
    public class Wifiptp : ITaskProgress
    {

        public const string ID = "WiFiPTP";

        public static string INTENT_FILE_RECEIVED = "file_received";

        private string serviceName;

        private string serviceType;

        private WiFiBroadcastReceiver wifiBroadcastReceiver;

        private ServerBroadcastReceiver serverBroadcastReceiver;

        private ClientBroadcastReceiver clientBroadcastReceiver;

        private WifiStatus wifiStatus;

        private SearchStatus searchStatus = SearchStatus.Stopped;

        private Context context;

        private StatusChangedListener statusListener;

        private NsdManager nsdManager;

        private ServerService serverService;

        private ServiceBindingCallback serviceBindingCallback;

        private NsdRegistrationListener nsdRegistrationListener;

        private ClientService clientService;

        private ClientBindingCallback clientBindingCallback;

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



        public Wifiptp(string service, Context context, StatusChangedListener listener)
        {
            // Use bluetooth name for device name
            deviceName = BluetoothAdapter.DefaultAdapter.Name;
            deviceName = deviceName.Replace(" ", "_");

            this.serviceName = service + "_" + deviceName;
            this.serviceType = "_" + service + "._tcp";
            this.context = context;
            this.statusListener = listener;
            nsdManager = (NsdManager)context.GetSystemService(Context.NsdService);

            // server service binding callback
            serviceBindingCallback = new ServiceBindingCallback((ComponentName name, IBinder ibinder) => {
                // connected
                ServerServiceBinder binder = (ServerServiceBinder)ibinder;
                serverService = binder.GetServerService();

            }, (ComponentName name) => {
                // disconnected
                serverService = null;
            });

            // client service binding callback
            clientBindingCallback = new ClientBindingCallback((ComponentName name, IBinder ibinder) => {
                // connected
                ClientServiceBinder binder = (ClientServiceBinder)ibinder;
                clientService = binder.GetClientService();

            }, (ComponentName name) => {
                // disconnected
                clientService = null;
            });

            // start server service
            startServerService();

            // init registration listener
            nsdRegistrationListener = new NsdRegistrationListener((NsdServiceInfo info) =>
            {
                // service registered
                myServiceInfo = info;
                nsdStatus = NsdStatus.Registered;
                statusListener.NsdRegistered(myServiceInfo.ServiceName);

            }, (NsdServiceInfo info) => // service unregistered
            {
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
            wifiBroadcastReceiver = new WiFiBroadcastReceiver(() =>
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
                    wifiStatus = WifiStatus.Connected;
                    startServerService();

                }
                else
                {
                    //  Wifi stopped during a transfer, while registered, or while searching
                    wifiStatus = WifiStatus.Disconnected;

                    // discovery stopped
                    if (searchStatus == SearchStatus.Searching)
                    {
                        searchStatus = SearchStatus.Stopped;
                        devices.Clear();
                        serviceNames.Clear();
                        statusListener.DiscoveryStopped();
                    }

                    // service unregistered
                    if (nsdStatus == NsdStatus.Registered)
                    {
                        myServiceInfo = null;
                        nsdStatus = NsdStatus.Unregistered;
                        statusListener.NsdUnregistered();
                    }

                    // assuming socket disconnects when WIFI is disconnected
                    if (serverService != null)
                    {
                        stopServerService(false);
                    }

                    // client task should automatically end when socket exception is caught


                }
            }, () =>
            {
                // disconnected
                wifiStatus = WifiStatus.Disconnected;

            });
            context.RegisterReceiver(wifiBroadcastReceiver, filter);

            // listen to server updates
            IntentFilter filter1 = new IntentFilter();
            filter1.AddAction(ServerBroadcastReceiver.ACTION_CONNECTED);
            filter1.AddAction(ServerBroadcastReceiver.ACTION_DISCONNECTED);
            filter1.AddAction(ServerBroadcastReceiver.ACTION_FILE_RECEIVED);
            filter1.AddAction(ServerBroadcastReceiver.ACTION_STATUS_MESSAGE);
            serverBroadcastReceiver = new ServerBroadcastReceiver(() =>
            {
                // connected
                statusListener.Connected(true);
            }, () =>
            {
                // disconnected
                statusListener.Disconnected(true);
            }, () =>
            {
                // file received
                statusListener.FilesReceived();
            }, (string message) =>
            {
                // status message
                statusListener.UpdateStatusMessage(message);
            });
            context.RegisterReceiver(serverBroadcastReceiver, filter1);

            // listen to client updates
            IntentFilter filter2 = new IntentFilter();
            filter2.AddAction(ClientBroadcastReceiver.ACTION_CONNECTED);
            filter2.AddAction(ClientBroadcastReceiver.ACTION_DISCONNECTED);
            filter2.AddAction(ClientBroadcastReceiver.ACTION_FILE_RECEIVED);
            filter2.AddAction(ClientBroadcastReceiver.ACTION_STATUS_MESSAGE);
            clientBroadcastReceiver = new ClientBroadcastReceiver(() =>
            {
                // connected
                statusListener.Connected(true);
            }, () =>
            {
                // disconnected
                statusListener.Disconnected(true);
            }, () =>
            {
                // file received
                statusListener.FilesReceived();
            }, (string message) =>
            {
                // status message
                statusListener.UpdateStatusMessage(message);
            });
            context.RegisterReceiver(clientBroadcastReceiver, filter2);
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

                    // register service
                    NsdServiceInfo serviceInfo = new NsdServiceInfo();
                    serviceInfo.ServiceName = serviceName;
                    serviceInfo.ServiceType = serviceType;
                    serviceInfo.Port = serverService.Port;

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


        public void sendFile(InetAddress address, int port, List<string> files) {

            // start client service
            Intent clientIntent = new Intent(context, typeof(ClientService));
            clientIntent.PutExtra(ClientService.EXTRA_ADDRESS, address);
            clientIntent.PutExtra(ClientService.EXTRA_PORT, port);
            clientIntent.PutStringArrayListExtra(ClientService.EXTRA_FILES, files);
            context.StartService(clientIntent);

            // bind context to client service
            Intent bindIntent = new Intent(context, typeof(ClientService));
            context.BindService(bindIntent, clientBindingCallback, Bind.None);
        }

        // manually force stop transfer
        public void interruptTransfer() {
            if (clientService != null) {
                // client task is running, stop it
                clientService.CloseConnectionImmediately();

            } else if (serverService != null) {
                // server task is running, stop it
                if (!serverService.IsListening)
                    serverService.CloseConnectionImmediately(); // mark close connection, will stop transfer, return server to listening mode
            }
        }

        public void shutdown() {
            // let client finishes itself
            // need to make server sevice stop immediately after task is finished
            if (serverService != null)
                serverService.stopService(true);
            context.UnregisterReceiver(wifiBroadcastReceiver);
            context.UnregisterReceiver(serverBroadcastReceiver);
            context.UnregisterReceiver(clientBroadcastReceiver);
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

        public void OnConnected(bool server) {
            statusListener.Connected(server);
        }

        public void OnFilesReceived() {
            statusListener.FilesReceived();
        }

        public void OnDisconnected(bool server)
        {
            statusListener.Disconnected(server);
        }

        public void OnStatusUpdate(string message)
        {
            statusListener.UpdateStatusMessage(message);
        }

        private void startServerService() {
            
            Intent serviceIntent = new Intent(context, typeof(ServerService));
            string dir = context.GetExternalFilesDir(null).AbsolutePath;
            serviceIntent.PutExtra(ServerService.EXTRA_FILE_DIR, dir);
            context.StartService(serviceIntent);

            // bind context to server service
            Intent bindIntent = new Intent(context, typeof(ServerService));
            context.BindService(bindIntent, serviceBindingCallback, Bind.None);
        }

        private void stopServerService(bool finishTransfer) {
            serverService.stopService(finishTransfer);
        }

    }

    public class ServiceBindingCallback : Java.Lang.Object, IServiceConnection {

        private Action<ComponentName, IBinder> connected;

        private Action<ComponentName> disconnected;

        public ServiceBindingCallback(Action<ComponentName, IBinder> connected, Action<ComponentName> disconnected) {
            this.connected = connected;
            this.disconnected = disconnected;
        }

        public void OnServiceConnected(ComponentName name, IBinder service)
        {
            connected(name, service);
        }

        public void OnServiceDisconnected(ComponentName name)
        {
            disconnected(name);
        }
    }

    public class ClientBindingCallback : Java.Lang.Object, IServiceConnection
    {

        private Action<ComponentName, IBinder> connected;

        private Action<ComponentName> disconnected;

        public ClientBindingCallback(Action<ComponentName, IBinder> connected, Action<ComponentName> disconnected)
        {
            this.connected = connected;
            this.disconnected = disconnected;
        }

        public void OnServiceConnected(ComponentName name, IBinder service)
        {
            connected(name, service);
        }

        public void OnServiceDisconnected(ComponentName name)
        {
            disconnected(name);
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
