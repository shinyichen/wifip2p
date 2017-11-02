
using System;
using System.IO;
using System.Net;
using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.Net;
using Android.Net.Nsd;
using Android.Net.Wifi;
using Android.OS;
using Android.Util;
using Java.Lang;
using Java.Net;

namespace wifiptp
{
    [Service(Label = "ServerService", Name = "edu.isi.wifiptp.ServerService")]
    [IntentFilter(new System.String[] { "edu.isi.wifiptp.ServerService" })]

	// This service starts at boots. It registers NSD service and create a server socket and listen to incoming connection.
	public class ServerService : IntentService
    {

        // TODO what to do when no wifi connected
        // TODO what to do when disconnected, or switched to different WIFI
        // Register only when connected
        // What happens to NSD registration when WIFI is suddenly turned off

		private const string id = "ServerService";

        private ServerSocket serverSocket;

        private int port;

        private string deviceName;

        private NsdManager nsdManager;

        public static readonly string SERVICE_REGISTERED_ACTION = "edu.isi.backpack.android.SERVICE_REGISTERED_ACTION";

        public static readonly string SERVICE_UNREGISTERED_ACTION = "edu.isi.backpack.android.SERVICE_UNREGISTERED_ACTION";

		public NsdManager NsdManager
		{
			get
			{
				return nsdManager;
			}
		}

		private Stream inputStream = null, outputStream = null;
		
        private FileStream fileStream = null;
		
        private FileStream imageFileStream = null;
		
        private Java.IO.File imageDir = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryPictures);

        private byte[] buf = new byte[1024];

        private NsdServiceInfo myServiceInfo = null;
        public NsdServiceInfo MyServiceInfo {
            get {
                return myServiceInfo;
            }
        }

        private NsdRegistrationListener nsdRegistrationListener;

        private static int NSD_UNREGISTERED = 0;

        private static int NSD_REGISTERING = 1;

        private static int NSD_REGISTERED = 2;

        private static int NSD_UNREGISTERING = 3;

        private int nsdStatus = NSD_UNREGISTERED;

        public bool ServiceRegistered {
            get {
                return nsdStatus == NSD_REGISTERED;
            }
        }

        private System.Threading.Thread socketThread;


		IBinder binder;

