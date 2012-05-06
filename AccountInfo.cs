using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

namespace Mail
{
    /// <summary>
    /// Used for serializing the settings
    /// </summary>
    [XmlInclude(typeof(AccountInfo))]
    public class AccountList: System.Collections.ArrayList, IList<AccountInfo>, INotifyCollectionChanged
    {
        #region IList<AccountInfo> Members
        public int IndexOf(AccountInfo item)
        {
            return base.IndexOf(item);
        }

        public void Insert(int index, AccountInfo item)
        {
            base.Insert(index, item);

            if (CollectionChanged != null) {
                CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        public new AccountInfo this[int index]
        {
            get
            {
                return (AccountInfo)base[index];
            }
            set
            {
                var oldItem = base[index];
                base[index] = value;

                if (CollectionChanged != null)
                {
                    CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, 
                                                                                 value, oldItem));
                }
            }
        }

        #endregion

        #region ICollection<AccountInfo> Members

        public void Add(AccountInfo item)
        {
            base.Add(item);

            if (CollectionChanged != null)
            {
                CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
            }
        }

        public bool Contains(AccountInfo item)
        {
            return base.Contains(item);
        }

        public void CopyTo(AccountInfo[] array, int arrayIndex)
        {
            base.CopyTo(array, arrayIndex);
        }

        public bool Remove(AccountInfo item)
        {
            bool exists = Contains(item);

            base.Remove(item);

            if (exists && CollectionChanged != null)
            {
                CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
            }

            return exists;
        }

        #endregion

        #region IEnumerable<AccountInfo> Members

        public new IEnumerator<AccountInfo> GetEnumerator()
        {
            List<AccountInfo> accounts = new List<AccountInfo>(base.Count);
            for (int i = 0; i < Count; ++i)
            {
                accounts.Add(this[i]);
            }

            return accounts.GetEnumerator();
        }

        #endregion

        #region INotifyCollectionChanged Members

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        #endregion
    }

    public enum Protocol
    {        
        IMAP4
    }

    public class AccountInfo: INotifyPropertyChanged
    {
        private Protocol proto_;
        private bool encrypt_;

        public string Host { get; set; }

        public Protocol Protocol
        {
            get { return proto_; }
            set
            {
                if (value == proto_) {
                    return;
                }

                proto_ = value;

                UpdatePort();
            }
        }
        public int Port { get; set; }
        public bool Encrypt
        {
            get
            {
                return encrypt_;
            }
            set
            {
                if (value == encrypt_)
                {
                    return;
                }

                encrypt_ = value;

                UpdatePort();
            }
        }

        public string Username { get; set; }

        [XmlIgnore]
        public SecureString SecurePassword { get; set; }

        public byte[] EncryptedPassword
        {
            get
            {
                byte[] password = Encoding.ASCII.GetBytes(GetPassword());
                byte[] encPasswd = ProtectedData.Protect(password, null, DataProtectionScope.CurrentUser);

                return encPasswd;
            }

            set
            {
                byte[] password = ProtectedData.Unprotect(value, null, DataProtectionScope.CurrentUser);
                string passwd = Encoding.ASCII.GetString(password);

                var spass = new SecureString();
                foreach (var c in passwd)
                {
                    spass.AppendChar(c);
                }

                SecurePassword = spass;
            }
        }

        [XmlIgnore]
        public IAccount Connection { get; private set; }

        public AccountInfo()
        {
            Protocol = Protocol.IMAP4;
            Port = 143;
        }
       
        public string GetPassword()
        {
            IntPtr bPwd = System.Runtime.InteropServices.Marshal.SecureStringToBSTR(SecurePassword);
            string strPwd = System.Runtime.InteropServices.Marshal.PtrToStringBSTR(bPwd);

            System.Runtime.InteropServices.Marshal.FreeBSTR(bPwd);

            return strPwd;
        }

        void UpdatePort()
        {
            switch (proto_)
            {
                case Protocol.IMAP4:
                    if (encrypt_)
                    {
                        Port = 993;
                    }
                    else
                    {
                        Port = 143;
                    }
                    break;
            }
        }

        public void Connect()
        {
            Connection = new Imap(this);

            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs("Connection"));
            }
        }


        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
