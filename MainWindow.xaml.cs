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

    public class FontConverter : IValueConverter
    {

        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            MessageHeader msg = value as MessageHeader;
            if (msg == null) {
                return value;
            }
            
            if (targetType == typeof(FontWeight))
            {
                if (msg.UnRead)
                {
                    return FontWeights.Bold;
                }
                else
                {
                    return FontWeights.Normal;
                }
            }
            else if (targetType == typeof(FontStyle))
            {
                if (msg.Deleted)
                {
                    return FontStyles.Oblique;
                }
                else
                {
                    return FontStyles.Normal;
                }
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
