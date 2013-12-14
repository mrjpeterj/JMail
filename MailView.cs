using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace JMail
{
    public class MailView: INotifyPropertyChanged
    {
        public FolderTree Servers { get; private set; }
        public FolderView CurrentFolder { get; private set; }

        public MailView(AccountList accounts)
        {
            Servers = new FolderTree(accounts);
        }

        public void Poll()
        {
            foreach (var server in Servers)
            {
                if (server.Server != null)
                {
                    server.Server.PollFolders();
                }
            }
        }

        public void Select(FolderView folder)
        {
            if (CurrentFolder != null)
            {
                CurrentFolder.Expunge();
                CurrentFolder.Unselect();
            }

            CurrentFolder = folder;

            if (CurrentFolder != null)
            {
                CurrentFolder.Select();
            }

            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs("CurrentFolder"));
            }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
