using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
        public void TestMethod1()
        {
            var s = new ScriptPlayer("../../../Data/test1.xml");
            bool complete = false;

            server_.MessagesChanged += (sender, e) => { complete = true; };

            server_.SetStream(s, ImapState.LoggedIn);

            var folder = new Folder(server_, "Junk", "Junk", "/", false, true);

            server_.SelectFolder(folder);

            while (!complete)
            {
                System.Threading.Thread.Sleep(1000);
            }

            Assert.AreEqual(27, folder.Messages.Count);
        }
    }
}
