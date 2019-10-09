using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;

using JMail.Core;

namespace JMail
{
    public class MessageList: ThreadedList<MessageHeaderView>
    {
        Folder folder_;

        internal MessageList(Folder f)
        {
            folder_ = f;
        }

        public void Refresh(IEnumerable<MessageHeader> changedMessages)
        {
            if (changedMessages == null || !changedMessages.Any())
            {
                Clear();

                foreach (var msg in folder_.ViewMessages)
                {
                    Add(new MessageHeaderView(msg));
                }
            }
            else
            {
                // Don't need to rebuild the list, just update the specific messages.
                // But we have to find them in the list

                int noFound = 0;
                
                foreach (var msg in this)
                {
                    if (changedMessages.Contains(msg.Message))
                    {
                        msg.Dirty();

                        ++noFound;
                    }

                    if (noFound == changedMessages.Count())
                    {
                        break;
                    }
                }                
            }
        }
    }

    public class FolderView: INotifyPropertyChanged
    {
        private bool selected_;

        private MessageList messages_;
        private MessageHeaderView currentMessage_;

        public Folder Folder { get; private set; }

        public IEnumerable<FolderView> Folders { get; private set; }

        public MessageList Messages
        {
            get
            {
                return messages_;
            }
        }

        public MessageHeaderView CurrentMessage
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
        public bool IsUnread { get { return IsMessage && CurrentMessage.Message.UnRead; } }
        public bool IsRead { get { return IsMessage && !CurrentMessage.Message.UnRead; } }
        public bool IsNotDeleted { get { return IsMessage && !CurrentMessage.Message.Deleted; } }
        public bool IsDeleted { get { return IsMessage && CurrentMessage.Message.Deleted; } }

        public string Name { get { return Folder.Name; } }

        public string UnseenText
        {
            get
            {
                if (Folder.Unseen == 0)
                {
                    return string.Empty;
                }
                else
                {
                    return "(" + Folder.Unseen + ")";
                }
            }
        }
        
        public FolderView(Folder f)
        {
            Folder = f;

            if (f.Children != null)
            {
                Folders = from kid in f.Children
                          select new FolderView(kid);
            }

            messages_ = new MessageList(f);

            f.Server.MessagesChanged += Refresh;
        }

        public void Select()
        {
            selected_ = true;

            Folder.Select();
        }

        public void Unselect()
        {
            selected_ = false;

            Folder.Unselect();
        }

        public void Expunge()
        {
            Folder.Expunge();
        }

        public void Refresh(object sender, MessagesChangedEventArgs e)
        {
            if (e.Folder != Folder)
            {
                return;
            }

            if (selected_)
            {
                messages_.Refresh(e.Messages);
            }

            ReportChange();
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
                nextMsg.Message.Fetch();
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
                nextMsg.Message.Fetch();
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

                PropertyChanged(this, new PropertyChangedEventArgs("UnseenText"));
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
