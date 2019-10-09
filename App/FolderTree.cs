using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;

using JMail.Core;

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

        public ServerView Add(AccountInfo info)
        {
            ServerView srv = new ServerView(info);

            Add(srv);

            return srv;
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
        public event EventHandler<AccountInfoEventArgs> AuthFailed;

        private AccountInfo server_;
        private IEnumerable<FolderView> folders_;

        public AccountInfo Info { get { return server_; } }

        public IAccount Server { get { return server_.Connection; } }
        public string Name { get { return server_.Name; } }

        public IEnumerable<FolderView> Folders { get { return folders_; } }

        public ServerView(AccountInfo server)
        {
            server_ = server;

            Reset();   
        }

        public void Reset()
        {
            folders_ = null;

            if (server_.Enabled)
            {
                server_.Connection.FoldersChanged += UpdateFolderList;
                server_.Connection.AuthFailed += OnAuthFailed;

                server_.Connect();
            }
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

        void OnAuthFailed(object sender, EventArgs e)
        {
            if (AuthFailed != null)
            {
                AuthFailed(this, new AccountInfoEventArgs { Account = Info });
            }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
