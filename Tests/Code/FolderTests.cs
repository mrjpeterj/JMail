using System;
using System.Linq;
using System.Reactive.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using JMail.Core;

namespace JMail
{
    [TestClass]
    public class FolderTests
    {
        TestAccount testServer_;
        IAccount server_;

        [TestInitialize]
        public void Init()
        {
            testServer_ = new TestAccount();

            server_ = testServer_;
            server_.Connect();
        }

        [TestCleanup]
        public void Shutdown()
        {
            server_.Shutdown();
            server_ = null;
        }

        [TestMethod]
        public void FolderHasChildrenAndMessages()
        {
            var f = new Folder(server_, "INBOX", "INBOX", ".", true, true);

            Assert.IsNotNull(f);
        }

        [TestMethod]
        public void FolderHasChildren()
        {
            var f = new Folder(server_, "INBOX", "INBOX", ".", true, false);

            Assert.IsNotNull(f);
        }

        [TestMethod]
        public void FolderHasMessages()
        {
            var f = new Folder(server_, "INBOX", "INBOX", ".", false, true);

            Assert.IsNotNull(f);
        }

        [TestMethod]
        public void MessagesArrive()
        {
            int folderMsgCount = -1;
            int folderUnreadCount = -1;

            int msgCount = -1;

            // Wait for the inbox to appear
            var findInbox = server_.AllFolders.Select((folders) =>
            {
                return folders.Where(folder => folder.FullName == "INBOX").FirstOrDefault();
            })
                .Where(folder => folder != null)
                .Take(1);

            // Now subscribe to interesting parts of the inbox
            findInbox.Subscribe(folder =>
            {
                server_.SelectFolder(folder);

                folder.Exists.Subscribe(val => folderMsgCount = val);
                folder.Unseen.Subscribe(val => folderUnreadCount = val);

                folder.Messages.Subscribe(msgs =>
                {
                    msgCount = msgs.Count();
                });
            });

            // Now start feeding the server with fake data.

            // Get the Inbox to appear
            var inbox = new Folder(server_, "INBOX", "INBOX", ".", false, true);
            testServer_.AddFolder(inbox);

            // Add a message to it.
            var msg1 = new MessageHeader(1, inbox);
            inbox.AddMessage(msg1);

            Assert.AreEqual(1, folderMsgCount);
            Assert.AreEqual(1, folderUnreadCount);
            Assert.AreEqual(1, msgCount);

            // Add another message to it.
            var msg2 = new MessageHeader(2, inbox);
            inbox.AddMessage(msg2);

            Assert.AreEqual(2, folderMsgCount);
            Assert.AreEqual(2, folderUnreadCount);
            Assert.AreEqual(2, msgCount);

            // Mark the first message as read
            server_.SetFlag(msg1, MessageFlags.Seen, true);

            Assert.AreEqual(2, folderMsgCount);
            Assert.AreEqual(1, folderUnreadCount);
            Assert.AreEqual(2, msgCount);

            // Delete the 2nd message
            server_.SetFlag(msg2, MessageFlags.Deleted, true);

            Assert.AreEqual(2, folderMsgCount);
            Assert.AreEqual(1, folderUnreadCount);
            Assert.AreEqual(2, msgCount);

            // Expunge the folder
            server_.ExpungeFolder();

            Assert.AreEqual(1, folderMsgCount);
            Assert.AreEqual(0, folderUnreadCount);
            Assert.AreEqual(1, msgCount);

            // Mark the first as unread again
            server_.SetFlag(msg1, MessageFlags.Seen, false);

            Assert.AreEqual(1, folderMsgCount);
            Assert.AreEqual(1, folderUnreadCount);
            Assert.AreEqual(1, msgCount);
        }

        [TestMethod]
        public void MessagesFiltered()
        {
            int visibleMessages = -1;

            // Wait for the inbox to appear
            var findInbox = server_.AllFolders.Select((folders) =>
            {
                return folders.Where(folder => folder.FullName == "INBOX").FirstOrDefault();
            })
                .Where(folder => folder != null)
                .Take(1)
                .Subscribe(folder =>
                {
                    // Now subscribe to interesting parts of the inbox

                    server_.SelectFolder(folder);

                    folder.ViewMessages.Select(msgs =>
                        {
                            return msgs.Count();
                        }).Subscribe(msgCount => {
                            visibleMessages = msgCount;
                        });
                });

            // Now start feeding the server with fake data.

            // Get the Inbox to appear
            var inbox = new Folder(server_, "INBOX", "INBOX", ".", false, true);
            testServer_.AddFolder(inbox);

            // Add a message to it.
            var msg1 = new MessageHeader(1, inbox);
            msg1.SetValue("subject", "Subject A");

            inbox.AddMessage(msg1);

            // Add another message to it.
            var msg2 = new MessageHeader(2, inbox);
            msg2.SetValue("subject", "Subject B");

            inbox.AddMessage(msg2);

            System.Threading.Thread.Sleep(1500);
            Assert.AreEqual(2, visibleMessages);


            // Now filter the messages
            inbox.SetFilterMsgIds(new int[] { 2 });

            System.Threading.Thread.Sleep(1500);
            Assert.AreEqual(1, visibleMessages);


            inbox.SetFilterMsgIds(new int[] { 1 });

            System.Threading.Thread.Sleep(1500);
            Assert.AreEqual(1, visibleMessages);

            inbox.SetFilterMsgIds(new int[] { 1, 2 });

            System.Threading.Thread.Sleep(1500);
            Assert.AreEqual(2, visibleMessages);

            inbox.SetFilterMsgIds(new int[] { });

            System.Threading.Thread.Sleep(1500);
            Assert.AreEqual(0, visibleMessages);

            inbox.SetFilterMsgIds(null);

            System.Threading.Thread.Sleep(1500);
            Assert.AreEqual(2, visibleMessages);
        }
    }
}
