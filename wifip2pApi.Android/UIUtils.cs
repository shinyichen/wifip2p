using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using Android.OS;
using Android.Util;

namespace wifip2pApi.Android
{
    public class UIUtils
    {
        public UIUtils()
        {
        }

		public static long CopyStream(AsyncTask task, Stream source, Stream target, long size) 
		{
            int bufSize = 65936;
			byte[] buf = new byte[bufSize];

			int totalBytes = 0;
			int bytesRead = 0;


            // use time out
            //source.ReadTimeout = 5000;
            //try {
            Stopwatch stopwatch = null;
            while (size > 0)
            {
                if (((ServerAsyncTask)task).closeConnectionRequested)
                    throw new Java.Lang.Exception("Interrupted");

                if ((bytesRead = source.Read(buf, 0, bufSize)) > 0)
                {
                    target.Write(buf, 0, bytesRead);
                    totalBytes += bytesRead;
                    size -= bytesRead;
                    if (size < bufSize)
                        bufSize = (int)size;
                    //Log.Info("CopyStream", "loop: " + totalBytes);

                    if (stopwatch != null && stopwatch.IsRunning)
                    {
                        stopwatch.Stop();
                        stopwatch.Reset();
                    }

                } else {
                    if (stopwatch == null)
                        stopwatch = Stopwatch.StartNew();
                    else {
                        if (stopwatch.ElapsedMilliseconds > 2000) {
                            // timeout
                            throw new TimeoutException("Read time out");
                        } 
                    }
                }

                    

            }

            //} catch (IOException e) {
            //    // read timed out
            //    Log.Debug("CopyStream", "Read timed out");
            //    throw e;
            //}
			
			return totalBytes;
		}

        public static long CopyStream(ServerService server, Stream source, Stream target, long size)
        {
            int bufSize = 65936;
            byte[] buf = new byte[bufSize];

            int totalBytes = 0;
            int bytesRead = 0;


            // use time out
            //source.ReadTimeout = 5000;
            //try {
            Stopwatch stopwatch = null;
            while (size > 0)
            {
                if (server.CloseConnectionImmediatelyRequested)
                    throw new Java.Lang.Exception("Interrupted");

                if ((bytesRead = source.Read(buf, 0, bufSize)) > 0)
                {
                    target.Write(buf, 0, bytesRead);
                    totalBytes += bytesRead;
                    size -= bytesRead;
                    if (size < bufSize)
                        bufSize = (int)size;
                    //Log.Info("CopyStream", "loop: " + totalBytes);

                    if (stopwatch != null && stopwatch.IsRunning)
                    {
                        stopwatch.Stop();
                        stopwatch.Reset();
                    }

                }
                else
                {
                    if (stopwatch == null)
                        stopwatch = Stopwatch.StartNew();
                    else
                    {
                        if (stopwatch.ElapsedMilliseconds > 2000)
                        {
                            // timeout
                            throw new TimeoutException("Read time out");
                        }
                    }
                }



            }

            //} catch (IOException e) {
            //    // read timed out
            //    Log.Debug("CopyStream", "Read timed out");
            //    throw e;
            //}

            return totalBytes;
        }

        public static long CopyStream(Socket socket, Stream target, long size)
        {
            int bufSize = 1024;
            byte[] buf = new byte[bufSize];

            int totalBytes = 0;
            int bytesRead = 0;


            // TODO could be in infinite loop, use time out
            while (size > 0)
            {
                if (socket.Available > 0 && ((bytesRead = socket.Receive(buf, bufSize, SocketFlags.None)) > 0))
                {
                    target.Write(buf, 0, bytesRead);
                    totalBytes += bytesRead;
                    size -= bytesRead;
                    if (size < bufSize)
                        bufSize = (int)size;
                    //Log.Info("CopyStream", "loop: " + totalBytes);
                }

            }
            return totalBytes;
        }
    }
}
