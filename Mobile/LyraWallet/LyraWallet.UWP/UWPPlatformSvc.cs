using LyraWallet.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

[assembly: Dependency(typeof(LyraWallet.UWP.UWPPlatformSvc))]
namespace LyraWallet.UWP
{
    public class UWPPlatformSvc : IPlatformSvc
    {
        public string GetStoragePath()
        {
            string libraryPath = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            return libraryPath + "\\";
        }
    }
}
