using System;
using System.Collections.Generic;
using System.Text;

namespace LyraWallet.ViewModels
{
    public class BarcodeGenViewModel : BaseViewModel
    {
        private string _txt2Gen;

        public BarcodeGenViewModel(string txt2Gen)
        {
            _txt2Gen = txt2Gen;
        }
    }
}
