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
            int msgCount = -1;
            int unRead = -1;

            server_.SetStream(s);

            var folder = new Folder(server_, "Junk", "Junk", "/", false, true);

            folder.Messages.Subscribe((msgs) =>
            {
                msgCount = msgs.Count();
                unRead = msgs.Where(m => m.UnRead == true).Count();
                complete = msgCount > 0;
            });

            server_.SelectFolder(folder);

            while (!complete)
            {
                System.Threading.Thread.Sleep(1000);
            }

            Assert.AreEqual(27, msgCount);
        }

        [TestMethod]
        public void Folder_List_2()
        {
            var s = new ScriptPlayer("../../../Data/test2.xml");

            bool complete = false;
            int msgCount = -1;
            int unRead = -1;

            server_.SetStream(s);

            var folder = new Folder(server_, "INBOX", "INBOX", "/", false, true);

            folder.Messages.Subscribe((msgs) =>
            {
                msgCount = msgs.Count();
                unRead = msgs.Where(m => m.UnRead == true).Count();
                complete = msgCount > 0;
            });

            server_.SelectFolder(folder);

            while (!complete)
            {
                System.Threading.Thread.Sleep(1000);
            }

            Assert.AreEqual(50, msgCount);
            Assert.AreEqual(0, unRead);
        }

        [TestMethod]
        public void Body_8Bit_UTF8()
        {
            var s = new ScriptPlayer("../../../Data/test3.xml");

            server_.SetStream(s);

            var folder = new Folder(server_, "INBOX.testContent", "testContent", ".", false, true);

            server_.SelectFolder(folder);

            bool contentReady = false;
            string bodyText = "";

            folder.Messages.Subscribe((msgs) =>
            {
                var msg = folder.MessageByUID(86);

                if (msg != null)
                {
                    msg.Body.Updated += (sender, e) => {
                        bodyText = msg.Body.Text;

                        contentReady = true;
                    };

                    msg.Fetch();
                }
            });

            while (!contentReady)
            {
                System.Threading.Thread.Sleep(1000);
            }

            Assert.IsTrue(bodyText.Contains("£12.50"));
        }
    }
}
