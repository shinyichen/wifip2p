using System;
using System.IO;
using Android.OS;
using Android.Util;
using Java.Lang;
using Java.Net;

namespace wifiptp.Api
{
    public class ServerAsyncTask : AsyncTask
    {
        
        private const string ID = "ServerService";
        
        private ServerSocket serverSocket;

        private Java.IO.File fileDirectory;

        private int port;

        private Stream inputStream = null, outputStream = null;

        private FileStream fileStream = null;

        private FileStream imageFileStream = null;

        private byte[] buf = new byte[1024];

        private bool isListening = false;


        public ServerAsyncTask(ServerSocket serverSocket, Java.IO.File directory)
        {
            this.serverSocket = serverSocket;
            this.port = serverSocket.LocalPort;
            this.fileDirectory = directory;
        }

        public bool IsListening {
            get {
                return isListening;
            }
        }

        protected override Java.Lang.Object DoInBackground(params Java.Lang.Object[] @params)
        {

            Log.Debug(ID, "Server Task started");

            while (true) // restarting socket after each connection
            {

                try
                {
                    // wait for client connection
                    if (serverSocket.IsClosed)
                    {
                        serverSocket = new ServerSocket(port);
                    }

                    isListening = true;
                    Socket client = serverSocket.Accept();
                    isListening = false;

                    Log.Info("Server", "Received incoming connection ");
                    inputStream = client.InputStream;
                    outputStream = client.OutputStream;

                    int count = 0;
                    while (true) { // receive files until got file size 0
                        
                        // 1.1 receive file size (as long) from client
                        inputStream.Read(buf, 0, sizeof(long));
                        long size = BitConverter.ToInt64(buf, 0);
                        if (size == 0) // done
                            break; 

                        Log.Debug("Server", "Receiving file " + count + 1);
                        Log.Info("Server", "Expecting file size: " + size + " bytes");

                        // 1.2 receive image from client
                        if (size > 0)
                        {
                            Log.Info("Server", "Receiving file from client");
                            string fileName = "wifip2p - " + JavaSystem.CurrentTimeMillis() + ".jpg"; // TODO use original name
                            Java.IO.File imageFile = new Java.IO.File(fileDirectory, fileName);
                            imageFile.CreateNewFile();
                            imageFileStream = new FileStream(imageFile.AbsolutePath, FileMode.Create, FileAccess.Write, FileShare.None);
                            Utils.CopyStream(inputStream, imageFileStream, size);
                            imageFileStream.Flush();

                            Log.Info("Server", "Received file length: " + imageFileStream.Length);
                            Log.Info("Server", "Write to file length: " + imageFile.Length());
                        }

                        // send 0 to signal received
                        byte[] sizeData = BitConverter.GetBytes((long)0);
                        outputStream.Write(sizeData, 0, sizeof(long));

                        // wait for clinet response 
                        Log.Info(ID, "Wait for client to send next");
                        while (!inputStream.IsDataAvailable()) { }


                    } // while more files to receive

                    if (outputStream != null)
                        outputStream.Close();
                    if (inputStream != null)
                        inputStream.Close();
                    if (fileStream != null)
                        fileStream.Close();
                    if (imageFileStream != null)
                        imageFileStream.Close();

                    serverSocket.Close();

                    // catch interruption if any or go back to listening
                    if (IsCancelled)
                    {
                        return "interrupted";
                    }


                } catch (SocketException) {
                    // Socket closed (interrupt)
                    return "interrupted";
                }
            } // while

        }

        // This is called to end the server task
        // Otherwise this task will loop and listen forever
        protected override void OnCancelled()
        {
            Log.Debug(ID, "Task Canceled");
            if (!serverSocket.IsClosed)
                serverSocket.Close();
            base.OnCancelled();
        }
    }
}
