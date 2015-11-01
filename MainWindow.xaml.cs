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

        private MailView mailView_;

        public static System.Windows.Threading.Dispatcher MainDispatcher;
        
        public MainWindow()
        {
            if (Properties.Settings.Default.Accounts == null)
            {
                Properties.Settings.Default.Accounts = new AccountList();
            }

            mailView_ = new MailView(Properties.Settings.Default.Accounts);

            MainDispatcher = Dispatcher;

            DataContext = mailView_;
            InitializeComponent();

            poller_ = new System.Windows.Threading.DispatcherTimer();
            poller_.Interval = new TimeSpan(0, 10, 0);
            poller_.Tick += PollServers;

            poller_.Start();
        }

        void PollServers(object sender, EventArgs e)
        {
            mailView_.Poll();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            this.SetPlacement(Properties.Settings.Default.MainWinPos);
        }

        void OnLoaded(object sender, RoutedEventArgs e)
        {
        }

        void OnClosing(object sender, CancelEventArgs e)
        {
            e.Cancel = false;

            Properties.Settings.Default.MainWinPos = this.GetPlacement();
            Properties.Settings.Default.Save();
        }

        void OnClosed(object sender, EventArgs e)
        {            
            // Unselect the folder, so that we clean up 
            mailView_.Select(null);

            mailView_.Shutdown();
        }

        private void SelectFolder(object sender, RoutedEventArgs e)
        {
            var item = e.OriginalSource as TreeViewItem;
            var folder = item.DataContext as FolderView;

            if (mailView_.CurrentFolder != null)
            {
                mailView_.CurrentFolder.Messages.CollectionChanged -= UpdateSorting;
            }

            mailView_.Select(folder);

            if (folder != null)
            {
                folder.Messages.CollectionChanged += UpdateSorting;
            }
        }

        private void UpdateSorting(object sender, NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
                {
                    SortMessageList();
                }
            ));
        }

        private void SortMessageList()
        {
            // Make sure it updates the sorting of the list
            u_MessageList.Items.SortDescriptions.Clear();
            u_MessageList.Items.SortDescriptions.Add(new SortDescription("Sent", ListSortDirection.Ascending));
            u_MessageList.Items.SortDescriptions.Add(new SortDescription("id", ListSortDirection.Ascending));

            if (u_MessageList.Items.Count == mailView_.CurrentFolder.Messages.Count())
            {
                // Update the column sizes now the messages are all in.
                ResizeMessageColumns();
            }

            if (u_MessageList.Items.Count > 0)
            {
                u_MessageList.ScrollIntoView(u_MessageList.Items[u_MessageList.Items.Count - 1]);
            }
        }

        public MessageHeader NextMessage(MessageHeader current)
        {
            if (current.Folder == mailView_.CurrentFolder.Folder)
            {
                int pos = u_MessageList.Items.IndexOf(current);

                // If not found or the last one in the list
                if (pos == u_MessageList.Items.Count - 1 || pos < 0)
                {
                    return null;
                }

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

        public bool IsLastMessage(MessageHeader current)
        {
            if (current.Folder == mailView_.CurrentFolder.Folder)
            {
                int pos = u_MessageList.Items.IndexOf(current);

                return pos == u_MessageList.Items.Count - 1;
            }
            else
            {
                return false;
            }
        }

        public MessageHeader PrevMessage(MessageHeader current)
        {
            if (current.Folder == mailView_.CurrentFolder.Folder)
            {
                int pos = u_MessageList.Items.IndexOf(current);

                // If not found or the first item in the list
                if (pos <= 0)
                {
                    return null;
                }
                else 
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

        public bool IsFirstMessage(MessageHeader current)
        {
            if (current.Folder == mailView_.CurrentFolder.Folder)
            {
                int pos = u_MessageList.Items.IndexOf(current);

                return pos == 0;
            }
            else
            {
                return false;
            }
        }

        private void Account_Create(object sender, RoutedEventArgs e)
        {
            var dialog = new AccountProps();
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                mailView_.Servers.Add(dialog.Account);

                Properties.Settings.Default.Save();
            }
        }

        private void Account_Delete(object sender, RoutedEventArgs e)
        {
            FrameworkElement ele = sender as FrameworkElement;
            ServerView acnt = ele.DataContext as ServerView;

            mailView_.Servers.Remove(acnt);

            Properties.Settings.Default.Save();
        }

        private void Account_Edit(object sender, RoutedEventArgs e)
        {
            FrameworkElement ele = sender as FrameworkElement;
            ServerView acnt = ele.DataContext as ServerView;

            AccountProps props = new AccountProps(acnt.Info);
            if (props.ShowDialog() == true)
            {
                mailView_.Servers.Remove(acnt);
                mailView_.Servers.Add(acnt);

                Properties.Settings.Default.Save();

                acnt.Reset();
            }
        }

        private void Folder_Rename(object sender, RoutedEventArgs e)
        {
            var ele = (System.Windows.FrameworkElement)sender;
            var host = ele.Parent as System.Windows.Controls.ContextMenu;
            var item = host.PlacementTarget as System.Windows.FrameworkElement;
            var folder = item.DataContext as FolderView;

            var dlg = new Rename();
            dlg.Text = folder.Name;
            dlg.Owner = this;

            if (dlg.ShowDialog() == true)
            {
                string newName = dlg.Text.ToString();

                folder.Rename(newName);
            }
        }

        private void SelectMessage(object sender, SelectionChangedEventArgs e)
        {
            MessageHeader msg = u_MessageList.SelectedItem as MessageHeader;

            if (mailView_.CurrentFolder != null)
            {
                mailView_.CurrentFolder.CurrentMessage = msg;
            }
        }

        private void OpenMessage(object sender, RoutedEventArgs e)
        {
            MessageHeader msg = mailView_.CurrentFolder.CurrentMessage;

            msg.Fetch();

            Message m = new Message(mailView_.CurrentFolder, this);
            m.DataContext = msg;

            m.Show();
            m.Focus();
        }

        private void KeyboardMessageControl(object sender, KeyEventArgs e)
        {
            ListView ele = sender as ListView;

            foreach (var item in ele.SelectedItems)
            {
                var msg = item as MessageHeader;
                if (msg == null)
                {
                    continue;
                }

                if (e.Key == Key.Delete)
                {
                    msg.Deleted = true;

                    e.Handled = true;
                }
                if (e.Key == Key.Q && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    msg.UnRead = !msg.UnRead;
                }
            }
        }

        private void MessageRead(object sender, RoutedEventArgs e)
        {
            var menu = sender as MenuItem;
            var menuHolder = menu.Parent as FrameworkElement;
            var popup = menuHolder.Parent as System.Windows.Controls.Primitives.Popup;

            ListView ele = popup.PlacementTarget as ListView;

            foreach (var item in ele.SelectedItems)
            {
                var msg = item as MessageHeader;
                if (msg == null)
                {
                    continue;
                }

                msg.UnRead = false;
            }
        }

        private void MessageUnread(object sender, RoutedEventArgs e)
        {
            var menu = sender as MenuItem;
            var menuHolder = menu.Parent as FrameworkElement;
            var popup = menuHolder.Parent as System.Windows.Controls.Primitives.Popup;

            ListView ele = popup.PlacementTarget as ListView;

            foreach (var item in ele.SelectedItems)
            {
                var msg = item as MessageHeader;
                if (msg == null)
                {
                    continue;
                }

                msg.UnRead = true;
            }
        }

        private void MessageDelete(object sender, RoutedEventArgs e)
        {
            var menu = sender as MenuItem;
            var menuHolder = menu.Parent as FrameworkElement;
            var popup = menuHolder.Parent as System.Windows.Controls.Primitives.Popup;

            ListView ele = popup.PlacementTarget as ListView;

            foreach (var item in ele.SelectedItems)
            {
                var msg = item as MessageHeader;
                if (msg == null)
                {
                    continue;
                }

                msg.Deleted = true;
            }
        }

        private void MessageUndelete(object sender, RoutedEventArgs e)
        {
            var menu = sender as MenuItem;
            var menuHolder = menu.Parent as FrameworkElement;
            var popup = menuHolder.Parent as System.Windows.Controls.Primitives.Popup;

            ListView ele = popup.PlacementTarget as ListView;

            foreach (var item in ele.SelectedItems)
            {
                var msg = item as MessageHeader;
                if (msg == null)
                {
                    continue;
                }

                msg.Deleted = false;
            }
        }

        private void MessageProps(object sender, RoutedEventArgs e)
        {
            MessageHeader msg = mailView_.CurrentFolder.CurrentMessage;
            msg.FetchWhole();

            MessageProps props = new MessageProps(msg, this);
            props.Show();
        }

        private void MessageListResized(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged)
            {
                ResizeMessageColumns();
            }
        }

        private void ResizeMessageColumns()
        {
            var kids = VisualTreeHelper.GetChildrenCount(u_MessageList);
            ScrollViewer scroller = null;
            for (int i = 0; i < kids; ++i)
            {
                var kid = VisualTreeHelper.GetChild(u_MessageList, i) as Decorator;

                if (kid != null)
                {
                    scroller = kid.Child as ScrollViewer;

                    if (scroller != null)
                    {
                        break;
                    }
                }
            }

            GridView view = u_MessageList.View as GridView;
            GridViewColumn subjectColumn = null;

            // Re-init columns so that they recalculate the layout
            foreach (var col in view.Columns)
            {
                col.Width = 0;
                col.Width = float.NaN;

                if ((string)col.Header == "Subject")
                {
                    subjectColumn = col;
                }
            }

            u_MessageList.UpdateLayout();

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
                    reqWidth = u_MessageList.ActualWidth;
                }

                reqWidth -= (colWidth - subjectColumn.ActualWidth);

                //if (reqWidth > subjectColumn.ActualWidth)
                //{
                    subjectColumn.Width = reqWidth;
                //}
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
            return valueStr.ToString(culture.DateTimeFormat.ShortDatePattern + " " + culture.DateTimeFormat.ShortTimePattern);
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
