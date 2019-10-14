using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.ComponentModel;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace JMail.Core
{
    public class Folder
    {
        IAccount server_;
        string name_;
        string shortName_;
        string separator_;

        BehaviorSubject<IEnumerable<Folder>> subFolders_;

        BehaviorSubject<IEnumerable<MessageHeader>> messages_;

        BehaviorSubject<IEnumerable<int>> filterIds_;

        bool canHaveMessages_;

        BehaviorSubject<int> exists_;
        BehaviorSubject<int> recent_;
        BehaviorSubject<int> unseen_;

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

        public IObservable<int> Exists
        {
            get;
            private set;
        }

        public IObservable<int> Recent
        {
            get
            {
                return recent_;
            }
        }

        public IObservable<int> Unseen
        {
            get;
            private set;
        }

        internal int ExistsValue
        {
            get
            {
                return exists_.Value;
            }
            set
            {
                exists_.OnNext(value);
            }
        }
        internal int RecentValue
        {
            get
            {
                return recent_.Value;
            }
            set
            {
                recent_.OnNext(value);
            }
        }
        internal int UnseenValue
        {
            get
            {
                return unseen_.Value;
            }
            set
            {
                unseen_.OnNext(value);
            }
        }

        public bool CanHaveMessages { get { return canHaveMessages_; } }

        public IAccount Server { get { return server_; } }

        // Any subfolders of this folder
        public IObservable<IEnumerable<Folder>> Children
        {
            get
            {
                return subFolders_;
            }
        }

        internal IEnumerable<Folder> ChildList
        {
            get
            {
                return subFolders_.Value;
            }
        }

        // All the messages in the folder
        public IObservable<IEnumerable<MessageHeader>> Messages
        {
            get
            {
                return messages_;
            }
        }

        // The current subset of messages in the folder that are being viewed
        public IObservable<IEnumerable<MessageHeader>> ViewMessages
        {
            get; private set;
        }

        internal IEnumerable<MessageHeader> MessageList
        {
            get
            {
                return messages_.Value;
            }
        }

        public Folder(IAccount server, string name, string shortName, string separator, bool hasChildren, bool canHaveMessages)
        {
            server_ = server;
            name_ = name;
            shortName_ = shortName;
            separator_ = separator;

            if (hasChildren)
            {
                subFolders_ = new BehaviorSubject<IEnumerable<Folder>>(new Folder[] { });
            }

            canHaveMessages_ = canHaveMessages;
            if (canHaveMessages)
            {
                filterIds_ = new BehaviorSubject<IEnumerable<int>>(null);

                messages_ = new BehaviorSubject<IEnumerable<MessageHeader>>(new MessageHeader[] { });

                ViewMessages = Observable.CombineLatest(messages_, filterIds_, FilterMessages);
            }

            exists_ = new BehaviorSubject<int>(0);
            recent_ = new BehaviorSubject<int>(0);
            unseen_ = new BehaviorSubject<int>(0);

            var msgCount = messages_.Select(msgs => msgs.Count());

            // Take the value as either the number of messages in the list or
            // just what the server has set for the folder.
            Exists = exists_.Merge(msgCount);


            var msgsUnRead = messages_.
                Select((msgs) =>
                {
                    // Build an observable list of all of the unread values.

                    return Observable.CombineLatest(msgs.Select(msg => msg.UnRead));
                }).Switch(). // .. and only listen to the newest one.
                Select(unreads =>
                {
                    // .. then count up the unread values in it.

                    return unreads.Where(unread => unread == true).Count();
                });

            // Take the value as either the number of messages with Unread set 
            // or just what the server has set for the folder.
            Unseen = unseen_.Merge(msgsUnRead);
        }

        public override string ToString()
        {
            return name_;
        }

        public MessageHeader MessageByID(int id)
        {
            var matches = from m in MessageList
                          where m.id == id
                          select m;

            return matches.FirstOrDefault();
        }

        public MessageHeader MessageByUID(int id)
        {
            var matches = from m in MessageList
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
        internal void Expunge(MessageHeader msg, int msgId)
        {
            var msgLst = from m in MessageList
                         where m.id > msgId
                         select m;

            foreach (var m in msgLst)
            {
                m.id = m.id - 1;
            }

            if (msg != null)
            {
                RemoveMessage(msg);
            }
        }

        public void AddMessage(MessageHeader msg)
        {
            var messages = messages_.Value.ToList();
            messages.Add(msg);

            messages_.OnNext(messages);
        }

        public void RemoveMessage(MessageHeader msg)
        {
            var messages = messages_.Value.ToList();
            messages.Remove(msg);

            messages_.OnNext(messages);
        }

        public void RemoveMessageByUID(int id)
        {
            var msg = MessageByUID(id);
            if (msg != null)
            {
                RemoveMessage(msg);
            }
        }

        public void SetFilterMsgIds(IEnumerable<int> msgIds)
        {
            filterIds_.OnNext(msgIds);
        }

        private IEnumerable<MessageHeader> FilterMessages(IEnumerable<MessageHeader> folderMessages, IEnumerable<int> filterIds)
        {
            if (filterIds == null)
            {
                return folderMessages;
            }
            else
            {
                List<MessageHeader> selected = new List<MessageHeader>();

                foreach (var id in filterIds)
                {
                    var msg = MessageByID(id);

                    if (msg != null)
                    {
                        selected.Add(msg);
                    }
                    else
                    {
                        int a = 0;
                    }
                }

                return selected;
            }
        }

        public void AddChild(Folder subFolder)
        {
            var children = subFolders_.Value.ToList();
            children.Add(subFolder);
            subFolders_.OnNext(children);
        }
    }
}
