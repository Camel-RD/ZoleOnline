using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using GameLib;
using GameServerLib;
using Microsoft.FSharp.Control;

namespace ZoleW
{
    /// <summary>
    /// Interaction logic for StarterWindow.xaml
    /// </summary>
    public partial class StarterWindow : Window
    {
        public StarterWindow()
        {
            InitializeComponent();
            (App.Current as App).StarterWindow = this;
            Closing += StarterWindow_Closing;
        }

        private void StarterWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            foreach(var w in WindowList)
                w.Close();
            WindowList.Clear();
        }

        public IMsgTaker<MsgClientToServer> AddUser(IMsgTaker<MsgServerToClient> gwtoclient)
        {
            var user = Server.To.AddRawUserR(gwtoclient);
            return user.FromClient;
        }

        public void ConnectClient(AppClient client)
        {
            var gwtoclient = (client as IClient).FromServer;
            var user = Server.To.AddRawUserR(gwtoclient);
            var gwtoserver = user.FromClient;
            client.SetGWToServer(gwtoserver);
        }

        private void btStartServer_Click(object sender, RoutedEventArgs e)
        {
            Server = new AppServer(7777, "", 0, "", 0, "", "");
            Server.Start();
        }

        AppServer Server = null;
        private List<MainWindow> WindowList = new List<MainWindow>();

        private void btStart_Click(object sender, RoutedEventArgs e)
        {
            var W1 = new MainWindow();
            var lastw = WindowList.LastOrDefault();
            W1.Show();
            if(lastw == null)
            {
                W1.Left = 200;
                W1.Top = 10;
            }
            else
            {
                W1.Left = 200;
                W1.Top = lastw.Top + 300;
            }
            WindowList.Add(W1);


        }
    }
}
