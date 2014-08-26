using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace JMail
{
    public class FolderTree: ThreadedList<ServerView>
    {
        IList<AccountInfo> infos_;

        public FolderTree(IList<AccountInfo> servers)
        {
            infos_ = servers;

            foreach (var info in servers)
            {
                base.Add(new ServerView(info));
            }
        }

        public void Add(AccountInfo info)
        {
            Add(new ServerView(info));
        }

        public new void Add(ServerView srv)
        {
            infos_.Add(srv.Info);

            base.Add(srv);
        }

        public new void Remove(ServerView srv)
        {
            infos_.Remove(srv.Info);

            base.Remove(srv);
        }
    }

    public class ServerView: INotifyPropertyChanged
    {
        AccountInfo server_;
        IEnumerable<FolderView> folders_;

        public AccountInfo Info { get { return server_; } }

        public IAccount Server { get { return server_.Connection; } }
        public string Name { get { return server_.Name; } }

        public IEnumerable<FolderView> Folders { get { return folders_; } }

        public ServerView(AccountInfo server)
        {
            server_ = server;

            server_.Connect();

            server_.Connection.FoldersChanged += UpdateFolderList;
        }

        void UpdateFolderList(object sender, EventArgs e)
        {
            folders_ = from f in server_.Connection.FolderList
                       select new FolderView(f);

            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs("Folders"));
            }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
