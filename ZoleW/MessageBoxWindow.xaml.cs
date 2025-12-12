using System.ComponentModel;
using System.Windows;
using MahApps.Metro.Controls;

namespace ZoleW
{
    /// <summary>
    /// Interaction logic for MessageBoxWindow.xaml
    /// </summary>
    public partial class MessageBoxWindow : MetroWindow, INotifyPropertyChanged
    {
        public MessageBoxWindow()
        {
            InitializeComponent();
        }

        public static MessageBoxResult Show(Window owner, string msg, string title = "", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None)
        {
            var dialog = new MessageBoxWindow();
            dialog.MyTitle = title;
            dialog.Message = msg;
            dialog.imInfo.Visibility = image == MessageBoxImage.Information ? Visibility.Visible : Visibility.Collapsed;
            dialog.imAlert.Visibility = image == MessageBoxImage.Warning ? Visibility.Visible : Visibility.Collapsed;
            dialog.imHelp.Visibility = image == MessageBoxImage.Question ? Visibility.Visible : Visibility.Collapsed;
            dialog.btOK.Visibility = buttons == MessageBoxButton.OK || buttons == MessageBoxButton.OKCancel? Visibility.Visible : Visibility.Collapsed;
            dialog.btCancel.Visibility = buttons == MessageBoxButton.OKCancel || buttons == MessageBoxButton.YesNoCancel ? Visibility.Visible : Visibility.Collapsed;
            dialog.btYes.Visibility = buttons == MessageBoxButton.YesNo || buttons == MessageBoxButton.YesNoCancel ? Visibility.Visible : Visibility.Collapsed;
            dialog.btNo.Visibility = dialog.btYes.Visibility;
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialog.ShowDialog();
            dialog.Close();
            return dialog.MyResult;
        }

        private string _MyTitle = "";
        private string _Message = "";
        private MessageBoxResult MyResult = MessageBoxResult.Cancel;

        public string MyTitle 
        {
            get { return _MyTitle; }
            set
            {
                if (_MyTitle == value) return;
                _MyTitle = value;
                OnPropertyChanged("MyTitle");
            }
        }

        public string Message
        {
            get { return _Message; }
            set
            {
                if (_Message == value) return;
                _Message = value;
                OnPropertyChanged("Message");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void btOK_Click(object sender, RoutedEventArgs e)
        {
            MyResult = MessageBoxResult.OK;
            DialogResult = true;
        }

        private void btYes_Click(object sender, RoutedEventArgs e)
        {
            MyResult = MessageBoxResult.Yes;
            DialogResult = true;
        }
        private void btNo_Click(object sender, RoutedEventArgs e)
        {
            MyResult = MessageBoxResult.No;
            DialogResult = true;
        }

    }
}
