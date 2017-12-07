using System;
using Java.IO;

namespace wifiptp
{
    public class MyFile : Java.Lang.Object
    {
        private File file;

        public File File {
            get {
                return file;
            }
        }
       
        public MyFile(File file) {
            this.file = file;
        }

        public override string ToString()
        {
            return file.Name;
        }
    }
}
