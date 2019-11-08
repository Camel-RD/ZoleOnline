using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

[assembly: Xamarin.Forms.Dependency(typeof(ZoleX.Droid.Methods.AndroidMethods))]
namespace ZoleX.Droid
{
    class Methods
    {
        public class AndroidMethods : IAndroidMethods
        {
            public void CloseApp()
            {
                Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
            }
        }
    }
}