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
        AccountInfo account_;

        public AddAccount()
        {
            account_ = new AccountInfo();
            DataContext = account_;

            InitializeComponent();

            proto_.ItemsSource = Enum.GetNames(typeof(Protocol));
        }

        private void Ok_Clicked(object sender, RoutedEventArgs e)
        {
            DialogResult = true;

            account_.SecurePassword = passwordBox_.SecurePassword;
            account_.Save();

            Close();
        }

        private void Cancel_Clicked(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
