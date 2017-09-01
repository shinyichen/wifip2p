
using System.IO;
using System.Net;
using System.Text;
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

            // TODO move this to service, if no connection not made in certain time, kill the service

            Log.Info("Server", "Received incoming connection on port 8888");
            Stream inputStream = client.InputStream;
            Stream outputStream = client.OutputStream;

            // 1. receive file size (as long) from client
            Log.Info("Server", "Receiving file size from client");
            inputStream.Read(buf, 0, sizeof(long));
            long size = System.BitConverter.ToInt64(buf, 0);
            Log.Info("Server", "Expecting file size: " + size + " bytes");

            // 2. receive image from client
            Log.Info("Server", "Receiving file from client");
            Java.IO.File imageDir = Environment.GetExternalStoragePublicDirectory(Environment.DirectoryPictures);
            string fileName = "wifip2p - " + JavaSystem.CurrentTimeMillis() + ".jpg";
            Java.IO.File imageFile = new Java.IO.File(imageDir, fileName);
            imageFile.CreateNewFile();
            FileStream imageFileStream = new FileStream(imageDir + "/" + fileName, FileMode.Create, FileAccess.Write, FileShare.None);
            CopyStream(inputStream, imageFileStream, size); 
            imageFileStream.Flush();

            Log.Info("Server", "Received file length: " + imageFileStream.Length);
            Log.Info("Server", "Write to file length: " + imageFile.Length());

            // signal client done
            Log.Info("Server", "Done. Signal to client");
            byte[] done = Encoding.ASCII.GetBytes("done");
            outputStream.Write(done, 0, done.Length);

            inputStream.Close();
            imageFileStream.Close();
            outputStream.Close();

            serverSocket.Close();
            Log.Info("Server", "Removing group");
            manager.RemoveGroup(channel, new MainActivity.GroupRemovedListener());


			return imageDir + "/received.jpg";
		}

		public long CopyStream(Stream source, Stream target, long size)
		{
			int bufSize = 1024;
			byte[] buf = new byte[bufSize];

			int totalBytes = 0;
			int bytesRead = 0;

            // TODO need to wait till client finish sending
            // TODO could be in infinite loop, use time out
            while (size > 0)
			{
                if (source.IsDataAvailable() && ((bytesRead = source.Read(buf, 0, bufSize)) > 0))
                {
					target.Write(buf, 0, bytesRead);
					totalBytes += bytesRead;
					size -= bytesRead;
                    if (size < bufSize)
                        bufSize = (int)size;
                    Log.Info("Server", "loop: " + totalBytes);
                }

			}
			return totalBytes;
		}
    }
}
