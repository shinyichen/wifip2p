
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Util;
using Java.IO;
using Java.Net;

namespace wifip2pApi.Android
{
    [Service(Label = "ServerService")]
    [IntentFilter(new String[] { "wifip2pApi.Android.ServerService" })]
    public class ServerService : Service
    {
        IBinder binder;

        public static string EXTRA_FILE_DIR = "FileDirectory";

        private const string id = "ServerService";

        private bool hasStarted = false;

        private FileStream outFileStream = null;

        private byte[] buf = new byte[65936];

        private bool isListening = false;

        private ServerSocket serverSocket;

        private string fileDirectory;

        public bool IsListening
        {
            get
            {
                return isListening;
            }
        }

        private int port;

        public int Port {
            get {
                return port;
            }
        }

        private bool closeConnectionImmediately = false;

        public bool CloseConnectionImmediatelyRequested {
            get {
                return closeConnectionImmediately;
            }
        }

        private bool closeConnectionGracefully = false;

        // TODO force stop connection even if file is not complete
        public void CloseConnectionImmediately() {
            closeConnectionImmediately = true;
        }

        // TODO if duing file transfer, wait until file is done
        public void CloseConnectionGracefully() {
            closeConnectionGracefully = true;
        }

        private bool stopServiceRequested = false;

        // use this instead of stopService
        public void stopService(bool finishTransfer) {
            if (isListening || !finishTransfer)
            {
                stopServiceRequested = true;
                serverSocket.Close();
            }
            else // quit after current connection is finished
                stopServiceRequested = true;
            
        }

		public override void OnCreate()
		{
            base.OnCreate();
		}

		public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            // reset these parameters when start service
            closeConnectionImmediately = false;
            closeConnectionGracefully = false;
            stopServiceRequested = false;

