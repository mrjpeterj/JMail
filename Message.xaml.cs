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
    /// Interaction logic for Message.xaml
    /// </summary>
    public partial class Message: Window
    {
        public Message()
        {
            InitializeComponent();
        }

        private void NextMessage(object sender, RoutedEventArgs e)
        {
            MessageHeader currentMessage = DataContext as MessageHeader;
            MessageHeader nextMessage = currentMessage.Folder.FindNext(currentMessage);
            if (nextMessage != null)
            {
                DataContext = nextMessage;
            }
        }

        private void PreviousMessage(object sender, RoutedEventArgs e)
        {
            MessageHeader currentMessage = DataContext as MessageHeader;
            MessageHeader nextMessage = currentMessage.Folder.FindPrev(currentMessage);
            if (nextMessage != null)
            {
                DataContext = nextMessage;
            }
        }
    }
}
