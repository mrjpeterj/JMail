using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using JMail.Core;

namespace JMail
{
    [TestClass]
    public class FolderViewTests
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
        public void ViewHasChildrenAndMessages()
        {
            var f = new Folder(server_, "INBOX", "INBOX", ".", true, true);

            var view = new FolderView(f);

            Assert.IsNotNull(view);
        }

        [TestMethod]
        public void ViewHasMessages()
        {
            var f = new Folder(server_, "INBOX", "INBOX", ".", false, true);

            var view = new FolderView(f);

            Assert.IsNotNull(view);
        }

        [TestMethod]
        public void ViewHasChildren()
        {
            var f = new Folder(server_, "INBOX", "INBOX", ".", true, false);

            var view = new FolderView(f);

            Assert.IsNotNull(view);
        }
    }
}
