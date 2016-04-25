using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMail
{
    public class MessagesChangedEventArgs: EventArgs
    {
        public Folder Folder { get; private set; }
        public IEnumerable<MessageHeader> Messages { get; private set; }
        
        public MessagesChangedEventArgs(Folder f, IEnumerable<MessageHeader> msgs)
        {
            Folder = f;
            Messages = msgs;
        }
    }

    public interface IAccount
    {
        /// <summary>
        /// Triggered when the list of folders that the server reports has changed.
        /// This might just happen because the list loads asynchronously at startup.
        /// </summary>
        event EventHandler FoldersChanged;

        /// <summary>
        /// The status of the messages in a folder has changed.
        /// </summary>
        event EventHandler<MessagesChangedEventArgs> MessagesChanged;

        /// <summary>
        /// Reported when the authentication for the account has failed.
        /// </summary>
        event EventHandler AuthFailed;

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
        void SearchFolder(string searchText);
        void SearchEnd();

        // General server actions
        void Connect();
        void PollFolders();
        void Shutdown();
    }
}
