using System;
using System.IO;
using Android.Content;
using Android.Net.Wifi.P2p;
using Android.OS;
using Android.Util;
using Java.Lang;
using Java.Net;
using static Android.Net.Wifi.P2p.WifiP2pManager;

namespace wifiptp
{
    public class FileServerAsyncTask : AsyncTask
    {

        private const string id = "Server";
        private Context context;
        WifiP2pManager manager;
        Channel channel;
        int port;

        private ITaskCompleted taskCompletedListener;

        byte[] buf = new byte[1024];

        public FileServerAsyncTask(Context context, WifiP2pManager manager, Channel channel, int port, ITaskCompleted taskCompletedListener)
        {
            this.context = context;
            this.channel = channel;
            this.manager = manager;
            this.port = port;
            this.taskCompletedListener = taskCompletedListener;
        }

        protected override Java.Lang.Object DoInBackground(params Java.Lang.Object[] @params)
        {

            Log.Info("Server", "Starting server service");
			Stream inputStream = null, outputStream = null;
			FileStream fileStream = null;
			FileStream imageFileStream = null;
            Java.IO.File imageDir = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryPictures);

            // wait for client connection
            ServerSocket serverSocket = new ServerSocket(port);
            Socket client = serverSocket.Accept();

            Log.Info("Server", "Received incoming connection on port 8888");
            inputStream = client.InputStream;
            outputStream = client.OutputStream;

			// 1. server receives file first
			
            // 1.1 receive file size (as long) from client
			Log.Info("Server", "Receiving file size from client");
            inputStream.Read(buf, 0, sizeof(long));
            long size = System.BitConverter.ToInt64(buf, 0);
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
            else { // no file to send

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


            return imageDir + "/received.jpg";
        }

        protected override void OnPostExecute(Java.Lang.Object result)
        {
            taskCompletedListener.OnTaskCompleted();
        }
    }
}
