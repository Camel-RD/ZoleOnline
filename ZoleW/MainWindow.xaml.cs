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
using System.Windows.Navigation;
using System.Windows.Shapes;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;

namespace ZoleW
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            GameController = new GameController(this);
        }

        GameController GameController = null;

        public void ShowMessage(string msg)
        {
            var mw = (this as MetroWindow);
            mw.ShowMessageAsync("", msg);
        }

        private void MetroWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.F5) return;
            if (!(Content is PageGame gp)) return;
            if (!(gp.DataContext is GamePageVM gvm)) return;
            gvm.IsInDegugMode = !gvm.IsInDegugMode;
        }

        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            GameController?.DoOnGameClosing();
        }
    }
}
