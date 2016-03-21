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

namespace JMail
{
    /// <summary>
    /// Interaction logic for AccountProps.xaml
    /// </summary>
    public partial class AccountProps: Window
    {
        AccountInfo account_;

        public AccountInfo Account { get { return account_; } }

        public AccountProps()
            : this(new AccountInfo())
        {
        }

        public AccountProps(AccountInfo account)
        {
            account_ = account;
            DataContext = account_;

            InitializeComponent();

            proto_.ItemsSource = Enum.GetNames(typeof(Protocol));

            if (account_.SecurePassword != null)
            {
                passwordBox_.Password = account_.GetPassword();
            }
        }

        private void Ok_Clicked(object sender, RoutedEventArgs e)
        {
            DialogResult = true;

            account_.SecurePassword = passwordBox_.SecurePassword;

            Close();
        }

        private void Cancel_Clicked(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
