using System;
using System.Collections.Generic;
using System.IO;

using System.Text;
using System.Threading;
using Android.OS;
using Android.Util;
using Java.IO;
using Java.Net;

namespace wifip2pApi.Android
{
    public class ClientAsyncTask : AsyncTask
    {

        private const string id = "Client";

        private Socket clientSocket;

        private InetAddress address;

        private int port;

        private List<string> files;

        private ITaskProgress taskListener;

        private byte[] buf = new byte[65936];

        public ClientAsyncTask(InetAddress address, int port, List<string> files, ITaskProgress taskListener)
        {
            this.address = address;
            this.port = port;
            this.files = files;
            this.taskListener = taskListener;
        }

        protected override Java.Lang.Object DoInBackground(params Java.Lang.Object[] @params)
        {

            Log.Info("Client", "Starting client service");

            // connect to server
            Log.Info("Client", "Connecting to server");
            try
            {
                clientSocket = new Socket(address, port);
                taskListener.OnConnected(false);
                DataInputStream inputStream = new DataInputStream(clientSocket.InputStream);
                DataOutputStream outputStream = new DataOutputStream(clientSocket.OutputStream);

                // 1. clients sends file first

                // prepare file to send
                foreach (string file in files) {
                    if (System.IO.File.Exists(file) || System.IO.Directory.Exists(file)) {
                        // if it is a file or directory
                        sendOneFile(file, inputStream, outputStream, "");
                    } 
                }

                // send 0 as a 64-bit long integer to indicate end
                Log.Info(id, "Finished. Send end signal");
                byte[] sizeData = BitConverter.GetBytes((long)0);
                outputStream.Write(sizeData, 0, sizeof(long));

                return "Success";


            } catch (Java.Lang.Exception e) {
                Log.Info(id, "Exception caught: " + e.Message);
                return "failed";
            } finally {
				Log.Info(id, "Finished, closing");


				if (clientSocket != null)
				{
                    if (clientSocket.IsConnected)
					{
                        clientSocket.Close();
					}
				}
            }

        }

        private void sendOneFile(string file, DataInputStream inputStream, DataOutputStream outputStream, string relativePath) {

            // if path is a file
            if (System.IO.File.Exists(file)) {
                FileStream filestream = System.IO.File.OpenRead(file);
                Log.Info(id, "Prepare to send file");

                // 1.1 send file name size as long integer
                Log.Debug(id, "Sending file name size to server");
                string fileName = Path.GetFileName(filestream.Name);
                fileName = relativePath + "/" + fileName;
                byte[] name = Encoding.Default.GetBytes(fileName);
                byte[] sizeData = BitConverter.GetBytes(name.LongLength);
                outputStream.Write(sizeData, 0, sizeof(long));

                // 1.2 send file name
                Log.Debug(id, "Sending file name to server");
                outputStream.Write(name, 0, name.Length);

                // 1.3 send size of file as a 64-bit (8 bytes) long integer
                Log.Info(id, "Sending file size to server");
                sizeData = BitConverter.GetBytes(filestream.Length);
                outputStream.Write(sizeData, 0, sizeof(long));

                // 1.4 send file
                Log.Info(id, "Sending file to server");
                //buf = new byte[filestream.Length];
                int bytesToRead = (int)filestream.Length;
                int bytesRead = 0;

                do
                {
                    int r = 65936;
                    if (bytesToRead < 65936)
                        r = bytesToRead;
                    int len = filestream.Read(buf, 0, r);

                    // send
                    outputStream.Write(buf, 0, len);

                    bytesRead += len;
                    bytesToRead -= len;
                } while (bytesToRead > 0);

                Log.Info(id, bytesRead + " bytes sent");

                filestream.Close();

                // wait for client's response
                Log.Info(id, "Waiting to hear from server");

                // wait till data available or timed out
                int elapsed = 0;
                while (inputStream.Available() == 0 && elapsed < 7000)
                {
                    Thread.Sleep(1000);
                    elapsed += 1000;
                }

                // if timed out
                if (elapsed >= 7000)
                {
                    Log.Info(id, "Wait timed out. Give up.");
                    //break;
                    return;
                }
                else
                { // receive
                    inputStream.Read(buf, 0, sizeof(long));
                }
            } else {
                // path is a directory
                string[] children = Directory.GetFileSystemEntries(file);
                string dirName = Path.GetFileName(file);
                foreach (string child in children) {
                    sendOneFile(child, inputStream, outputStream, relativePath + "/" + dirName);
                }
            }
        }

        protected override void OnPostExecute(Java.Lang.Object result)
        {
            taskListener.OnDisconnected(false);
        }
    }
}
