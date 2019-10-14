using System;
using System.Linq;
using System.Reactive.Linq;

using JMail.Core;

using Microsoft.VisualStudio.TestTools.UnitTesting;

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
        }
    }
}
