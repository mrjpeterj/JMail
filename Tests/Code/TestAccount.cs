using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

using JMail.Core;

namespace JMail
{
    class TestAccount: IAccount
    {
        public event EventHandler AuthFailed;

        private List<Folder> allFoldersList_;
        private List<Folder> topFoldersList_;

        private BehaviorSubject<IEnumerable<Folder>> allFolders_;
        private BehaviorSubject<IEnumerable<Folder>> topFolders_;

        private Folder currentFolder_;

        public IObservable<IEnumerable<Folder>> FolderList { get { return topFolders_; } }
        public IObservable<IEnumerable<Folder>> AllFolders { get { return allFolders_; } }

        public TestAccount()
        {
            allFoldersList_ = new List<Folder>();
            allFolders_ = new BehaviorSubject<IEnumerable<Folder>>(allFoldersList_);

            topFoldersList_ = new List<Folder>();
            topFolders_ = new BehaviorSubject<IEnumerable<Folder>>(topFoldersList_);
        }

        public void SelectFolder(Folder f)
        {
            currentFolder_ = f;
        }

        public void UnselectFolder(Folder f)
        {
            if (currentFolder_ == f)
            {
                currentFolder_ = null;
            }
        }

        public void RenameFolder(string oldName, string newName)
        {
            throw new NotImplementedException();
        }

        public void SubscribeFolder(string folderName)
        {
            throw new NotImplementedException();
        }

        public void FetchMessage(MessageHeader m, BodyPart p)
        {
            throw new NotImplementedException();
        }

        public void SetFlag(MessageHeader m, MessageFlags flags, bool isSet)
        {
            m.SetFlag(flags, isSet);
        }

        public void ExpungeFolder()
        {
            if (currentFolder_ != null)
            {
                currentFolder_.Messages.Take(1).Subscribe(msgs =>
                {
                    var deleted = msgs.Where(msg => msg.IsDeleted).ToList();

                    foreach (var d in deleted)
                    {
                        currentFolder_.RemoveMessage(d);
                    }
                });
            }
        }

        public void SearchFolder(string searchText)
        {
            throw new NotImplementedException();
        }

        public void SearchEnd()
        {
            throw new NotImplementedException();
        }

        public void Connect()
        {
            
        }

        public void PollFolders()
        {
            throw new NotImplementedException();
        }

        public void Shutdown()
        {
            
        }

        ///// Impl methods for controlling the data 
        public void AddFolder(Folder f)
        {
            allFoldersList_.Add(f);
            allFolders_.OnNext(allFoldersList_);
        }
    }
}
