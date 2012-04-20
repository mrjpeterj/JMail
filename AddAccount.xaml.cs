using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Mail
{
    /// <summary>
    /// Interaction logic for AddAccount.xaml
    /// </summary>
    public partial class AddAccount: Window
    {
        public AddAccount()
        {
            InitializeComponent();
        }

        public void spam()
        {
            byte[] password = null;
            System.Security.Cryptography.ProtectedData.Protect(password);
        }
    }
}
