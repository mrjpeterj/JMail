using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMail
{
    public interface IAccount
    {
        IEnumerable<Folder> FolderList { get; }
        IEnumerable<Folder> AllFolders { get; }

        // Actions on folders
        void SelectFolder(Folder f);
        void RenameFolder(string oldName, string newName);
        void SubscribeFolder(string folderName);
        
        // Actions on the Current folder
        void FetchMessage(MessageHeader m, BodyPart p);
        void SetFlag(MessageHeader m, MessageFlags flags, bool isSet);
        void ExpungeFolder();        

        // General server actions
        void Poll();
    }
}
