using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Android.Content;
using Android.Net.Wifi.P2p;
using Android.OS;
using Android.Util;
using Java.Lang;
using Java.Net;

namespace wifiptp
{
    public class ClientAsyncTask : AsyncTask
    {

        private const string id = "Client";

        private InetAddress ip;

        private List<string> files;

        private int port;

        private ITaskCompleted taskCompletedListener;

        private byte[] buf = new byte[1024];

        public ClientAsyncTask(InetAddress ip, int port, List<string> files, ITaskCompleted taskCompletedListener)
        {
            this.ip = ip;
            this.port = port;
            this.files = files;
            this.taskCompletedListener = taskCompletedListener;
        }

        protected override Java.Lang.Object DoInBackground(params Java.Lang.Object[] @params)
        {

            Log.Info("Client", "Starting client service");
            Stream inputStream = null, outputStream = null;
            FileStream fileStream = null;
            FileStream imageFileStream = null;

            // connect to server
            Log.Info("Client", "Connecting to server");
            Socket socket = new Socket();
            socket.Bind(null);
            InetSocketAddress sa = new InetSocketAddress(ip, port);
            try
            {
                socket.Connect(sa, 2000);

                inputStream = socket.InputStream;
				outputStream = socket.OutputStream;

                // 1. clients sends file first

				// prepare image to send
                Java.IO.File imageDir = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryPictures);
                if (File.Exists(imageDir.AbsolutePath + "/image.jpg"))
                {

                    // 1.1 send size of file as a 64-bit (8 bytes) long integer
                    Log.Info("Client", "Sending file size to server");
                    fileStream = new FileStream(imageDir.AbsolutePath + "/image.jpg", FileMode.Open, FileAccess.Read);
                    byte[] sizeData = BitConverter.GetBytes(fileStream.Length);
                    outputStream.Write(sizeData, 0, sizeof(long));

                    // 1.2 send file
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

                } else { // no files to send

                    // 1.1 send size of file 0 as a 64-bit long integer
                    Log.Info(id, "No image file. Send size 0 to server");
                    byte[] sizeData = BitConverter.GetBytes((long)0);
					outputStream.Write(sizeData, 0, sizeof(long));
                }

                // 2. Client receives from server

				// wait for server to send size data
				Log.Info("Client", "Finished sending. Waiting to receive from server.");
                while (!inputStream.IsDataAvailable()) {}

                // 2.1 receive file size (as long) from server
                Log.Info(id, "Receiving file size from server");
                inputStream.Read(buf, 0, sizeof(long));
                long size = BitConverter.ToInt64(buf, 0);
                Log.Info(id, "Expecting file size: " + size + " bytes");

                // 2.2 receive image from server if server has files to send
                if (size > 0)
                {
                    Log.Info(id, "Receiving file from server");
                    string fileName = "wifip2p - " + JavaSystem.CurrentTimeMillis() + ".jpg";
                    Java.IO.File imageFile = new Java.IO.File(imageDir, fileName);
                    imageFile.CreateNewFile();
                    imageFileStream = new FileStream(imageDir + "/" + fileName, FileMode.Create, FileAccess.Write, FileShare.None);
                    Utils.CopyStream(inputStream, imageFileStream, size);
                    imageFileStream.Flush();

                    Log.Info(id, "Received file length: " + imageFileStream.Length);
                    Log.Info(id, "Write to file length: " + imageFile.Length());
                } else {
                    Log.Info(id, "Server has no files to send.");
                }

				// indicate done
				Log.Info(id, "Send Done");
				byte[] done = Encoding.ASCII.GetBytes("done");
				outputStream.Write(done, 0, done.Length);

				return imageDir + "/image.jpg";


            } catch (Java.Lang.Exception e) {
                Log.Info(id, "Exception caught: " + e.Message);
                return "failed";

            } finally {
				Log.Info(id, "Finished, closing");
                if (outputStream != null) 
                    outputStream.Close();
                if (inputStream != null)
                    inputStream.Close();
                if (fileStream != null)
                    fileStream.Close();
                if (imageFileStream != null)
                    imageFileStream.Close();

				if (socket != null)
				{
					if (socket.IsConnected)
					{
						socket.Close();
					}
				}
            }

        }

        protected override void OnPostExecute(Java.Lang.Object result)
        {
            taskCompletedListener.OnTaskCompleted();
        }
    }
}
