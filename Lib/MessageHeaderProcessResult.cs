using System;
using System.Collections.Generic;
using System.Text;

namespace JMail.Core
{
    internal class MessageHeaderProcessResult
    {
        public MessageHeader Message { get; set; }
        public bool IsNew { get; set; }
        public bool IsModified { get; set; }

        public MessageHeaderProcessResult()
        {
            IsNew = false;
            IsModified = false;
        }
    }
}
