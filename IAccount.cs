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

        void SelectFolder(Folder f);
        void FetchMessage(MessageHeader m, BodyPart p);
        void DeleteMessage(MessageHeader m);

        void Poll();
    }
}
