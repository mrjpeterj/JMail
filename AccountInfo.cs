using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Security;
using System.Security.Cryptography;

namespace Mail
{
    public class AccountInfo
    {
        public string Host { get; set; }
        public int Port { get; set; }

        public string Username { get; set; }
        public SecureString SecurePassword { get; set; }
        public string Password
        {
            get
            {
                IntPtr bPwd = System.Runtime.InteropServices.Marshal.SecureStringToBSTR(SecurePassword);
                string strPwd = System.Runtime.InteropServices.Marshal.PtrToStringBSTR(bPwd);

                System.Runtime.InteropServices.Marshal.FreeBSTR(bPwd);

                return strPwd;
            }
        }

        public void Save()
        {
            byte[] password = Encoding.ASCII.GetBytes(Password);
            
            byte[] encPasswd = ProtectedData.Protect(password, null, DataProtectionScope.CurrentUser);
        }
    }
}
