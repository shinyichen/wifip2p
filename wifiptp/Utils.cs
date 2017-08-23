using System;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using Android.Util;
using Java.IO;

namespace wifiptp
{
    public class Utils
    {
        //private static String p2pInt = "p2p-p2p0";

		public static String getIPFromMac(String MAC)
		{
			
			BufferedReader br = null;

			br = new BufferedReader(new FileReader("/proc/net/arp"));
			string line;
            while ((line = br.ReadLine()) != null)
			{

                string[] splitted = Regex.Split(line, " +");
                if (splitted != null && splitted.Length >= 4)
				{
					// Basic sanity check
					//string device = splitted[5];
     //               if (Regex.IsMatch(device, (".*" + p2pInt + ".*")))
					//{
						string mac = splitted[3];
                    Log.Info("Utils", splitted[0] + " -- " + splitted[1] + " -- " + splitted[2] + " -- " + splitted[3] + " -- " + splitted[4]);
                    if (String.Equals(mac, MAC, StringComparison.OrdinalIgnoreCase))
						{
							return splitted[0];
						}
					//}
				}
			}
	
            br.Close();
				
			return null;
		}

        public static String getIPFromMac2(string MAC)
        {
            NetworkInterface[] nis = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface ni in nis) {
                Log.Info("Utils", ni.ToString());
            }
            return null;
        }
    }
}
