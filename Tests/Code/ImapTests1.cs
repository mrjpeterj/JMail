using System;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using JMail.Core;

namespace JMail
{
    [TestClass]
    public class ImapTests1
    {
        Imap server_;

        [TestInitialize]
        public void Init()
        {
            var account = new AccountInfo();
            account.Name = "Mail_Test";

            server_ = new Imap(account);
        }

        [TestMethod]
        public void Folder_List_1()
        {
            var s = new ScriptPlayer("../../../Data/test1.xml");
            bool complete = false;

            server_.MessagesChanged += (sender, e) => { complete = true; };

            server_.SetStream(s);

            var folder = new Folder(server_, "Junk", "Junk", "/", false, true);

            server_.SelectFolder(folder);

            while (!complete)
            {
                System.Threading.Thread.Sleep(1000);
            }

            Assert.AreEqual(27, folder.Messages.Count);
        }

        [TestMethod]
        public void Folder_List_2()
        {
            var s = new ScriptPlayer("../../../Data/test2.xml");
            bool complete = false;

            server_.MessagesChanged += (sender, e) => { complete = true; };

            server_.SetStream(s);

            var folder = new Folder(server_, "INBOX", "INBOX", "/", false, true);

            server_.SelectFolder(folder);

            while (!complete)
            {
                System.Threading.Thread.Sleep(1000);
            }

            Assert.AreEqual(50, folder.Messages.Count);
            Assert.AreEqual(0, folder.Messages.Where(m => m.UnRead == true).Count());
        }

        [TestMethod]
        public void Body_8Bit_UTF8()
        {
            var s = new ScriptPlayer("../../../Data/test3.xml");
            bool complete = false;

            server_.MessagesChanged += (sender, e) => { complete = true; };

            server_.SetStream(s);

            var folder = new Folder(server_, "INBOX.testContent", "testContent", ".", false, true);

            server_.SelectFolder(folder);

            while (!complete)
            {
                System.Threading.Thread.Sleep(1000);
            }

            bool contentReady = false;
            var msg = folder.MessageByUID(86);
            msg.Body.Updated += (sender, e) => { contentReady = true; };

            msg.Fetch();

            while (!contentReady)
            {
                System.Threading.Thread.Sleep(1000);
            }

            var bodyText = msg.Body.Text;

            Assert.IsTrue(bodyText.Contains("£12.50"));
        }
    }
}
