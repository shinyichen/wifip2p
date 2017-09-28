using System;
using System.IO;
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
					Log.Info("CopyStream", "loop: " + totalBytes);
				}

			}
			return totalBytes;
		}
    }
}
