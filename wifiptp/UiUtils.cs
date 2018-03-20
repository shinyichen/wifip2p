using System;
using System.IO;
using Android.Graphics;

namespace wifiptp
{
    public class UiUtils
    {
        public UiUtils()
        {
        }

        public static Bitmap GenerateBitmapFromImage(String path) {
            FileStream fs = File.OpenRead(path);
            Bitmap bitmap = BitmapFactory.DecodeStream(fs);
            int w = 64;
            int h =  bitmap.Height / (bitmap.Width / w);
            bitmap = Bitmap.CreateScaledBitmap(bitmap, w, h, false);
            return bitmap;
        }
    }
}
