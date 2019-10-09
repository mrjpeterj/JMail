using System;
using System.Collections.Generic;
using System.Text;

namespace JMail.Core
{
    public class MessagesChangedEventArgs : EventArgs
    {
        public Folder Folder { get; private set; }
        public IEnumerable<MessageHeader> Messages { get; private set; }

        public MessagesChangedEventArgs(Folder f, IEnumerable<MessageHeader> msgs)
        {
            Folder = f;
            Messages = msgs;
        }
    }
}
