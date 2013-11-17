using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace JMail
{
    public class MessageList: ThreadedList<MessageHeader>
    {
        Folder folder_;

        internal MessageList(Folder f)
        {
            folder_ = f;

        }

        public void Refresh()
        {
            Clear();

            foreach (var msg in folder_.Messages)
            {
                Add(msg);
            }
        }
    }

    public class FolderView: INotifyPropertyChanged
    {
        private MessageList messages_;
        private MessageHeader currentMessage_;

        public Folder Folder { get; private set; }

        public IEnumerable<FolderView> Folders { get; private set; }

        public MessageList Messages
        {
            get
            {
                return messages_;
            }
        }

        public MessageHeader CurrentMessage
        {
            get
            {
                return currentMessage_;
            }

            set
            {
                currentMessage_ = value;

                ReportChange();
            }
        }

        public bool IsMessage { get { return CurrentMessage != null; } }
        public bool IsUnread { get { return IsMessage && CurrentMessage.UnRead; } }
        public bool IsRead { get { return IsMessage && !CurrentMessage.UnRead; } }
        public bool IsNotDeleted { get { return IsMessage && !CurrentMessage.Deleted; } }
        public bool IsDeleted { get { return IsMessage && CurrentMessage.Deleted; } }

        public string Name { get { return Folder.Name; } }

        public string UnseenText { get { return Folder.UnseenText; } }
        
        public FolderView(Folder f)
        {
            Folder = f;

            if (f.Children != null)
            {
                Folders = from kid in f.Children
                          select new FolderView(kid);
            }

            messages_ = new MessageList(f);
        }

        public void Select()
        {
            Folder.Select();
        }

        public void Refresh()
        {
            messages_.Refresh();

            ReportChange();
        }

        public void Rename(string newName)
        {
            Folder.Rename(newName);
        }

        public MessageHeader Next(MainWindow view, MessageHeader msg)
        {
            MessageHeader nextMsg = null;

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
                nextMsg.Fetch();
            }

            return nextMsg;
        }

        public MessageHeader Prev(MainWindow view, MessageHeader msg)
        {
            MessageHeader nextMsg = null;

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
                nextMsg.Fetch();
            }

            return nextMsg;
        }

        public bool IsLast(MainWindow view, MessageHeader msg)
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

        public bool IsFirst(MainWindow view, MessageHeader msg)
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

        private void MessageChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "UnRead" || e.PropertyName == "Deleted")
            {
                ReportChange();
            }
        }

        private void ReportChange()
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs("CurrentMessage"));
                PropertyChanged(this, new PropertyChangedEventArgs("IsMessage"));
                PropertyChanged(this, new PropertyChangedEventArgs("IsUnread"));
                PropertyChanged(this, new PropertyChangedEventArgs("IsRead"));
                PropertyChanged(this, new PropertyChangedEventArgs("IsNotDeleted"));
                PropertyChanged(this, new PropertyChangedEventArgs("IsDeleted"));
            }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

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
