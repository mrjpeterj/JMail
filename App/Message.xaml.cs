using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using JMail.Core;

namespace JMail
{
    /// <summary>
    /// Interaction logic for Message.xaml
    /// </summary>
    public partial class Message: Window
    {
        MainWindow owner_; // Not used as the Window.Owner as we don't want them attached

        FolderView folder_;

        MessageHeaderView currentMessage_;
        
        public MessageHeaderView CurrentMessageView
        {
            get
            {
                return currentMessage_;
            }

            set
            {
                currentMessage_ = value;

                DataContext = currentMessage_;
            }
        }

        public MessageHeader CurrentMessage
        {
            get
            {
                return currentMessage_.Message;
            }
        }

        public new object DataContext
        {
            get { return base.DataContext; }
            protected set { base.DataContext = value; }
        }

        public Message(FolderView folder, MainWindow owner)
        {
            owner_ = owner;
            folder_ = folder;

            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            this.SetPlacement(Properties.Settings.Default.MsgWinPos);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CurrentMessage.Body.Updated += BodyUpdated;

            UpdateContent();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // This stops the command bindings still trying to do stuff
            DataContext = null;

            e.Cancel = false;

            Properties.Settings.Default.MsgWinPos = this.GetPlacement();
            Properties.Settings.Default.Save();            
        }

        private void BodyUpdated(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateContent();
                }));
        }

        private void UpdateContent()
        {
            if (CurrentMessage.Body.Text != null)
            {
                UIElement existingChild = content_.Child;

                if (CurrentMessage.Body.ContentType.MediaType.EndsWith("/plain"))
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

                    plainText.Text = CurrentMessage.Body.Text;

                    plainText.Focus();
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
                        htmlText.Document.Write(CurrentMessage.Body.Text);
                        htmlText_Ready(htmlText, null);
                    }
                    else
                    {
                        htmlText.DocumentText = CurrentMessage.Body.Text;
                    }
                }

                // Now mark the message as read
                CurrentMessage.UnRead = false;
            }
            else
            {
                // TODO: Check that this is correct.

                if (content_.Child != null)
                {
                    IDisposable disposableChild = content_.Child as IDisposable;

                    if (disposableChild != null)
                    {
                        disposableChild.Dispose();
                    }

                    content_.Child = null;
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

                    var imgPart = from r in CurrentMessage.Related
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
                    else if (CurrentMessage.HasAttachments)
                    {
                        // See if it was listed as an attachement
                        imgPart = from r in CurrentMessage.Attachments
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
        }

        private void SaveAttachment(object sender, RoutedEventArgs e)
        {
            FrameworkElement ele = (FrameworkElement)sender;

            BodyPart part = ele.DataContext as BodyPart;

            var dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = part.Disposition.FileName;

            if (dlg.ShowDialog() == true)
            {
                string location = dlg.FileName;
                part.Save(null, location);
            }
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
                e.CanExecute = !folder_.IsLast(owner_, CurrentMessageView);
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
                e.CanExecute = !folder_.IsFirst(owner_, CurrentMessageView);
            }
        }

        private void NextMessage(object sender, ExecutedRoutedEventArgs e)
        {
            if (CurrentMessageView != null)
            {
                CurrentMessage.Body.Updated -= BodyUpdated;
            }

            CurrentMessageView = folder_.Next(owner_, CurrentMessageView);

            if (CurrentMessageView != null)
            {
                UpdateContent();

                CurrentMessage.Body.Updated += BodyUpdated;
            }
        }

        private void PreviousMessage(object sender, ExecutedRoutedEventArgs e)
        {
            if (CurrentMessageView != null)
            {
                CurrentMessage.Body.Updated -= BodyUpdated;
            }

            CurrentMessageView = folder_.Prev(owner_, CurrentMessageView);

            if (CurrentMessageView != null)
            {
                UpdateContent();

                CurrentMessage.Body.Updated += BodyUpdated;
            }
        }

        private void DeleteMessage(object sender, ExecutedRoutedEventArgs e)
        {
            if (CurrentMessageView != null)
            {
                CurrentMessage.Deleted = true;
            }

            NextMessage(sender, null);

            if (CurrentMessageView == null)
            {
                // Didn't have a next message.

                Close();
            }
        }
    }
}
