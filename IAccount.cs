using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMail
{
    public class MessagesChangedEventArgs: EventArgs
    {
        public Folder Folder { get; private set; }

        public MessagesChangedEventArgs(Folder f)
        {
            Folder = f;
        }
    }

    public interface IAccount
    {
        event EventHandler FoldersChanged;
        event EventHandler<MessagesChangedEventArgs> MessagesChanged;

        IEnumerable<Folder> FolderList { get; }
        IEnumerable<Folder> AllFolders { get; }

        // Actions on folders
        void SelectFolder(Folder f);
        void UnselectFolder(Folder f);
        void RenameFolder(string oldName, string newName);
        void SubscribeFolder(string folderName);
        
        // Actions on the Current folder
        void FetchMessage(MessageHeader m, BodyPart p);
        void SetFlag(MessageHeader m, MessageFlags flags, bool isSet);
        void ExpungeFolder();        

        // General server actions
        void PollFolders();
    }
}
