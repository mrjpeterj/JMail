﻿using System;
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

using System.Windows.Forms.Integration;

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
                UIElement existingChild = content_.Child;

                if (currentMessage.Body.ContentType.MediaType.EndsWith("/plain"))
                {
                    TextBox plainText = existingChild as TextBox;

                    if (plainText == null)
                    {
                        if (existingChild is IDisposable)
                        {
                            ((IDisposable)existingChild).Dispose();
                        }

                        plainText = new TextBox();
                        plainText.Style = Resources["PlainTextStyle"] as Style;
                        content_.Child = plainText;
                    }

                    plainText.Text = currentMessage.Body.Text;
                }
                else
                {
                    currentMessage.Body.Save();

                    System.Windows.Forms.WebBrowser htmlText = null;
                    WindowsFormsHost host = existingChild as WindowsFormsHost;

                    if (host == null)
                    {
                        host = new WindowsFormsHost();

                        htmlText = new System.Windows.Forms.WebBrowser();
                        host.Child = htmlText;

                        htmlText.AllowNavigation = true;
                        htmlText.AllowWebBrowserDrop = false;
                        htmlText.IsWebBrowserContextMenuEnabled = false;
                        htmlText.WebBrowserShortcutsEnabled = false;
                        htmlText.Navigating += htmlText_Navigating;
                        htmlText.DocumentCompleted += htmlText_Ready;

                        content_.Child = host;
                    }
                    else
                    {
                        htmlText = host.Child as System.Windows.Forms.WebBrowser;
                    }

                    htmlText.Navigate(currentMessage.Body.CacheFile);
                }
            }
        }

        void htmlText_Navigating(object sender, System.Windows.Forms.WebBrowserNavigatingEventArgs e)
        {
            if (e.Url.Scheme == "file")
            {
                return;
            }

            System.Diagnostics.Process p = System.Diagnostics.Process.Start(e.Url.ToString());

            e.Cancel = true;
        }

        void htmlText_Ready(object sender, System.Windows.Forms.WebBrowserDocumentCompletedEventArgs e)
        {
            System.Windows.Forms.WebBrowser htmlText = sender as System.Windows.Forms.WebBrowser;            

            foreach (var obj in htmlText.Document.Images)
            {
                var img = obj as System.Windows.Forms.HtmlElement;
                string imgSrc = img.GetAttribute("src");
                Uri imgSrcUri = new Uri(imgSrc);

                if (imgSrcUri.Scheme == "cid")
                {
                    // Need to fixup from related component of the message.
                    string refId = "<" + imgSrcUri.PathAndQuery + ">";

                    MessageHeader msg = DataContext as MessageHeader;
                    var imgPart = from r in msg.Related
                                  where r.Id == refId
                                  select r;

                    if (imgPart.Any())
                    {
                        var item = imgPart.First();
                        item.Save();

                        img.SetAttribute("src", item.CacheFile);
                    }
                }
            }
        }

        private void BodyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateContent();
                }));
        }

        private void SaveAttachment(object sender, RoutedEventArgs e)
        {
            FrameworkElement ele = (FrameworkElement)sender;

            BodyPart part = ele.DataContext as BodyPart;

            //part.Save();
        }

        private void OpenAttachment(object sender, RoutedEventArgs e)
        {
            FrameworkElement ele = (FrameworkElement)sender;

            BodyPart part = ele.DataContext as BodyPart;

            //part.Open();
        }

        private void ClickAttachment(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount > 1 && e.ChangedButton == MouseButton.Left)
            {
                OpenAttachment(sender, null);
            }
        }
    }
}
