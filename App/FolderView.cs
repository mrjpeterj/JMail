using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Text;

using JMail.Core;

namespace JMail
{
    public class FolderView: IChangingProperty
    {
        public Folder Folder { get; private set; }

        public IEnumerable<FolderView> Folders { get; private set; }

        public IEnumerable<MessageHeaderView> Messages { get; private set; }

        public MessageHeaderView CurrentMessage { get; set; }

        public bool IsMessage { get { return CurrentMessage != null; } }
        public bool IsUnread { get { return IsMessage && CurrentMessage.UnRead; } }
        public bool IsRead { get { return IsMessage && !CurrentMessage.UnRead; } }
        public bool IsNotDeleted { get { return IsMessage && !CurrentMessage.Deleted; } }
        public bool IsDeleted { get { return IsMessage && CurrentMessage.Deleted; } }

        public string Name { get { return Folder.Name; } }

        public string UnseenText { get; set; }      
        
        public FolderView(Folder f)
        {
            Folder = f;

            if (Folder.Children != null)
            {
                Folder.Children.Select((folders) =>
                {
                    return from kid in folders select new FolderView(kid);
                }).SubscribeTo(this, x => x.Folders);
            }

            if (Folder.CanHaveMessages)
            {
                Folder.ViewMessages
                    .Select((msgs) =>
                    {
                        return msgs.Select(msg => new MessageHeaderView(msg));
                    })
                    .SubscribeTo(this, x => x.Messages);

                Folder.Unseen.Select((val) =>
                {
                    if (val == 0)
                    {
                        return string.Empty;
                    }
                    else
                    {
                        return "(" + val + ")";
                    }
                }).
                SubscribeTo(this, x => x.UnseenText);
            }
        }

        public void Select()
        {
            Folder.Select();
        }

        public void Unselect()
        {
            Folder.Unselect();
        }

        public void Expunge()
        {
            Folder.Expunge();
        }

        public void Rename(string newName)
        {
            Folder.Rename(newName);
        }

        public MessageHeaderView Next(MainWindow view, MessageHeaderView msg)
        {
            MessageHeaderView nextMsg = null;

            if (view != null)
            {
                nextMsg = view.NextMessage(msg);
            }

            if (nextMsg == null)
            {
                //nextMsg = Folder.FindNext(this);
            }

            if (nextMsg != null)
            {
                nextMsg.Body.Fetch();
            }

            return nextMsg;
        }

        public MessageHeaderView Prev(MainWindow view, MessageHeaderView msg)
        {
            MessageHeaderView nextMsg = null;

            if (view != null)
            {
                nextMsg = view.PrevMessage(msg);
            }

            if (nextMsg == null)
            {
                //nextMsg = Folder.FindPrev(this);
            }
            
            if (nextMsg != null)
            {
                nextMsg.Body.Fetch();
            }

            return nextMsg;
        }

        public bool IsLast(MainWindow view, MessageHeaderView msg)
        {
            if (view != null)
            {
                return view.IsLastMessage(msg);
            }
            else
            {
                return false;
            }
        }

        public bool IsFirst(MainWindow view, MessageHeaderView msg)
        {
            if (view != null)
            {
                return view.IsFirstMessage(msg);
            }
            else
            {
                return false;
            }
        }

        #region IChangingProperty Members

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    public class AddressDisplay : System.Windows.Data.IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            System.Net.Mail.MailAddress addr = value as System.Net.Mail.MailAddress;
            if (addr == null)
            {
                return null;
            }

            if (targetType != typeof(string))
            {
                return null;
            }

            if (addr.DisplayName.Length > 0)
            {
                return addr.DisplayName;
            }
            else
            {
                return addr.Address;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
