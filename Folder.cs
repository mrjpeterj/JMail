using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.ComponentModel;

namespace Mail
{
    public class Folder: INotifyPropertyChanged
    {
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

        public Folder(string name, Folder parent = null)
        {
            name_ = name;
        }

        public override string ToString()
        {
            return name_;
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
