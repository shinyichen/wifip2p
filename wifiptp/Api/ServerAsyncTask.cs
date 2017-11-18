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

        private int port;

        private Stream inputStream = null, outputStream = null;

        private FileStream fileStream = null;

        private FileStream imageFileStream = null;

        private Java.IO.File imageDir = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryPictures);

        private byte[] buf = new byte[1024];

        private bool isListening = false;


        public ServerAsyncTask(ServerSocket serverSocket)
        {
            this.serverSocket = serverSocket;
            this.port = serverSocket.LocalPort;
        }

        public bool IsListening {
            get {
                return isListening;
            }
        }

        protected override Java.Lang.Object DoInBackground(params Java.Lang.Object[] @params)
        {

            Log.Debug(ID, "Server Task started");

            while (true)
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
                        Log.Info(ID, "Sending file size to client");
                        fileStream = new FileStream(imageDir.AbsolutePath + "/image.jpg", FileMode.Open, FileAccess.Read);
                        byte[] sizeData = BitConverter.GetBytes(fileStream.Length);
                        outputStream.Write(sizeData, 0, sizeof(long));

                        // 2.2 send file
                        Log.Info(ID, "Sending file to client");
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

                        Log.Info(ID, bytesRead + " bytes sent");
                    }
                    else
                    { // no file to send

                        // 2.1 send file size 0
                        Log.Info(ID, "No image file. Send size 0 to server");
                        byte[] sizeData = BitConverter.GetBytes((long)0);
                        outputStream.Write(sizeData, 0, sizeof(long));
                    }

                    // wait until clinet response 
                    Log.Info(ID, "Wait for client to finish");
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
