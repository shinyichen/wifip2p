using System;
using System.Text;
using Android.OS;
using Android.Util;
using System.Threading;
using Java.Net;
using Java.IO;
using System.IO;
using System.Diagnostics;

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

        public bool closeConnectionRequested = false;

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

                        if (closeConnectionRequested)
                        {
                            throw new Java.Lang.Exception("Interrupted");
                        }

                        // 1.1 receive file name size 
                        Log.Debug(id, "Receiving file name size from client");
                        //inputStream.Read(buf, 0, sizeof(long));
                        int read = readInputStreamWithTimeout(inputStream, buf, 0, sizeof(long), 2000);
                        if (read == -1) {
                            // read timed out
                            throw new TimeoutException();
                        }
                        int size = (int)BitConverter.ToInt64(buf, 0);

                        if (size == 0)
                        {// done
                            Log.Debug(id, "Got end signal from client. Ending");
                            taskListener.OnDisconnected(true);
                            break;
                        }

                        if (closeConnectionRequested)
                        {
                            throw new Java.Lang.Exception("Interrupted");
                        }

                        // 1.2 receive file name
                        Log.Debug(id, "Receiving file name from client");
                        byte[] name = new byte[size];
                        //inputStream.Read(name, 0, size);
                        readInputStreamWithTimeout(inputStream, name, 0, size, 2000);
                        if (read == -1)
                        {
                            // read timed out
                            throw new TimeoutException();
                        }
                        string filename = Encoding.Default.GetString(name); // with relative path

                        // 1.3 receive file size (as long) from client
                        //inputStream.Read(buf, 0, sizeof(long));
                        readInputStreamWithTimeout(inputStream, buf, 0, sizeof(long), 2000);
                        if (read == -1)
                        {
                            // read timed out
                            throw new TimeoutException();
                        }
                        size = (int)BitConverter.ToInt64(buf, 0);

                        Log.Debug(id, "Receiving " + filename + ": " + size + " bytes");

                        if (closeConnectionRequested)
                        {
                            throw new Java.Lang.Exception("Interrupted");
                        }

                        // create path directory if needed
                        FileInfo fileInfo = new FileInfo(fileDirectory + "/" + filename);
                        fileInfo.Directory.Create();

                        // 1.4 receive image from client

                        if (size > 0)
                        {
                            Log.Info(id, "Receiving file from client");
                            PublishProgress("Receiving " + filename);
                            outFileStream = System.IO.File.Create(fileDirectory + "/" + filename);
                            UIUtils.CopyStream(this, client.InputStream, outFileStream, size);
                            Log.Info(id, "Received file length: " + size);
                            outFileStream.Close();
                        }

                        // send 0 to signal received
                        byte[] sizeData = BitConverter.GetBytes((long)0);
                        outputStream.Write(sizeData, 0, sizeof(long));

                        if (closeConnectionRequested)
                        {
                            throw new Java.Lang.Exception("Interrupted");
                        }

                        // files received
                        PublishProgress();

                        // wait for clinet response or timed out
                        Log.Info(id, "Wait for client to send next");

                    } // while more files to receive

                 


                } catch (SocketException) {
                    // Socket closed (interrupt)
                    if (client != null && !client.IsClosed)
                        client.Close();
                    taskListener.OnDisconnected(true);
                    if (IsCancelled) // if task is cancelled, let it stop
                        break;
                } catch (TimeoutException) {
                    // Util.CopyStream read timed out
                    Log.Debug(id, "Read timed out, disconnect");
                    if (client != null && !client.IsClosed)
                        client.Close();
                    taskListener.OnDisconnected(true);
                } catch (Exception e) {
                    // close connection requested, restart and go back to listening
                    Log.Debug(id, e.Message);
                    if (!client.IsClosed)
                        client.Close();
                    taskListener.OnDisconnected(true);
                    if (closeConnectionRequested)
                    {
                        closeConnectionRequested = false; // go back to listening
                    }
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
            if (values != null && values.Length > 0) {
                taskListener.OnStatusUpdate((string)values[0]); // receiving file
            } else {
                taskListener.OnFilesReceived(); // received
            }

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

        public void closeConnection() {
            closeConnectionRequested = true;
        }

        private int readInputStreamWithTimeout(DataInputStream inputstream, byte[] buffer, int offset, int len, int timeoutMillis) {
            Stopwatch stopwatch = Stopwatch.StartNew();
            stopwatch.Start();
            while (inputstream.Available() <= 0) {
                if (stopwatch.ElapsedMilliseconds >= timeoutMillis) {
                    // timed out
                    return -1;
                }
            }
            int read = inputstream.Read(buffer, offset, len);
            return read;
        }
    }
}
