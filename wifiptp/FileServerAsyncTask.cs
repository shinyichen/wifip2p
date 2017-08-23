using System.IO;
using System.Net;
using Android.Content;
using Android.Net;
using Android.Net.Wifi.P2p;
using Android.OS;
using Android.Util;
using Java.IO;
using Java.Lang;
using Java.Net;
using static Android.Net.Wifi.P2p.WifiP2pManager;

namespace wifiptp
{
    public class FileServerAsyncTask : AsyncTask
    {
        private Context context;
        WifiP2pManager manager;
        Channel channel;
        byte[] buf = new byte[1024];

        public FileServerAsyncTask(Context context, WifiP2pManager manager, Channel channel)
        {
            this.context = context;
            this.channel = channel;
            this.manager = manager;
        }

        protected override Object DoInBackground(params Object[] @params)
        {

            Log.Info("Server", "Starting server service");

            // wait for client connection
            ServerSocket serverSocket = new ServerSocket(8888);
            Socket client = serverSocket.Accept();

            Log.Info("Server", "Received incoming connection on port 8888");
            Stream inputStream = client.InputStream;

            // receive image from client
            Log.Info("Server", "Receiving file from client");
            Java.IO.File imageDir = Environment.GetExternalStoragePublicDirectory(Environment.DirectoryPictures);
            string fileName = "wifip2p - " + JavaSystem.CurrentTimeMillis() + ".jpg";
            Java.IO.File imageFile = new Java.IO.File(imageDir, fileName);
            imageFile.CreateNewFile();
            FileStream imageFileStream = new FileStream(imageDir + "/" + fileName, FileMode.Create, FileAccess.Write, FileShare.None);
            inputStream.CopyTo(imageFileStream);


            Log.Info("Server", "Server finished. Removing group");
            manager.RemoveGroup(channel, new MainActivity.GroupRemovedListener());

			return imageDir + "/received.jpg";
		}
    }
}
