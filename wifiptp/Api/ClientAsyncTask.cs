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

        private List<Java.IO.File> files;

        private int port;

        private ITaskCompleted taskCompletedListener;

        private byte[] buf = new byte[1024];

        public ClientAsyncTask(InetAddress ip, int port, List<Java.IO.File> files, ITaskCompleted taskCompletedListener)
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
                int count = 0;
                byte[] sizeData;
                foreach (Java.IO.File file in files) {
                    if (file.Exists()) {

                        Log.Info(id, "Sending file " + count + 1);

                        // 1.1 send file name size as long integer
                        Log.Debug(id, "Sending file name size to server");
                        byte[] name = Encoding.ASCII.GetBytes(file.Name);
                        sizeData = BitConverter.GetBytes(name.LongLength);
                        outputStream.Write(sizeData, 0, sizeof(long));

                        // 1.2 send file name
                        Log.Debug(id, "Sending file name to server");
                        outputStream.Write(name, 0, name.Length);

                        // 1.3 send size of file as a 64-bit (8 bytes) long integer
                        Log.Info(id, "Sending file size to server");
                        fileStream = new FileStream(file.AbsolutePath, FileMode.Open, FileAccess.Read);
                        sizeData = BitConverter.GetBytes(fileStream.Length);
                        outputStream.Write(sizeData, 0, sizeof(long));

                        // 1.4 send file
                        Log.Info(id, "Sending file to server");
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

                        count++;

                        // wait for client's response
                        Log.Info(id, "Waiting to hear from server");
                        while (!inputStream.IsDataAvailable()) { }
                        inputStream.Read(buf, 0, sizeof(long));
                    }
                }

                // send 0 as a 64-bit long integer to indicate end
                Log.Info(id, "Finished. Send end signal");
                sizeData = BitConverter.GetBytes((long)0);
                outputStream.Write(sizeData, 0, sizeof(long));

                return "Success";


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
