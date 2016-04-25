using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.ComponentModel;

namespace JMail
{
    public class Folder
    {
        IAccount server_;
        string name_;
        string shortName_;
        string separator_;

        List<Folder> subFolders_;
        List<MessageHeader> messages_;

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
            }
        }

        public bool CanHaveMessages { get { return canHaveMessages_; } }

        public IAccount Server { get { return server_; } }
        public IList<Folder> Children { get { return subFolders_; } }
        public IList<MessageHeader> Messages { get { return messages_; } }
        public IList<MessageHeader> ViewMessages { get; internal set; }

        public Folder(IAccount server, string name, string shortName, string separator, bool hasChildren, bool canHaveMessages)
        {
            server_ = server;
            name_ = name;
            shortName_ = shortName;
            separator_ = separator;

            if (hasChildren)
            {
                subFolders_ = new List<Folder>();
            }

            canHaveMessages_ = canHaveMessages;
            if (canHaveMessages)
            {
                messages_ = new List<MessageHeader>();

                ViewMessages = Messages;
            }
        }

        public override string ToString()
        {
            return name_;
        }

        public MessageHeader MessageByID(int id)
        {
            var matches = from m in Messages
                          where m.id == id
                          select m;

            return matches.FirstOrDefault();
        }

        public MessageHeader MessageByUID(int id)
        {
            var matches = from m in Messages
                          where m.Uid == id
                          select m;

            return matches.FirstOrDefault();
        }

        public void Select()
        {
            if (canHaveMessages_)
            {
                server_.SelectFolder(this);
            }
        }

        public void Unselect()
        {
            if (canHaveMessages_)
            {
                server_.UnselectFolder(this);
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
    }
}
