﻿using System;
using System.IO;
using System.Net.Sockets;
using Android.Util;

namespace wifiptp
{
    public class Utils
    {
        public Utils()
        {
        }

		public static long CopyStream(Stream source, Stream target, long size)
		{
			int bufSize = 1024;
			byte[] buf = new byte[bufSize];

			int totalBytes = 0;
			int bytesRead = 0;


            // use time out
            source.ReadTimeout = 5000;
            try {

                while (size > 0)
                {
                    if ((bytesRead = source.Read(buf, 0, bufSize)) > 0)
                    {
                        target.Write(buf, 0, bytesRead);
                        totalBytes += bytesRead;
                        size -= bytesRead;
                        if (size < bufSize)
                            bufSize = (int)size;
                        //Log.Info("CopyStream", "loop: " + totalBytes);
                    }

                }

            } catch (IOException e) {
                // read timed out
                Log.Debug("CopyStream", "Read timed out");
                throw e;
            }
			
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
