using System;
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
        Imap server_;

        public MainWindow()
        {
            InitializeComponent();

            var accnt = new AccountInfo
            {
                Host = "mister-j.dyndns.org",
                Port = 143,
                Username = "peterj",

            };

            server_ = new Imap(accnt);

            folderList_.ItemsSource = server_.FolderList;
            messageList_.ItemsSource = server_.MessageList;
        }

        private void SelectFolder(object sender, SelectionChangedEventArgs e)
        {
            Folder folder = folderList_.SelectedItem as Folder;

            if (folder != null)
            {
                server_.SelectFolder(folder);
            }
        }
    }
}
