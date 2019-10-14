using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
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

    public class ServerView: IChangingProperty
    {
        public event EventHandler<AccountInfoEventArgs> AuthFailed;

        private AccountInfo server_;

        public AccountInfo Info { get { return server_; } }

        public IAccount Server { get { return server_.Connection; } }
        public string Name { get { return server_.Name; } }

        public IEnumerable<FolderView> Folders { get; set; }

        public ServerView(AccountInfo server)
        {
            server_ = server;

            Reset();   
        }

        public void Reset()
        {
            if (server_.Enabled)
            {
                server_.Connection.AuthFailed += OnAuthFailed;

                server_.Connection.FolderList
                    .Select((folders) =>
                    {
                        return from f in folders select new FolderView(f);
                    })
                    .SubscribeTo<IEnumerable<FolderView>, ServerView>(this, x => x.Folders);

                server_.Connect();
            }
        }

        void OnAuthFailed(object sender, EventArgs e)
        {
            if (AuthFailed != null)
            {
                AuthFailed(this, new AccountInfoEventArgs { Account = Info });
            }
        }

        #region IChangingProperty Members

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
