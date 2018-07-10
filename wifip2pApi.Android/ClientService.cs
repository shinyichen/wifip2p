
using System;
using System.Collections.Generic;
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
    [Service(Label = "ClientService")]
    [IntentFilter(new String[] { "wifip2pApi.Android.ClientService" })]
    public class ClientService : Service
    {
        IBinder binder;

        public static string EXTRA_ADDRESS = "address";

        public static string EXTRA_PORT = "port";

        public static string EXTRA_FILES = "files";

        private const string id = "Client";

        private Socket clientSocket;

        private Intent broadcastIntent;

        private byte[] buf = new byte[65936];

        private bool closeConnectionImmediately = false;

        private bool closeConnectionGracefully = false;

        // TODO close connection even if file is not complete
        public void CloseConnectionImmediately()
        {
            closeConnectionImmediately = true;
        }


        // TODO close connection immediately after finishing the current file
        public void CloseConnectionGracefully()
        {
            closeConnectionGracefully = true;
        }

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            
            // get input params
            InetAddress address = (InetAddress)intent.GetSerializableExtra(EXTRA_ADDRESS);
            int port = intent.GetIntExtra(EXTRA_PORT, 0);
            IList<string> files = intent.GetStringArrayListExtra(EXTRA_FILES);

            Log.Info("Client", "Starting client service");


            new Thread(() =>
            {
                // connect to server
                Log.Info("Client", "Connecting to server");
                try
                {
                    clientSocket = new Socket(address, port);
                    broadcastIntent = new Intent();
                    broadcastIntent.SetAction(ClientBroadcastReceiver.ACTION_CONNECTED);
                    SendBroadcast(broadcastIntent);
                    DataInputStream inputStream = new DataInputStream(clientSocket.InputStream);
                    DataOutputStream outputStream = new DataOutputStream(clientSocket.OutputStream);

                    // 1. clients sends file first

                    // prepare file to send
                    foreach (string file in files)
                    {
                        if (closeConnectionImmediately || closeConnectionGracefully)
                        {
                            throw new Java.Lang.Exception("Interrupted");
                        }

                        if (System.IO.File.Exists(file) || System.IO.Directory.Exists(file))
                        {
                            // if it is a file or directory
                            sendOneFile(file, inputStream, outputStream, "");
                        }
                    }

                    // send 0 as a 64-bit long integer to indicate end
                    Log.Info(id, "Finished. Send end signal");
                    byte[] sizeData = BitConverter.GetBytes((long)0);
                    outputStream.Write(sizeData, 0, sizeof(long));

                }
                catch (TimeoutException e)
                {
                    // sendOneFile read timeout
                    Log.Info(id, "Exception caught: " + e.Message);

                }
                catch (Java.Lang.Exception e)
                {
                    Log.Info(id, "Exception caught: " + e.Message);

                }
                finally
                {
                    Log.Info(id, "Finished, closing");


                    if (clientSocket != null)
                    {
                        if (clientSocket.IsConnected)
                        {
                            clientSocket.Close();
                        }
                    }

                    broadcastIntent = new Intent();
                    broadcastIntent.SetAction(ClientBroadcastReceiver.ACTION_DISCONNECTED);
                    SendBroadcast(broadcastIntent);
                }

                StopSelf();
                
            }).Start();


            return StartCommandResult.NotSticky;
        }

        public override IBinder OnBind(Intent intent)
        {
            binder = new ClientServiceBinder(this);
            return binder;
        }

        private void sendOneFile(string file, DataInputStream inputStream, DataOutputStream outputStream, string relativePath)
        {

            // if path is a file
            if (System.IO.File.Exists(file))
            {
                FileStream filestream = System.IO.File.OpenRead(file);
                Log.Info(id, "Prepare to send file");

                // 1.1 send file name size as long integer
                Log.Debug(id, "Sending file name size to server");
                string fileName = Path.GetFileName(filestream.Name);
                broadcastIntent = new Intent();
                broadcastIntent.SetAction(ClientBroadcastReceiver.ACTION_STATUS_MESSAGE);
                broadcastIntent.PutExtra(ClientBroadcastReceiver.EXTRA_MESSAGE, "Sending " + fileName);
                SendBroadcast(broadcastIntent);
                fileName = relativePath + "/" + fileName;
                byte[] name = Encoding.Default.GetBytes(fileName);
                byte[] sizeData = BitConverter.GetBytes(name.LongLength);
                outputStream.Write(sizeData, 0, sizeof(long));

                if (closeConnectionImmediately || closeConnectionGracefully)
                {
                    throw new Java.Lang.Exception("Interrupted");
                }

                // 1.2 send file name
                Log.Debug(id, "Sending file name to server");
                outputStream.Write(name, 0, name.Length);

                if (closeConnectionImmediately || closeConnectionGracefully)
                {
                    throw new Java.Lang.Exception("Interrupted");
                }

                // 1.3 send size of file as a 64-bit (8 bytes) long integer
                Log.Info(id, "Sending file size to server");
                sizeData = BitConverter.GetBytes(filestream.Length);
                outputStream.Write(sizeData, 0, sizeof(long));

                if (closeConnectionImmediately || closeConnectionGracefully)
                {
                    throw new Java.Lang.Exception("Interrupted");
                }

                // 1.4 send file
                Log.Info(id, "Sending file to server");
                //buf = new byte[filestream.Length];
                int bytesToRead = (int)filestream.Length;
                int bytesRead = 0;

                do
                {
                    if (closeConnectionImmediately)
                    {
                        filestream.Close();
                        throw new Java.Lang.Exception("Interrupted");
                    }

                    int r = 65936;
                    if (bytesToRead < 65936)
                        r = bytesToRead;
                    int len = filestream.Read(buf, 0, r);

                    // send
                    outputStream.Write(buf, 0, len);

                    bytesRead += len;
                    bytesToRead -= len;
                } while (bytesToRead > 0);

                Log.Info(id, bytesRead + " bytes sent");

                filestream.Close();

                // wait for client's response
                Log.Info(id, "Waiting to hear from server");

                if (closeConnectionImmediately || closeConnectionGracefully)
                {
                    throw new Java.Lang.Exception("Interrupted");
                }

                // wait till data available or timed out
                int read = readInputStreamWithTimeout(inputStream, buf, 0, sizeof(long), 60000);
                if (read == -1)
                {
                    // timed out
                    Log.Info(id, "Wait timed out. Give up.");
                    throw new TimeoutException("read timeout");
                }

            }
            else
            {
                // path is a directory
                string[] children = Directory.GetFileSystemEntries(file);
                string dirName = Path.GetFileName(file);
                foreach (string child in children)
                {
                    sendOneFile(child, inputStream, outputStream, relativePath + "/" + dirName);
                }
            }
        }

        private int readInputStreamWithTimeout(DataInputStream inputstream, byte[] buffer, int offset, int len, int timeoutMillis)
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
    }

    public class ClientServiceBinder : Binder
    {
        readonly ClientService service;

        public ClientServiceBinder(ClientService service)
        {
            this.service = service;
        }

        public ClientService GetClientService()
        {
            return service;
        }
    }
}
