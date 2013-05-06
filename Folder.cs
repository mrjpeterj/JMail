using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.ComponentModel;

namespace JMail
{
    public class Folder: INotifyPropertyChanged
    {
        IAccount server_;
        string name_;
        string shortName_;
        string separator_;

        ThreadedList<Folder> subFolders_;
        MessageStore messages_;

        bool canHaveMessages_;

        int exists_;
        int recent_;
        int unseen_;

        public string FullName
        {
            get
            {
                return name_;
            }
        }

        public string Name
        {
            get
            {
                return shortName_;
            }            
        }

        public int Exists
        {
            get { return exists_; }
            set
            {
                exists_ = value;

                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("Exists"));
                }
            }
        }

        public int Recent
        {
            get { return recent_; }
            set { recent_ = value; }
        }

        public int Unseen
        {
            get
            {
                return unseen_;
            }

            set
            {
                unseen_ = value;

                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("Unseen"));
                    PropertyChanged(this, new PropertyChangedEventArgs("UnseenText"));
                }                
            }
        }

        public string UnseenText
        {
            get
            {
                if (Unseen == 0)
                {
                    return string.Empty;
                }
                else
                {
                    return "(" + Unseen + ")";
                }
            }
        }

        public IAccount Server { get { return server_; } }
        public IList<Folder> Children { get { return subFolders_; } }
        public MessageStore Messages { get { return messages_; } }

        public Folder(IAccount server, string name, string shortName, string separator, bool hasChildren, bool canHaveMessages)
        {
            server_ = server;
            name_ = name;
            shortName_ = shortName;
            separator_ = separator;

            if (hasChildren)
            {
                subFolders_ = new ThreadedList<Folder>();
            }

            canHaveMessages_ = canHaveMessages;
            if (canHaveMessages)
            {
                messages_ = new MessageStore();
            }
        }

        public override string ToString()
        {
            return name_;
        }

        public void Select()
        {
            if (canHaveMessages_)
            {
                server_.SelectFolder(this);
            }
        }

        public void Rename(string newName)
        {
            string newFullName = FullName.Replace(Name, "") + newName;

            server_.RenameFolder(FullName, newFullName);
        }

        // Called by the client to start the clean up of deleted messages.
        public void Expunge()
        {
            server_.ExpungeFolder();
        }        

        // Called by the server to report that a message has been removed.
        public void Expunge(MessageHeader msg, int msgId)
        {
            var msgLst = from m in messages_
                         where m.id > msgId
                         select m;

            foreach (var m in msgLst)
            {
                m.id = m.id - 1;
            }

            if (msg != null)
            {
                messages_.Remove(msg);
            }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
