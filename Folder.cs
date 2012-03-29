using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mail
{
    public class Folder
    {
        string name_;
        List<Folder> subFolders_;

        public string FullName
        {
            get
            {
                return name_;
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
    }
}
