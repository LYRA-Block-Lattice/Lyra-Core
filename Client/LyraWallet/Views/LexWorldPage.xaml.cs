﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace LyraWallet.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class LexWorldPage : ContentPage
    {
        public LexWorldPage()
        {
            InitializeComponent();

            Shell.SetTabBarIsVisible(this, false);
        }
    }
}