using System;
using System.Text;
using Android.OS;
using Android.Util;
using System.Threading;
using Java.Net;
using Java.IO;
using System.IO;

namespace wifip2pApi.Android
{
    public class ServerAsyncTask : AsyncTask
    {
        
        private const string id = "ServerService";
        
        private ServerSocket serverSocket;

        private Java.IO.File fileDirectory;

        private int port;

        private FileStream outFileStream = null;

        private byte[] buf = new byte[65936];

        private bool isListening = false;

        private ITaskProgress taskListener;

        public ServerAsyncTask(ServerSocket serverSocket, Java.IO.File directory, ITaskProgress taskListener)
        {
            this.serverSocket = serverSocket;
            this.port = serverSocket.LocalPort;
            this.fileDirectory = directory;
            this.taskListener = taskListener;
        }

        public bool IsListening {
            get {
                return isListening;
            }
        }

        protected override Java.Lang.Object DoInBackground(params Java.Lang.Object[] @params)
        {

            Log.Debug(id, "Server Task started");
            Socket client = null;
            DataInputStream inputStream = null;
            DataOutputStream outputStream = null;

            while (true) // restarting socket after each connection
            {

                try
                {

                    Log.Debug(id, "Listening for connection");
                    // wait for client connection
                    isListening = true;
                    client = serverSocket.Accept();
                    taskListener.OnConnected(true);
                    isListening = false;

                    Log.Info(id, "Received incoming connection ");
                    outputStream = new DataOutputStream(client.OutputStream);
                    inputStream = new DataInputStream(client.InputStream);

                    while (true) { // receive files until got 0 (indicate end)

                        // 1.1 receive file name size 
                        Log.Debug(id, "Receiving file name size from client");
                        inputStream.Read(buf, 0, sizeof(long));
                        int size = (int)BitConverter.ToInt64(buf, 0);

                        if (size == 0)
                        {// done
                            Log.Debug(id, "Got end signal from client. Ending");
                            taskListener.OnDisconnected(true);
                            break;
                        }

                        // 1.2 receive file name
                        Log.Debug(id, "Receiving file name from client");
                        byte[] name = new byte[size];
                        inputStream.Read(name, 0, size);
                        string filename = Encoding.Default.GetString(name); // with relative path

                        // 1.3 receive file size (as long) from client
                        inputStream.Read(buf, 0, sizeof(long));
                        size = (int)BitConverter.ToInt64(buf, 0);

                        Log.Debug(id, "Receiving " + filename + ": " + size + " bytes");

                        // create path directory if needed
                        FileInfo fileInfo = new FileInfo(fileDirectory + "/" + filename);
                        fileInfo.Directory.Create();

                        // 1.4 receive image from client
                        if (size > 0)
                        {
                            Log.Info(id, "Receiving file from client");
                            outFileStream = System.IO.File.Create(fileDirectory + "/" + filename);
                            UIUtils.CopyStream(client.InputStream, outFileStream, size);
                            Log.Info(id, "Received file length: " + size);
                            outFileStream.Close();
                        }

                        // send 0 to signal received
                        byte[] sizeData = BitConverter.GetBytes((long)0);
                        outputStream.Write(sizeData, 0, sizeof(long));

                        // files received
                        PublishProgress();

                        // wait for clinet response or timed out
                        Log.Info(id, "Wait for client to send next");
                        int elapsed = 0;
                        while(inputStream.Available() == 0 && elapsed < 7000) {
                            Thread.Sleep(1000);
                            elapsed += 1000;
                        }

                        // if timed out
                        if (elapsed >= 7000) {
                            Log.Debug(id, "Wait timed out, give up.");
                            break;
                        }
                    } // while more files to receive

                 


                } catch (SocketException) {
                    // Socket closed (interrupt)
                    if (IsCancelled)
                        break;
                } catch (TimeoutException) {
                    // Util.CopyStream read timed out
                    Log.Debug(id, "Read timed out, exit server task");
                }

                // catch interruption if any or go back to listening
                if (IsCancelled)
                {
                    break;
                }

            } // while

            return "Complete";

        }

        protected override void OnProgressUpdate(params Java.Lang.Object[] values)
        {
            taskListener.OnFilesReceived();
            base.OnProgressUpdate(values);
        }

        // This is called to end the server task
        // Otherwise this task will loop and listen forever
        protected override void OnCancelled()
        {
            Log.Debug(id, "Task Canceled");
            try {
                serverSocket.Close();
            } catch (ObjectDisposedException) {
                // already disposed
            }

            base.OnCancelled();
        }
    }
}
