using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMail.Core
{
    public interface IAccount
    {
        /// <summary>
        /// Reported when the authentication for the account has failed.
        /// </summary>
        event EventHandler AuthFailed;

        IObservable<IEnumerable<Folder>> FolderList { get; }
        IObservable<IEnumerable<Folder>> AllFolders { get; }

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
