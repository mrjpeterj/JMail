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

namespace JMail
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow: Window
    {
        private System.Windows.Threading.DispatcherTimer poller_;

        public static System.Windows.Threading.Dispatcher MainDispatcher;

        public IList<AccountInfo> Servers { get; private set; }
        public Folder CurrentFolder { get; private set; }

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
            poller_.Interval = new TimeSpan(0, 0, 20);
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
            CurrentFolder = item.DataContext as Folder;

            if (CurrentFolder != null)
            {
                CurrentFolder.Select();

                if (u_MessageList.ItemsSource is MessageStore)
                {
                    ((MessageStore)u_MessageList.ItemsSource).CollectionChanged -= UpdateSorting;
                }

                u_MessageList.ItemsSource = CurrentFolder.Messages;
                CurrentFolder.Messages.CollectionChanged += UpdateSorting;

                UpdateSorting(CurrentFolder.Messages, null);
            }
            else
            {
                u_MessageList.ItemsSource = null;
            }
        }

        private void UpdateSorting(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Make sure it updates the sorting of the list
            u_MessageList.Items.SortDescriptions.Clear();
            u_MessageList.Items.SortDescriptions.Add(new SortDescription("Sent", ListSortDirection.Ascending));
            u_MessageList.Items.SortDescriptions.Add(new SortDescription("id", ListSortDirection.Ascending));
        }

        public MessageHeader NextMessage(MessageHeader current)
        {
            if (current.Folder == CurrentFolder)
            {
                int pos = u_MessageList.Items.IndexOf(current);
                if (pos >= 0)
                {
                    u_MessageList.SelectedIndex = pos + 1;
                }

                return u_MessageList.SelectedItem as MessageHeader;
            }
            else
            {
                return null;
            }
        }

        public MessageHeader PrevMessage(MessageHeader current)
        {
            if (current.Folder == CurrentFolder)
            {
                int pos = u_MessageList.Items.IndexOf(current);
                if (pos >= 0)
                {
                    u_MessageList.SelectedIndex = pos - 1;
                }

                return u_MessageList.SelectedItem as MessageHeader;
            }
            else
            {
                return null;
            }
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
            FrameworkElement ele = sender as FrameworkElement;
            AccountInfo acnt = ele.DataContext as AccountInfo;

            AccountProps props = new AccountProps(acnt);
            props.ShowDialog();

            Servers.Remove(acnt);
            Servers.Add(acnt);

            Properties.Settings.Default.Save();

            acnt.Connect();
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

        private void MessageListResized(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged)
            {
                ListView list = sender as ListView;
                var kids = VisualTreeHelper.GetChildrenCount(list);
                ScrollViewer scroller = null;
                for (int i = 0; i < kids; ++i) 
                {
                    var kid = VisualTreeHelper.GetChild(list, i) as Decorator;

                    if (kid != null)
                    {
                        scroller = kid.Child as ScrollViewer;

                        if (scroller != null)
                        {
                            break;
                        }
                    }
                }

                GridView view = list.View as GridView;
                GridViewColumn subjectColumn = null;

                // Reinit columns so that the recalculate the layout
                foreach (var col in view.Columns)
                {
                    col.Width = 0;
                    col.Width = float.NaN;
                    
                    if ((string)col.Header == "Subject")
                    {
                        subjectColumn = col;
                    }
                }

                list.UpdateLayout();

                if (subjectColumn != null)
                {
                    double colWidth = 0;
                    foreach (var col in view.Columns)
                    {
                        colWidth += col.ActualWidth;
                    }

                    double reqWidth = double.NaN;
                    if (scroller != null)
                    {
                        reqWidth = ((ItemsPresenter)scroller.Content).ActualWidth;
                    }
                    else
                    {
                        reqWidth = list.ActualWidth;
                    }
                    
                    reqWidth -= (colWidth - subjectColumn.ActualWidth);

                    if (reqWidth > subjectColumn.ActualWidth)
                    {
                        subjectColumn.Width = reqWidth;
                    }
                }
            }
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
