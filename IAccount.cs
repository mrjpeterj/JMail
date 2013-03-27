﻿using System;
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
        void RenameFolder(string oldName, string newName);
        void SubscribeFolder(string folderName);

        void FetchMessage(MessageHeader m, BodyPart p);
        void SetFlag(MessageHeader m, MessageFlags flags, bool isSet);

        void Poll();
    }
}
