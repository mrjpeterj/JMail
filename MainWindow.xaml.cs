using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
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

using System.ComponentModel;

namespace Mail
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow: Window
    {
        private System.Windows.Threading.DispatcherTimer poller_;

        public static System.Windows.Threading.Dispatcher MainDispatcher;
        public IList<AccountInfo> Servers { get; private set; }

        public MainWindow()
        {
            if (Properties.Settings.Default.Accounts == null)
            {
                Properties.Settings.Default.Accounts = new AccountList();
            }

            MainDispatcher = Dispatcher;
   
            Servers = Properties.Settings.Default.Accounts;

            DataContext = this;
            InitializeComponent();

            poller_ = new System.Windows.Threading.DispatcherTimer();
            poller_.Interval = new TimeSpan(0, 10, 0);
            poller_.Tick += PollServers;

            poller_.Start();
        }

        void PollServers(object sender, EventArgs e)
        {
            foreach (var server in Servers)
            {
                if (server.Connection != null)
                {
                    server.Connection.Poll();
                }
            }
        }

        void OnLoaded(object sender, RoutedEventArgs e)
        {
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

                if (messageList_.ItemsSource is MessageStore)
                {
                    ((MessageStore)messageList_.ItemsSource).CollectionChanged -= UpdateSorting;
                }

                messageList_.ItemsSource = folder.Messages;
                folder.Messages.CollectionChanged += UpdateSorting;

                UpdateSorting(folder.Messages, null);
            }
        }

        private void UpdateSorting(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Make sure it updates the sorting of the list
            messageList_.Items.SortDescriptions.Clear();
            messageList_.Items.SortDescriptions.Add(new SortDescription("Sent", ListSortDirection.Ascending));
            messageList_.Items.SortDescriptions.Add(new SortDescription("id", ListSortDirection.Ascending));
        }

        private void Account_Create(object sender, RoutedEventArgs e)
        {
            var dialog = new AccountProps();
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                Servers.Add(dialog.Account);

                Properties.Settings.Default.Save();

                dialog.Account.Connect();
            }
        }

        private void Account_Delete(object sender, RoutedEventArgs e)
        {
            FrameworkElement ele = sender as FrameworkElement;
            AccountInfo acnt = ele.DataContext as AccountInfo;

            Servers.Remove(acnt);

            Properties.Settings.Default.Save();
        }

        private void Account_Edit(object sender, RoutedEventArgs e)
        {

        }

        private void OpenMessage(object sender, MouseButtonEventArgs e)
        {
            FrameworkElement ele = sender as FrameworkElement;
            MessageHeader msg = ele.DataContext as MessageHeader;

            msg.Fetch();

            Message m = new Message();
            m.DataContext = msg;

            m.Owner = this;
            m.Show();
        }
    }

    public class IsVisible: System.Windows.Data.IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool bVal = (bool)value;

            if (targetType != typeof(Visibility))
            {
                throw new NotImplementedException();                
            }
            
            if (bVal)
            {
                return Visibility.Visible;
            }
            else
            {
                return Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    public class DateDisplay: System.Windows.Data.IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (targetType != typeof(string))
            {
                throw new NotImplementedException();
            }

            culture = System.Globalization.CultureInfo.CurrentCulture;

            DateTime valueStr = (DateTime)value;
            return valueStr.ToString(culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    public class SizeDisplay: System.Windows.Data.IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (targetType != typeof(string))
            {
                throw new NotImplementedException();
            }

            if (value is long)
            {
                long sz = (long)value;
                return "" + (sz / 1024) + "KB";
            }

            if (value is int)
            {
                int sz = (int)value;
                return "" + (sz / 1024) + "KB";
            }

            throw new NotImplementedException();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
