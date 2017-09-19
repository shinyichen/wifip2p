using System;
using System.IO;
using Android.Content;
using Android.Net.Wifi.P2p;
using Android.OS;
using Android.Util;
using Java.Net;
using static Android.Net.Wifi.P2p.WifiP2pManager;

namespace wifiptp
{
    public class ClientAsyncTask : AsyncTask
    {
        private Context context;

        private InetAddress ip;

        private WifiP2pManager manager;

        private Channel channel;

        private ITaskCompleted taskCompletedListener;

        private byte[] buf = new byte[1024];

        public ClientAsyncTask(Context context, InetAddress ip, WifiP2pManager manager, Channel channel, ITaskCompleted taskCompletedListener)
        {
            this.manager = manager;
            this.channel = channel;
            this.context = context;
            this.ip = ip;
            this.taskCompletedListener = taskCompletedListener;
        }

        protected override Java.Lang.Object DoInBackground(params Java.Lang.Object[] @params)
        {

            Log.Info("Client", "Starting client service");

            // connect to server
            Log.Info("Client", "Connecting to server");
            Socket socket = new Socket();
            socket.Bind(null);
            InetSocketAddress sa = new InetSocketAddress(ip, MainActivity.port);
            try
            {
                socket.Connect(sa, 2000);

                Stream inputStream = socket.InputStream;
				Stream outputStream = socket.OutputStream;

				// prepare image to send
                Java.IO.File imageDir = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryPictures);
				FileStream fileStream = new FileStream(imageDir.AbsolutePath + "/image.jpg", FileMode.Open, FileAccess.Read);

                // 1. send size of file as a 64-bit (8 bytes) long integer
                Log.Info("Client", "Sending file size to server");
                byte[] sizeData = BitConverter.GetBytes(fileStream.Length);
                outputStream.Write(sizeData, 0, sizeof(long));

                // 2. send file
                Log.Info("Client", "Sending file to server");
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

                Log.Info("Client", bytesRead + " bytes sent");


				// wait for server to finish receiving
				Log.Info("Client", "Waiting for server to finish");
                while (!inputStream.IsDataAvailable())
				{

				}

				Log.Info("Client", "Finished, closing");
				outputStream.Close();
                inputStream.Close();
				fileStream.Close();

				if (socket != null)
				{
					if (socket.IsConnected)
					{
						socket.Close();
					}
				}

				Log.Info("Client", "Removing from group");
				manager.RemoveGroup(channel, new MainActivity.GroupRemovedListener());

				return imageDir + "/image.jpg";


            } catch (Java.Lang.Exception e) 
            {
                Log.Info("Client", "Client error: " + e.Message);
                manager.RemoveGroup(channel, new MainActivity.GroupRemovedListener());
                return "failed";
            }

        }

        protected override void OnPostExecute(Java.Lang.Object result)
        {
            taskCompletedListener.OnTaskCompleted();
        }
    }
}