        public override void OnCreate()
        {
            base.OnCreate();

			Log.Info(id, "Starting Server Service");

            // Use bluetooth name for device name
            deviceName = BluetoothAdapter.DefaultAdapter.Name;
            deviceName = deviceName.Replace(" ", "_");

            Log.Info(id, "Get NSD Manager");
            nsdManager = (NsdManager)GetSystemService(NsdService);

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

                // connected ( -1 is disconnected)
                if (networkId != -1)
                {
                    if (nsdStatus == NSD_UNREGISTERED || nsdStatus == NSD_UNREGISTERING)
                    {
                        // Initialize a server socket on the next available port.
                        serverSocket = new ServerSocket(0);
                        port = serverSocket.LocalPort;

                        // register service
                        NsdServiceInfo serviceInfo = new NsdServiceInfo();
                        serviceInfo.ServiceName = "Backpack_" + deviceName;
                        serviceInfo.ServiceType = "_backpack._tcp";
                        serviceInfo.Port = port;

                        nsdRegistrationListener = new NsdRegistrationListener((NsdServiceInfo info) =>
                        {
                            // service registered
                            myServiceInfo = info;
                            nsdStatus = NSD_REGISTERED;
                            Intent i = new Intent(SERVICE_REGISTERED_ACTION);
                            SendBroadcast(i);
                        }, (NsdServiceInfo info) =>
                        {
                            myServiceInfo = null;
                            nsdStatus = NSD_UNREGISTERED;
                            Intent i = new Intent(SERVICE_UNREGISTERED_ACTION);
                            SendBroadcast(i);
                        });

                        Log.Debug(id, "NSD Registration");
                        nsdManager.RegisterService(serviceInfo, NsdProtocol.DnsSd, nsdRegistrationListener);
                        nsdStatus = NSD_REGISTERING;

                        socketThread = startSocketThread();
                    }
                }
                else
                {
                    // same as disconnected
                    // if registered, unregister
                    // close socket and kill thread
                    if (nsdStatus == NSD_REGISTERED)
                    {
                        unregisterService();
                        Log.Debug(id, "Closing socket");
                        serverSocket.Close(); // TODO check: thread should auto finish
                        //socketThread.Interrupt();
                    }

                }
            }, () =>
            {
                // disconnected
                // close server socket thread & unregister service
                // TODO
                if (nsdStatus == NSD_REGISTERED)
                {
                    unregisterService();
                    Log.Debug(id, "Closing socket");
                    serverSocket.Close();  // TODO check: thread should auto finish
                    //socketThread.Interrupt();

                }
            });
            RegisterReceiver(receiver, filter);

            // the abouve will check connectivity status
            // if connected to WIFI, register NSD
            // if not, wait till connected
            // it also receives at start of service
			
        }

        private void unregisterService() {
            Log.Debug(id, "Unregister service");
            nsdManager.UnregisterService(nsdRegistrationListener);
            nsdStatus = NSD_UNREGISTERING;
        }

		public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
		{

			// constant running service
			return StartCommandResult.Sticky;
		}

        public override IBinder OnBind(Intent intent)
        {
            binder = new ServerServiceBinder(this);
            return binder;
        }

        // this is called when user swipe kill the app
        public override void OnTaskRemoved(Intent rootIntent)
        {

            if (nsdStatus == NSD_REGISTERED)
            {
                unregisterService();
            }

            // restart service
            Log.Debug(id, "Try to restart service");
            //Intent restartServiceTask = new Intent(this.ApplicationContext, this.Class);
            //restartServiceTask.SetPackage(this.PackageName);
            PendingIntent restartPendingIntent = PendingIntent.GetService(
                this.ApplicationContext,
                1001, 
                new Intent(this.ApplicationContext, this.Class),
                PendingIntentFlags.OneShot);
            AlarmManager alarmManager = (AlarmManager)this.ApplicationContext.GetSystemService(AlarmService);
            alarmManager.Set(AlarmType.ElapsedRealtime, SystemClock.ElapsedRealtime() + 1000, restartPendingIntent);

            base.OnTaskRemoved(rootIntent);
        }

        public override void OnDestroy()
        {
            Log.Debug(id, "onDestroy");
            base.OnDestroy();
        }

        protected override void OnHandleIntent(Intent intent)
        {
            throw new NotImplementedException();
        }

        public class NsdRegistrationListener : Java.Lang.Object, NsdManager.IRegistrationListener
		{

			private Action<NsdServiceInfo> onRegisteredAction, onUnregisteredAction;

            public NsdRegistrationListener(Action<NsdServiceInfo> onRegisteredAction, Action<NsdServiceInfo> onUnregisteredAction)
			{
				this.onRegisteredAction = onRegisteredAction;
                this.onUnregisteredAction = onUnregisteredAction;
			}

			public void OnRegistrationFailed(NsdServiceInfo serviceInfo, NsdFailure errorCode)
			{
				Log.Debug(id, "NSD Registration Failed");
			}

			public void OnServiceRegistered(NsdServiceInfo serviceInfo)
			{
                Log.Debug(id, "NSD Service Registered: " + serviceInfo.ServiceName);
				onRegisteredAction(serviceInfo);
			}

			public void OnServiceUnregistered(NsdServiceInfo serviceInfo)
			{
				Log.Debug(id, "NSD Service Unregistered");
                onUnregisteredAction(serviceInfo);
			}

			public void OnUnregistrationFailed(NsdServiceInfo serviceInfo, NsdFailure errorCode)
			{
				Log.Debug(id, "NSD Unregistration Failed");
			}
		}

        //public bool connectedToWiFi()
        //{
        //    ConnectivityManager cm = (ConnectivityManager) GetSystemService(ConnectivityService);
        //    NetworkInfo info = cm.ActiveNetworkInfo;
        //    if (info != null && info.Type == ConnectivityType.Wifi) {
        //        return true;
        //    }
        //    return false;
        //}


        // Start a separate thread for socket
        private System.Threading.Thread startSocketThread() {

            System.Threading.Thread t = new System.Threading.Thread(new System.Threading.ThreadStart(() =>
            {
                

                while (true)
                {
                    Log.Info("Server", "Socket thread running, waiting for incoming connection");

                    // wait for client connection
                    if (serverSocket.IsClosed)
                    {
                        serverSocket = new ServerSocket(port);
                    }

                    try
                    {
                        Socket client = serverSocket.Accept();

                        Log.Info("Server", "Received incoming connection ");
                        inputStream = client.InputStream;
                        outputStream = client.OutputStream;

                        // 1. server receives file first

                        // 1.1 receive file size (as long) from client
                        Log.Info("Server", "Receiving file size from client");
                        inputStream.Read(buf, 0, sizeof(long));
                        long size = BitConverter.ToInt64(buf, 0);
                        Log.Info("Server", "Expecting file size: " + size + " bytes");

                        // 1.2 receive image from client
                        if (size > 0)
                        {
                            Log.Info("Server", "Receiving file from client");
                            string fileName = "wifip2p - " + JavaSystem.CurrentTimeMillis() + ".jpg";
                            Java.IO.File imageFile = new Java.IO.File(imageDir, fileName);
                            imageFile.CreateNewFile();
                            imageFileStream = new FileStream(imageDir + "/" + fileName, FileMode.Create, FileAccess.Write, FileShare.None);
                            Utils.CopyStream(inputStream, imageFileStream, size);
                            imageFileStream.Flush();

                            Log.Info("Server", "Received file length: " + imageFileStream.Length);
                            Log.Info("Server", "Write to file length: " + imageFile.Length());
                        }

                        // 2. server send file

                        if (File.Exists(imageDir.AbsolutePath + "/image.jpg"))
                        {

                            // 2.1 send file size
                            Log.Info(id, "Sending file size to client");
                            fileStream = new FileStream(imageDir.AbsolutePath + "/image.jpg", FileMode.Open, FileAccess.Read);
                            byte[] sizeData = BitConverter.GetBytes(fileStream.Length);
                            outputStream.Write(sizeData, 0, sizeof(long));

                            // 2.2 send file
                            Log.Info(id, "Sending file to client");
                            buf = new byte[fileStream.Length];
                            int bytesToRead = (int)fileStream.Length;
                            int bytesRead = 0;

                            do
                            {
                                int r = 1024;
                                if (bytesToRead < 1024)
                                    r = bytesToRead;
                                int len = fileStream.Read(buf, 0, r);

                                // send
                                outputStream.Write(buf, 0, len);
                                outputStream.Flush();

                                bytesRead += len;
                                bytesToRead -= len;
                            } while (bytesToRead > 0);

                            Log.Info(id, bytesRead + " bytes sent");
                        }
                        else
                        { // no file to send

                            // 2.1 send file size 0
                            Log.Info(id, "No image file. Send size 0 to server");
                            byte[] sizeData = BitConverter.GetBytes((long)0);
                            outputStream.Write(sizeData, 0, sizeof(long));
                        }

                        // wait until clinet response 
                        Log.Info(id, "Wait for client to finish");
                        while (!inputStream.IsDataAvailable()) { }

                        if (outputStream != null)
                            outputStream.Close();
                        if (inputStream != null)
                            inputStream.Close();
                        if (fileStream != null)
                            fileStream.Close();
                        if (imageFileStream != null)
                            imageFileStream.Close();

                        serverSocket.Close();
                    } catch (SocketException) {
                        // close() called from another thread
                        // exit the while loop and return
                        break;
                    }

                }

                Log.Info(id, "Socket thread finished");

            }));

            t.Start();

            return t;
        }
    }

    public class ServerServiceBinder : Binder
    {
        readonly ServerService service;

        public ServerServiceBinder(ServerService service)
        {
            this.service = service;
        }

        public ServerService GetServerService()
        {
            return service;
        }
    }
}
