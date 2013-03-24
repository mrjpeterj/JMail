using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace JMail
{
    public class FolderView: INotifyPropertyChanged
    {
        private MessageHeader currentMessage_;

        public Folder Folder { get; private set; }

        public MessageStore Messages
        {
            get
            {
                return Folder.Messages;
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
                if (currentMessage_ != null)
                {
                    currentMessage_.PropertyChanged -= MessageChanged;
                }

                currentMessage_ = value;

                if (currentMessage_ != null)
                {
                    currentMessage_.PropertyChanged += MessageChanged;
                }

                ReportChange();
            }
        }

        public bool IsMessage { get { return CurrentMessage != null; } }
        public bool IsUnread { get { return IsMessage && CurrentMessage.UnRead; } }
        public bool IsRead { get { return IsMessage && !CurrentMessage.UnRead; } }
        public bool IsNotDeleted { get { return IsMessage && !CurrentMessage.Deleted; } }
        public bool IsDeleted { get { return IsMessage && CurrentMessage.Deleted; } }

        public FolderView(Folder f)
        {
            Folder = f;
        }

        public void Select()
        {
            Folder.Select();
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
}
