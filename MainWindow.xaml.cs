﻿using System;
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

                messageList_.ItemsSource = folder.Messages;
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
