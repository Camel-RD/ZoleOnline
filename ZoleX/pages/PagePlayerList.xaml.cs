using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace ZoleX
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PagePlayerList : ContentPage
    {
        public PagePlayerList()
        {
            InitializeComponent();
            if (DesignMode.IsDesignModeEnabled)
                BindingContext = LobbyPageVM.ST;
        }
    }
}