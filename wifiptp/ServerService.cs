
using System;
using System.IO;
using Android.App;
using Android.Content;
using Android.Net.Nsd;
using Android.OS;
using Android.Util;
using Java.Lang;
using Java.Net;

namespace wifiptp
{
    [Service(Label = "ServerService")]
    [IntentFilter(new System.String[] { "com.yourname.ServerService" })]

	// This service starts at boots. It registers NSD service and create a server socket and listen to incoming connection.
	public class ServerService : IntentService
    {

		private const string id = "ServerService";

        private ServerSocket serverSocket;

        private int port;

        private NsdManager nsdManager;

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

        private string myServiceName;
        public string MyServiceName {
            get {
                return myServiceName;
            }
        }

        private NsdRegistrationListener nsdRegistrationListener;

		IBinder binder;


		public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
		{

			Log.Info(id, "Starting Server Service");

			// Initialize a server socket on the next available port.
			serverSocket = new ServerSocket(0);
            port = serverSocket.LocalPort;

            // Register service
			NsdServiceInfo serviceInfo = new NsdServiceInfo();
			serviceInfo.ServiceName = "Backpack";
			serviceInfo.ServiceType = "_backpack._tcp";
			serviceInfo.Port = port;

			Log.Debug(id, "NSD Registration");
			nsdManager = (NsdManager)GetSystemService(Context.NsdService);
            nsdRegistrationListener = new NsdRegistrationListener((NsdServiceInfo info) =>
            {
                myServiceName = info.ServiceName;
            });
            nsdManager.RegisterService(serviceInfo, NsdProtocol.DnsSd, nsdRegistrationListener);

            // Start a separate thread for socket
            new System.Threading.Thread(new System.Threading.ThreadStart(() =>
            {
                while(true) {
					Log.Info("Server", "Server thread running, waiting for incoming connection");
					
					// wait for client connection
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

				}

            })).Start();

			// constant running service
			return StartCommandResult.Sticky;
		}

        protected override void OnHandleIntent(Intent intent)
        {
            // Perform your service logic here
        }

        public override IBinder OnBind(Intent intent)
        {
            binder = new ServerServiceBinder(this);
            return binder;
        }

        public override void OnDestroy()
        {
            Log.Debug(id, "Unregister service");
            nsdManager.UnregisterService(nsdRegistrationListener);
            base.OnDestroy();

        }

		public class NsdRegistrationListener : Java.Lang.Object, NsdManager.IRegistrationListener
		{

			private Action<NsdServiceInfo> onRegisteredAction;

			public NsdRegistrationListener(Action<NsdServiceInfo> onRegisteredAction)
			{
				this.onRegisteredAction = onRegisteredAction;
			}

			public void OnRegistrationFailed(NsdServiceInfo serviceInfo, NsdFailure errorCode)
			{
				Log.Debug(id, "NSD Registration Failed");
			}

			public void OnServiceRegistered(NsdServiceInfo serviceInfo)
			{
				Log.Debug(id, "NSD Service Registered");
				onRegisteredAction(serviceInfo);
			}

			public void OnServiceUnregistered(NsdServiceInfo serviceInfo)
			{
				Log.Debug(id, "NSD Service Unregistered");
			}

			public void OnUnregistrationFailed(NsdServiceInfo serviceInfo, NsdFailure errorCode)
			{
				Log.Debug(id, "NSD Unregistration Failed");
			}
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
