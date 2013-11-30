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
                Add(info);
            }
        }

        public void Add(AccountInfo info)
        {
            infos_.Add(info);

            var view = new ServerView(info);
            //view.CollectionChanged += UpdateCollection;
            Add(view);
        }

        public void Remove(AccountInfo info)
        {
            infos_.Remove(info);

            var view = (from v in this
                        where v.Info == info
                        select v).SingleOrDefault();
            if (view != null)
            {
                Remove(view);
            }
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
