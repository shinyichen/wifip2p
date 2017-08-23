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
    public class ClientAsyncTask : AsyncTask
    {
        private Context context;

        private InetAddress ip;

        private WifiP2pManager manager;

        private Channel channel;

		private byte[] buf = new byte[1024];

        public ClientAsyncTask(Context context, InetAddress ip, WifiP2pManager manager, Channel channel)
        {
            this.manager = manager;
            this.channel = channel;
            this.context = context;
            this.ip = ip;
        }

        protected override Java.Lang.Object DoInBackground(params Java.Lang.Object[] @params)
        {

            Log.Info("Client", "Starting client service");

            // connect to server
            Log.Info("Client", "Connecting to server port 8888");
            Socket socket = new Socket();
            socket.Bind(null);
            InetSocketAddress sa = new InetSocketAddress(ip, 8888);
            socket.Connect(sa, 500);
            Stream outputStream = socket.OutputStream;

            Java.IO.File imageDir = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryPictures);

            // send image to server
            Log.Info("Client", "Sending file to server");
            ContentResolver cr = context.ContentResolver;
            Stream inputStream = cr.OpenInputStream(Android.Net.Uri.Parse(imageDir.AbsolutePath + "/image.jpg"));
            inputStream.Read(buf, 0, 1024);
            int len;
            while ((len = inputStream.Read(buf, 0, 1024)) != -1)
            {
            	outputStream.Write(buf, 0, len);
            }
            outputStream.Close();
            inputStream.Close();

			Log.Info("Client", "Client finished, removing from group");
			manager.RemoveGroup(channel, new MainActivity.GroupRemovedListener());

            return imageDir + "/image.jpg";
        }
    }
}
