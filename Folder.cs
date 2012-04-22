﻿using System;
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
        int unseenStart_;

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

                UnseenStart = value + 1;
            }
        }

        public int Recent
        {
            get { return recent_; }
            set { recent_ = value; }
        }

        public int UnseenStart
        {
            get { return unseenStart_; }
            set
            {
                unseenStart_ = value;

                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("UnseenStart"));
                    PropertyChanged(this, new PropertyChangedEventArgs("Unseen"));
                    PropertyChanged(this, new PropertyChangedEventArgs("UnseenText"));
                }
            }
        }

        public int Unseen
        {
            get
            {
                if (exists_ == 0)
                {
                    return 0;
                }
                else
                {
                    return exists_ - unseenStart_ + 1;
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
