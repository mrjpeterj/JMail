using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.ComponentModel;

namespace Mail
{
    public class Folder: INotifyPropertyChanged
    {
        IAccount server_;
        string name_;
        List<Folder> subFolders_;

        int exists_;
        int recent_;
        int unseen_;

        public string FullName
        {
            get
            {
                return name_;
            }
        }

        public int Exists
        {
            get { return exists_; }
            set
            {
                exists_ = value;

                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("Exists"));
                }
            }
        }

        public int Recent
        {
            get { return recent_; }
            set { recent_ = value; }
        }

        public int Unseen
        {
            get
            {
                return unseen_;
            }

            set
            {
                unseen_ = value;

                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("Unseen"));
                    PropertyChanged(this, new PropertyChangedEventArgs("UnseenText"));
                }                
            }
        }

        public string UnseenText
        {
            get
            {
                if (Unseen == 0)
                {
                    return string.Empty;
                }
                else
                {
                    return "(" + Unseen + ")";
                }
            }
        }

        public IAccount Server { get { return server_; } }

        public Folder(IAccount server, string name, Folder parent = null)
        {
            server_ = server;
            name_ = name;
        }

        public override string ToString()
        {
            return name_;
        }

        public void Select()
        {
            server_.SelectFolder(this);
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
