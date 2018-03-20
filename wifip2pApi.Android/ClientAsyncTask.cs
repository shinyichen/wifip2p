using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Android.OS;
using Android.Util;

namespace wifip2pApi.Android
{
    public class ClientAsyncTask : AsyncTask
    {

        private const string id = "Client";

        private Socket clientSocket;

        private IPEndPoint remoteEP;

        private List<string> files;

        private ITaskProgress taskListener;

        private byte[] buf = new byte[1024];

        public ClientAsyncTask(Socket clientSocket, IPEndPoint remoteEP, List<string> files, ITaskProgress taskListener)
        {
            this.clientSocket = clientSocket;
            this.remoteEP = remoteEP;
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
                clientSocket.Connect(remoteEP);
                taskListener.OnConnected(false);

                // 1. clients sends file first

                // prepare image to send
                int count = 0;
                byte[] sizeData;
                foreach (string file in files) {
                    if (File.Exists(file)) {

                        FileStream filestream = new FileStream(file, FileMode.Open);
                        Log.Info(id, "Sending file " + count + 1);

                        // 1.1 send file name size as long integer
                        Log.Debug(id, "Sending file name size to server");
                        byte[] name = Encoding.ASCII.GetBytes(Path.GetFileName(filestream.Name));
                        sizeData = BitConverter.GetBytes(name.LongLength);
                        clientSocket.Send(sizeData, sizeof(long), SocketFlags.None);

                        // 1.2 send file name
                        Log.Debug(id, "Sending file name to server");
                        clientSocket.Send(name, name.Length, SocketFlags.None);

                        // 1.3 send size of file as a 64-bit (8 bytes) long integer
                        Log.Info(id, "Sending file size to server");
                        sizeData = BitConverter.GetBytes(filestream.Length);
                        clientSocket.Send(sizeData, sizeof(long), SocketFlags.None);

                        // 1.4 send file
                        Log.Info(id, "Sending file to server");
                        buf = new byte[filestream.Length];
                        int bytesToRead = (int)filestream.Length;
                        int bytesRead = 0;

                        do
                        {
                            int r = 1024;
                            if (bytesToRead < 1024)
                                r = bytesToRead;
                            int len = filestream.Read(buf, 0, r);

                            // send
                            clientSocket.Send(buf, len, SocketFlags.None);

                            bytesRead += len;
                            bytesToRead -= len;
                        } while (bytesToRead > 0);

                        Log.Info(id, bytesRead + " bytes sent");

                        count++;
                        filestream.Close();

                        // wait for client's response
                        Log.Info(id, "Waiting to hear from server");

                        // wait till data available or timed out
                        int elapsed = 0;
                        while (clientSocket.Available == 0 && elapsed < 7000) {
                            Thread.Sleep(1000);
                            elapsed += 1000;
                        }

                        // if timed out
                        if (elapsed >= 7000) {
                            Log.Info(id, "Wait timed out. Give up.");
                            break;
                        } else { // receive
                            clientSocket.Receive(buf, sizeof(long), SocketFlags.None);
                        }

                    }
                }

                // send 0 as a 64-bit long integer to indicate end
                Log.Info(id, "Finished. Send end signal");
                sizeData = BitConverter.GetBytes((long)0);
                clientSocket.Send(sizeData, sizeof(long), SocketFlags.None);

                return "Success";


            } catch (Java.Lang.Exception e) {
                Log.Info(id, "Exception caught: " + e.Message);
                return "failed";

            } catch (SocketException se) {
                Log.Info(id, "Exception caught: " + se.Message);
                return "failed";
            } finally {
				Log.Info(id, "Finished, closing");


				if (clientSocket != null)
				{
                    if (clientSocket.Connected)
					{
                        clientSocket.Shutdown(SocketShutdown.Both);
                        clientSocket.Close();
					}
				}
            }

        }

        protected override void OnPostExecute(Java.Lang.Object result)
        {
            taskListener.OnDisconnected(false);
        }
    }
}
