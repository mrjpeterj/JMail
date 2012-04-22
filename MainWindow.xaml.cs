using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Mail
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow: Window
    {
        public IList<AccountInfo> Servers { get; private set; }

        public MainWindow()
        {
            Servers = Properties.Settings.Default.Accounts;

            DataContext = this;
            InitializeComponent();

            if (Servers != null)
            {
                foreach (var s in Servers)
                {
                    s.Connect();
                }
            }
        }

        private void SelectFolder(object sender, RoutedEventArgs e)
        {
            var item = e.OriginalSource as TreeViewItem;
            var folder = item.DataContext as Folder;

            if (folder != null)
            {
                folder.Select();

                messageList_.ItemsSource = folder.Server.MessageList;
            }
        }

        private void Account_Create(object sender, RoutedEventArgs e)
        {
            var dialog = new AddAccount();
            dialog.Owner = this;
            dialog.ShowDialog();
        }
    }
}
