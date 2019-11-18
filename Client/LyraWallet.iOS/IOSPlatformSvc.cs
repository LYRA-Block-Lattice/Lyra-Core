using LyraWallet.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

[assembly: Dependency(typeof(LyraWallet.iOS.IOSPlatformSvc))]
namespace LyraWallet.iOS
{
    public class IOSPlatformSvc : IPlatformSvc
    {
        public string GetStoragePath()
        {
            // we need to put in /Library/ on iOS5.1 to meet Apple's iCloud terms
            // (they don't want non-user-generated data in Documents)
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal); // Documents folder
            string libraryPath = Path.Combine(documentsPath, "..", "Library");
            return libraryPath + Path.PathSeparator;
        }
    }
}