            if (!hasStarted)
            {
                hasStarted = true;

                // Start server
                serverSocket = new ServerSocket(0);
                port = serverSocket.LocalPort;

                fileDirectory = intent.GetStringExtra(EXTRA_FILE_DIR);

                new Thread(() =>
                {
                    

                    Log.Debug(id, "Server Task started");
                    Socket client = null;
                    DataInputStream inputStream = null;
                    DataOutputStream outputStream = null;

                    Intent broadcastIntent;

                    while (true) // restarting socket after each connection
                    {

                        try
                        {

                            Log.Debug(id, "Listening for connection");
                            // wait for client connection
                            isListening = true;
                            client = serverSocket.Accept();
                            //taskListener.OnConnected(true);
                            broadcastIntent = new Intent();
                            broadcastIntent.SetAction(ServerBroadcastReceiver.ACTION_CONNECTED);
                            SendBroadcast(broadcastIntent);
                            isListening = false;

                            Log.Info(id, "Received incoming connection ");
                            outputStream = new DataOutputStream(client.OutputStream);
                            inputStream = new DataInputStream(client.InputStream);

                            while (true)
                            { // receive files until got 0 (indicate end)

                                if (closeConnectionImmediately || closeConnectionGracefully)
                                {
                                    throw new Java.Lang.Exception("Interrupted");
                                }

                                // 1.1 receive file name size 
                                Log.Debug(id, "Receiving file name size from client");
                                //inputStream.Read(buf, 0, sizeof(long));
                                int read = readInputStreamWithTimeout(inputStream, buf, 0, sizeof(long), 5000);
                                if (read == -1)
                                {
                                    // read timed out
                                    throw new TimeoutException();
                                }
                                int size = (int)BitConverter.ToInt64(buf, 0);

                                if (size == 0)
                                {// done
                                    Log.Debug(id, "Got end signal from client. Ending");
                                    broadcastIntent = new Intent();
                                    broadcastIntent.SetAction(ServerBroadcastReceiver.ACTION_DISCONNECTED);
                                    SendBroadcast(broadcastIntent);
                                    break;
                                }

                                if (closeConnectionImmediately || closeConnectionGracefully)
                                {
                                    throw new Java.Lang.Exception("Interrupted");
                                }

                                // 1.2 receive file name
                                Log.Debug(id, "Receiving file name from client");
                                byte[] name = new byte[size];
                                //inputStream.Read(name, 0, size);
                                readInputStreamWithTimeout(inputStream, name, 0, size, 5000);
                                if (read == -1)
                                {
                                    // read timed out
                                    throw new TimeoutException();
                                }
                                string filename = Encoding.Default.GetString(name); // with relative path

                                // 1.3 receive file size (as long) from client
                                //inputStream.Read(buf, 0, sizeof(long));
                                readInputStreamWithTimeout(inputStream, buf, 0, sizeof(long), 5000);
                                if (read == -1)
                                {
                                    // read timed out
                                    throw new TimeoutException();
                                }
                                size = (int)BitConverter.ToInt64(buf, 0);

                                Log.Debug(id, "Receiving " + filename + ": " + size + " bytes");

                                if (closeConnectionImmediately || closeConnectionGracefully)
                                {
                                    throw new Java.Lang.Exception("Interrupted");
                                }

                                // create path directory if needed
                                FileInfo fileInfo = new FileInfo(fileDirectory + "/" + filename);
                                fileInfo.Directory.Create();

                                // 1.4 receive file from client

                                if (size > 0)
                                {
                                    Log.Info(id, "Receiving file from client");
                                    publishProgress("Receiving " + filename);
                                    outFileStream = System.IO.File.Create(fileDirectory + "/" + filename);
                                    UIUtils.CopyStream(this, client.InputStream, outFileStream, size);
                                    Log.Info(id, "Received file length: " + size);
                                    outFileStream.Close();
                                }

                                // send 0 to signal received
                                byte[] sizeData = BitConverter.GetBytes((long)0);
                                outputStream.Write(sizeData, 0, sizeof(long));

                                if (closeConnectionImmediately || closeConnectionGracefully)
                                {
                                    throw new Java.Lang.Exception("Interrupted");
                                }

                                // files received
                                publishProgress();

                                // wait for clinet response or timed out
                                Log.Info(id, "Wait for client to send next");

                            } // while more files to receive




                        }
                        catch (SocketException)
                        {
                            // Socket closed (interrupt)
                            if (client != null && !client.IsClosed)
                                client.Close();
                            //taskListener.OnDisconnected(true);
                            broadcastIntent = new Intent();
                            broadcastIntent.SetAction(ServerBroadcastReceiver.ACTION_DISCONNECTED);
                            SendBroadcast(broadcastIntent);

                        }
                        catch (TimeoutException)
                        {
                            // Util.CopyStream read timed out
                            Log.Debug(id, "Read timed out, disconnect");
                            if (client != null && !client.IsClosed)
                                client.Close();
                            //taskListener.OnDisconnected(true);
                            broadcastIntent = new Intent();
                            broadcastIntent.SetAction(ServerBroadcastReceiver.ACTION_DISCONNECTED);
                            SendBroadcast(broadcastIntent);
                        }
                        catch (Exception e)
                        {
                            // close connection requested, restart and go back to listening
                            Log.Debug(id, e.Message);
                            if (client != null && !client.IsClosed)
                                client.Close();
                            //taskListener.OnDisconnected(true);
                            broadcastIntent = new Intent();
                            broadcastIntent.SetAction(ServerBroadcastReceiver.ACTION_DISCONNECTED);
                            SendBroadcast(broadcastIntent);
                        }

                        outFileStream.Dispose();

                        // catch interruption if any or go back to listening
                        if (stopServiceRequested)
                        {
                            Log.Debug(id, "Stopping Server Service");
                            StopSelf();
                            break;
                        } else {
                            // reset
                            closeConnectionImmediately = false;
                            closeConnectionGracefully = false;
                        }

                    } // while

                }).Start();

            }

            return StartCommandResult.NotSticky; // don't recreate service
         
        }

        public override IBinder OnBind(Intent intent)
        {
            binder = new ServerServiceBinder(this);
            return binder;
        }

		public override void OnDestroy()
		{
            // TODO if listening, Close socket
            // otherwise, mark and let task finish
            base.OnDestroy();
		}

        public int readInputStreamWithTimeout(DataInputStream inputstream, byte[] buffer, int offset, int len, int timeoutMillis)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            stopwatch.Start();
            while (inputstream.Available() <= 0)
            {
                if (stopwatch.ElapsedMilliseconds >= timeoutMillis)
                {
                    // timed out
                    return -1;
                }
            }
            int read = inputstream.Read(buffer, offset, len);
            return read;
        }

        private void publishProgress(string progress = null)
        {
            if (progress != null){
                //taskListener.OnStatusUpdate(progress); // receiving file  
                Intent broadcastIntent = new Intent();
                broadcastIntent.SetAction(ServerBroadcastReceiver.ACTION_STATUS_MESSAGE);
                broadcastIntent.PutExtra(ServerBroadcastReceiver.EXTRA_MESSAGE, progress);
                SendBroadcast(broadcastIntent);
            } 
            else
            {
                //taskListener.OnFilesReceived(); // received
                Intent broadcastIntent = new Intent();
                broadcastIntent.SetAction(ServerBroadcastReceiver.ACTION_FILE_RECEIVED);
                SendBroadcast(broadcastIntent);
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
