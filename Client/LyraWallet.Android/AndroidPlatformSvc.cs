using LyraWallet.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

[assembly: Dependency(typeof(LyraWallet.Droid.AndroidPlatformSvc))]
namespace LyraWallet.Droid
{
    public class AndroidPlatformSvc : IPlatformSvc
    {
        public string GetStoragePath()
        {
            string libraryPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            return libraryPath + Path.PathSeparator;
        }
    }
}
