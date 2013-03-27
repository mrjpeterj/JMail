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
    /// Interaction logic for Rename.xaml
    /// </summary>
    public partial class Rename: Window
    {
        public static DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(Rename));

        public string Text
        {
            get
            {
                return GetValue(TextProperty).ToString();
            }
            set
            {
                SetValue(TextProperty, value);
            }
        }

        public Rename()
        {
            DataContext = this;

            InitializeComponent();
        }

        private void Ok(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
