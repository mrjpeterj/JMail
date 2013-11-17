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

using System.Windows.Forms.Integration;

namespace JMail
{
    /// <summary>
    /// Interaction logic for Message.xaml
    /// </summary>
    public partial class Message: Window
    {
        FolderView folder_;

        public Message(FolderView folder)
        {
            folder_ = folder;

            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            MessageHeader currentMessage = DataContext as MessageHeader;

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

                    if (htmlText.Document != null)
                    {
                        // Doing this avoids the window playing the navigation sounds 
                        htmlText.Document.OpenNew(true);
                        htmlText.Document.Write(currentMessage.Body.Text);
                        htmlText_Ready(htmlText, null);
                    }
                    else
                    {
                        htmlText.DocumentText = currentMessage.Body.Text;
                    }
                }
            }
        }

        void htmlText_Navigating(object sender, System.Windows.Forms.WebBrowserNavigatingEventArgs e)
        {
            if (e.Url.Scheme == "file" || e.Url.Scheme == "about")
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
                        item.Save((bp) =>
                        {
                            img.SetAttribute("src", item.CacheFile);                            
                        });
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

            Cursor = Cursors.Wait;
            
            part.Save((p) =>
            {
                System.Diagnostics.Process.Start(part.CacheFile);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    Cursor = null;
                }));
            });
        }

        private void ClickAttachment(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount > 1 && e.ChangedButton == MouseButton.Left)
            {
                OpenAttachment(sender, null);
            }
        }

        private void NotLastItem(object sender, CanExecuteRoutedEventArgs e)
        {
            if (DataContext == null)
            {
                e.CanExecute = false;
            }
            else
            {
                MessageHeader currentMessage = DataContext as MessageHeader;
                e.CanExecute = !folder_.IsLast(Owner as MainWindow, currentMessage);
            }
        }

        private void NotFirstItem(object sender, CanExecuteRoutedEventArgs e)
        {
            if (DataContext == null)
            {
                e.CanExecute = false;
            }
            else
            {
                MessageHeader currentMessage = DataContext as MessageHeader;
                e.CanExecute = !folder_.IsFirst(Owner as MainWindow, currentMessage);
            }
        }

        private void NextMessage(object sender, ExecutedRoutedEventArgs e)
        {
            MessageHeader currentMessage = DataContext as MessageHeader;
            MessageHeader nextMessage = folder_.Next(Owner as MainWindow, currentMessage);

            DataContext = nextMessage;

            if (nextMessage != null)
            {
                UpdateContent();
            }
        }

        private void PreviousMessage(object sender, ExecutedRoutedEventArgs e)
        {
            MessageHeader currentMessage = DataContext as MessageHeader;
            MessageHeader nextMessage = folder_.Prev(Owner as MainWindow, currentMessage);

            DataContext = nextMessage;

            if (nextMessage != null)
            {
                UpdateContent();
            }
        }

        private void DeleteMessage(object sender, ExecutedRoutedEventArgs e)
        {
            MessageHeader currentMessage = DataContext as MessageHeader;
            NextMessage(sender, null);
            currentMessage.Deleted = true;

            if (DataContext == null)
            {
                // Didn't have a next message.

                Close();
            }
        }
    }
}
