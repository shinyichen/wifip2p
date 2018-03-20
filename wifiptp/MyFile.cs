using System;
using Android.Graphics;
using Java.IO;

namespace wifiptp
{
    public class MyFile : Java.Lang.Object
    {
        private File file;

        private string type;

        public string Type {
            get {
                return type;
            }
        }

        private Bitmap thumbnail = null;

        public Bitmap Thumbnail {
            get {
                return thumbnail;
            }
            set {
                this.thumbnail = value;
            }
        }

        public File File {
            get {
                return file;
            }
        }
       
        public MyFile(File file) {
            this.file = file;

            if (file.ToString().Contains(".doc") || file.ToString().Contains(".docx"))
            {
                // Word document
                type = "application/msword";
            }
            else if (file.ToString().Contains(".pdf"))
            {
                // PDF file
                type = "application/pdf";
            }
            else if (file.ToString().Contains(".ppt") || file.ToString().Contains(".pptx"))
            {
                // Powerpoint file
                type = "application/vnd.ms-powerpoint";
            }
            else if (file.ToString().Contains(".xls") || file.ToString().Contains(".xlsx"))
            {
                // Excel file
                type = "application/vnd.ms-excel";
            }
            else if (file.ToString().Contains(".zip") || file.ToString().Contains(".rar"))
            {
                // WAV audio file
                type = "application/x-wav";
            }
            else if (file.ToString().Contains(".rtf"))
            {
                // RTF file
                type = "application/rtf";
            }
            else if (file.ToString().Contains(".wav") || file.ToString().Contains(".mp3"))
            {
                // WAV audio file
                type = "audio/x-wav";
            }
            else if (file.ToString().Contains(".gif"))
            {
                // GIF file
                type = "image/gif";
            }
            else if (file.ToString().Contains(".jpg") || file.ToString().Contains(".jpeg") || file.ToString().Contains(".png"))
            {
                // JPG file
                type = "image/jpeg";
            }
            else if (file.ToString().Contains(".txt"))
            {
                // Text file
                type = "text/plain";
            }
            else if (file.ToString().Contains(".3gp") || file.ToString().Contains(".mpg") || file.ToString().Contains(".mpeg") || file.ToString().Contains(".mpe") || file.ToString().Contains(".mp4") || file.ToString().Contains(".avi"))
            {
                // Video files
                type = "video/*";
            }
            else
            {
                type = "*/*";
            }
        }

        public override string ToString()
        {
            return file.Name;
        }

    }
}
