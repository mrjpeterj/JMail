using System;
using System.Linq;

using JMail.Core;

using Microsoft.Reactive.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JMail
{
    [TestClass]
    public class ImapTests1
    {
        TestScheduler scheduler_;
        Imap server_;

        [TestInitialize]
        public void Init()
        {
            scheduler_ = new TestScheduler();
            Core.Dependencies.TimeScheduler = scheduler_;

            var account = new AccountInfo();
            account.Name = "Mail_Test";

            server_ = new Imap(account);
        }

        [TestMethod]
        public void Folder_List_1()
        {
            var s = new ScriptPlayer("../../../Data/test1.xml");
            int msgCount = -1;
            int folderExists = -1;
            int unRead = -1;

            server_.SetStream(s);

            var folder = new Folder(server_, "Junk", "Junk", "/", false, true);

            folder.Messages.Subscribe((msgs) =>
            {
                msgCount = msgs.Count();
            });
            folder.Exists.Subscribe((val) =>
            {
                folderExists = val;
            });
            folder.Unseen.Subscribe((val) =>
            {
                unRead = val;
            });

            server_.SelectFolder(folder);

            System.Threading.Thread.Sleep(1000);

            scheduler_.AdvanceBy(TimeSpan.FromSeconds(10).Ticks);

            Assert.AreEqual(27, msgCount);
            Assert.AreEqual(msgCount, folderExists);
            Assert.AreEqual(27, unRead);
        }

        [TestMethod]
        public void Folder_List_2()
        {
            var s = new ScriptPlayer("../../../Data/test2.xml");

            int msgCount = -1;
            int folderExists = -1;
            int unRead = -1;

            server_.SetStream(s);

            var folder = new Folder(server_, "INBOX", "INBOX", "/", false, true);

            folder.Messages.Subscribe((msgs) =>
            {
                msgCount = msgs.Count();
            });
            folder.Exists.Subscribe((val) =>
            {
                folderExists = val;
            });
            folder.Unseen.Subscribe((val) =>
            {
                unRead = val;
            });

            server_.SelectFolder(folder);

            System.Threading.Thread.Sleep(1000);

            scheduler_.AdvanceBy(TimeSpan.FromSeconds(10).Ticks);

            Assert.AreEqual(50, msgCount);
            Assert.AreEqual(msgCount, folderExists);
            Assert.AreEqual(0, unRead);
        }

        [TestMethod]
        public void Body_8Bit_UTF8()
        {
            var s = new ScriptPlayer("../../../Data/test3.xml");

            server_.SetStream(s);

            var folder = new Folder(server_, "INBOX.testContent", "testContent", ".", false, true);

            server_.SelectFolder(folder);

            string bodyText = "";

            folder.Messages.Subscribe((msgs) =>
            {
                var msg = folder.MessageByUID(86);

                if (msg != null)
                {
                    msg.Body.Updated += (sender, e) => {
                        bodyText = msg.Body.Text;
                    };

                    msg.Body.Fetch();
                }
            });

            scheduler_.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);

            System.Threading.Thread.Sleep(1000);

            scheduler_.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);

            System.Threading.Thread.Sleep(1000);

            Assert.IsTrue(bodyText.Contains("£12.50"));
        }
    }
}
