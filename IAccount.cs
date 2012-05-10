using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mail
{
    public interface IAccount
    {
        IEnumerable<Folder> FolderList { get; }
        IEnumerable<Folder> AllFolders { get; }

        IEnumerable<MessageHeader> MessageList { get; }

        void SelectFolder(Folder f);
        void FetchMessage(MessageHeader m, BodyPart p);
    }
}
