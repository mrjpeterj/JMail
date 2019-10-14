using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
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

namespace JMail
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow: Window
    {
        private System.Windows.Threading.DispatcherTimer poller_;

        private MailView mailView_;
        private IDisposable currentFolderSub_;

        public static System.Windows.Threading.Dispatcher MainDispatcher;

        public MainWindow()
        {
            if (Properties.Settings.Default.Accounts == null)
            {
                Properties.Settings.Default.Accounts = new Core.AccountList();
            }

            mailView_ = new MailView(Properties.Settings.Default.Accounts);

            foreach (var srv in mailView_.Servers)
            {
                srv.AuthFailed += OnAuthFailed;
            }

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

            // Clear the search text on folder change.
            u_search.Text = "";

            if (currentFolderSub_ != null)
            {
                currentFolderSub_.Dispose();
                currentFolderSub_ = null;
            }

            mailView_.Select(folder);

            if (folder != null)
            {
                currentFolderSub_ = folder.Folder.Messages.ObserveOn(Dispatcher).Subscribe((msgs) =>
                {
                    SortMessageList();
                });
            }
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

        public MessageHeaderView NextMessage(MessageHeaderView current)
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

                return u_MessageList.SelectedItem as MessageHeaderView;
            }
            else
            {
                return null;
            }
        }

        public bool IsLastMessage(MessageHeaderView current)
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

        public MessageHeaderView PrevMessage(MessageHeaderView current)
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

                return u_MessageList.SelectedItem as MessageHeaderView;
            }
            else
            {
                return null;
            }
        }

        public bool IsFirstMessage(MessageHeaderView current)
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
                var srv = mailView_.Servers.Add(dialog.Account);
                srv.AuthFailed += OnAuthFailed;

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
            MessageHeaderView msg = u_MessageList.SelectedItem as MessageHeaderView;

            if (mailView_.CurrentFolder != null)
            {
                mailView_.CurrentFolder.CurrentMessage = msg;
            }
        }

        private void OpenMessage(object sender, RoutedEventArgs e)
        {
            MessageHeaderView msg = mailView_.CurrentFolder.CurrentMessage;

            msg.Body.Fetch();

            Message m = new Message(mailView_.CurrentFolder, this);
            m.CurrentMessage = msg;

            m.Show();
            m.Focus();
        }

        private void KeyboardMessageControl(object sender, KeyEventArgs e)
        {
            ListView ele = sender as ListView;

            foreach (var item in ele.SelectedItems)
            {
                var msg = item as MessageHeaderView;
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
                var msg = item as MessageHeaderView;
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
                var msg = item as MessageHeaderView;
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
                var msg = item as MessageHeaderView;
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
                var msg = item as MessageHeaderView;
                if (msg == null)
                {
                    continue;
                }

                msg.Deleted = false;
            }
        }

        private void MessageProps(object sender, RoutedEventArgs e)
        {
            MessageHeaderView msg = mailView_.CurrentFolder.CurrentMessage;
            msg.FullMessage.Fetch();

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

        private void OnAuthFailed(object sender, Core.AccountInfoEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageBox.Show("Authentication failed for " + e.Account.Name);

                AccountProps props = new AccountProps(e.Account);
                if (props.ShowDialog() == true)
                {
                    ServerView srv = null;

                    foreach (var acnt in mailView_.Servers)
                    {
                        if (acnt.Info == e.Account)
                        {
                            srv = acnt;

                            break;
                        }
                    }

                    mailView_.Servers.Remove(srv);
                    mailView_.Servers.Add(srv);

                    Properties.Settings.Default.Save();

                    srv.Reset();
                }

            }));
        }

        private void ClearSearch(object sender, RoutedEventArgs e)
        {
            u_search.Text = "";
            mailView_.CurrentFolder.Folder.Server.SearchEnd();
        }

        private void UpdateSearch(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(u_search.Text))
            {
                mailView_.CurrentFolder.Folder.Server.SearchFolder(u_search.Text);
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
