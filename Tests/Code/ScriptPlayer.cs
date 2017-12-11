using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;
using System.Xml.Linq;

namespace JMail
{
    class ScriptAction
    {
        public byte[] request;

        public List<byte[]> response;

        public ScriptAction()
        {
            response = new List<byte[]>();
        }
    }

    class ScriptPlayer: System.IO.Stream
    {
        List<ScriptAction> actions_;

        List<byte[]> nextResponse_;
        int nextResponsePos_;

        public ScriptPlayer(string fileName)
        {
            actions_ = new List<ScriptAction>();

            var doc = XDocument.Load(fileName);

            var actions = doc.Element("test").Elements("action");
            foreach (var action in actions)
            {
                var actionItem = new ScriptAction();

                // XDocument does not preserve \r\n

                var requestEle = action.Element("request");
                actionItem.request = Encoding.UTF8.GetBytes(requestEle.Value.Replace("\n", "\r\n"));

                var responsesEle = action.Elements("response");
                foreach (var responseEle in responsesEle)
                {
                    actionItem.response.Add(Encoding.UTF8.GetBytes(responseEle.Value.Replace("\n", "\r\n")));
                }

                actions_.Add(actionItem);
            }
        }

        #region Stream implementation
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            while (nextResponse_ == null)
            {
                System.Threading.Thread.Sleep(100);
            }

            lock (this)
            {
                int realCount = count;
                var response = nextResponse_.First();

                if (realCount > response.Length - nextResponsePos_)
                {
                    realCount = response.Length - nextResponsePos_;
                }

                Array.Copy(response, nextResponsePos_, buffer, offset, realCount);

                nextResponsePos_ += realCount;

                // Response fully given out
                if (nextResponsePos_ == response.Length)
                {
                    nextResponse_.RemoveAt(0);
                    nextResponsePos_ = 0;

                    if (!nextResponse_.Any())
                    {
                        // No more response content

                        nextResponse_ = null;
                    }
                }

                return realCount;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            System.Diagnostics.Debug.Assert(offset == 0);
            System.Diagnostics.Debug.Assert(count == buffer.Length);

            var action = actions_.First();

            lock (this)
            {
                if (action.request.SequenceEqual(buffer))
                {
                    nextResponse_ = action.response;
                    nextResponsePos_ = 0;

                    actions_.RemoveAt(0);
                }
            }
        }
        #endregion
    }
}
