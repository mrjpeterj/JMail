using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using JMail.Core;

namespace JMail
{
    [TestClass]
    public class ImapDataTests
    {
        [TestMethod]
        public void TestMethod1()
        {
            string input = "* 10 FETCH (FLAGS () INTERNALDATE \"30-Nov-2017 16:14:34 +0000\" RFC822.SIZE 61631 ENVELOPE (\"Thu, 30 Nov 2017 15:41:49 +0000\" \"You have a new message\" ((\"donotreply@email01.aaaaaaaaaa.co.uk\" NIL \"donotreply\" \"email01.aaaaaaaaaa.co.uk\")) NIL ((\"AAAAAA BBBB\" NIL \"info\" \"email01.aaaaaaaaaa.co.uk\")) ((NIL NIL \"mmmmm\" \"hotmail.com\")) NIL NIL NIL \"<oooooooooooooooooooooo@email01.aaaaaaaaaa.co.uk\\\" <donotreply@email01.aaaaaaaaaa.co.uk>\") BODYSTRUCTURE(\"text\" \"html\" (\"charset\" \"UTF-8\") NIL NIL \"quoted-printable\" 21833 574 NIL NIL NIL NIL) UID 1083)";
            var data = Encoding.UTF8.GetBytes(input);
            List<byte[]> tokens = new List<byte[]>();

            ImapData.SplitTokens(data, tokens);

            Assert.AreEqual(4, tokens.Count);

            var subTokens = ImapData.SplitToken(tokens[3]);

            Assert.AreEqual(11, subTokens.Count);

            var envItems = ImapData.SplitToken(Encoding.UTF8.GetString(subTokens[7]));

            Assert.AreEqual(10, envItems.Length);

            string dateStr = ImapData.StripQuotes(envItems[0], true);
            string subject = ImapData.StripQuotes(envItems[1], false);
            string[] from = ImapData.SplitToken(envItems[2]);
            string[] sender = ImapData.SplitToken(envItems[3]);
            string[] replyTo = ImapData.SplitToken(envItems[4]);
            string[] to = ImapData.SplitToken(envItems[5]);
            string[] cc = ImapData.SplitToken(envItems[6]);
            string[] bcc = ImapData.SplitToken(envItems[7]);
            string inReplyTo = ImapData.StripQuotes(envItems[8], false);
            string msgId = ImapData.StripQuotes(envItems[9], false);

            var date = ImapData.ParseDate(dateStr);
            Assert.AreEqual(11, date.Month);
            Assert.AreEqual(2017, date.Year);
            Assert.AreEqual(30, date.Day);

            Assert.AreEqual("You have a new message", subject);
            Assert.AreEqual(1, from.Length);
            Assert.IsNull(sender);
            Assert.AreEqual(1, replyTo.Length);
            Assert.AreEqual(1, to.Length);
            Assert.IsNull(cc);
            Assert.IsNull(bcc);
            Assert.AreEqual(0, inReplyTo.Length);
            Assert.AreEqual("<oooooooooooooooooooooo@email01.aaaaaaaaaa.co.uk\" <donotreply@email01.aaaaaaaaaa.co.uk>", msgId);
        }
    }
}
