using System.Collections.Generic;
using System.Linq;
using System.Windows;

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

        //AppServer Server = null;
        private List<MainWindow> WindowList = new List<MainWindow>();

        private void StarterWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            foreach(var w in WindowList)
                w.Close();
            WindowList.Clear();
        }

        private void btStartServer_Click(object sender, RoutedEventArgs e)
        {
            //Server = new AppServer(7777, "", 0, "", 0, "", "");
            //Server.Start();
        }

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
