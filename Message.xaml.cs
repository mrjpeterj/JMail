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
            MessageHeader nextMessage = currentMessage.Next();
            if (nextMessage != null)
            {
                currentMessage.Body.PropertyChanged -= BodyChanged;

                DataContext = nextMessage;

                UpdateContent();

                nextMessage.Body.PropertyChanged += BodyChanged;
            }
        }

        private void PreviousMessage(object sender, RoutedEventArgs e)
        {
            MessageHeader currentMessage = DataContext as MessageHeader;
            MessageHeader nextMessage = currentMessage.Prev();
            if (nextMessage != null)
            {
                currentMessage.Body.PropertyChanged -= BodyChanged;

                DataContext = nextMessage;

                UpdateContent();

                nextMessage.Body.PropertyChanged += BodyChanged;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            MessageHeader currentMessage = DataContext as MessageHeader;
            currentMessage.Body.PropertyChanged += BodyChanged;

            UpdateContent();
        }

        private void UpdateContent()
        {
            MessageHeader currentMessage = DataContext as MessageHeader;
            if (currentMessage.Body.Text != null)
            {
                content_.Children.Clear();

                if (currentMessage.Body.ContentType.MediaType.EndsWith("/plain"))
                {
                    TextBox plainText = new TextBox();
                    plainText.Text = currentMessage.Body.Text;
                    plainText.Style = Resources["PlainTextStyle"] as Style;

                    content_.Children.Add(plainText);
                }
                else
                {
                    System.Windows.Forms.Integration.WindowsFormsHost host = new System.Windows.Forms.Integration.WindowsFormsHost();

                    System.Windows.Forms.WebBrowser htmlText = new System.Windows.Forms.WebBrowser();
                    host.Child = htmlText;

                    htmlText.AllowNavigation = true;
                    htmlText.AllowWebBrowserDrop = false;
                    htmlText.IsWebBrowserContextMenuEnabled = false;
                    htmlText.WebBrowserShortcutsEnabled = false;
                    htmlText.DocumentText = currentMessage.Body.Text;
                    htmlText.Navigating += htmlText_Navigating;

                    content_.Children.Add(host);
                }
            }
        }

        void htmlText_Navigating(object sender, System.Windows.Forms.WebBrowserNavigatingEventArgs e)
        {
            int a = 0;

             System.Diagnostics.Process p = System.Diagnostics.Process.Start(e.Url.ToString());

            e.Cancel = true;
            
        }

        private void BodyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateContent();
                }));
        }
    }
}
