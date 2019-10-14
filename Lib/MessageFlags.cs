using System;
using System.Collections.Generic;
using System.Text;

namespace JMail.Core
{
    [Flags]
    public enum MessageFlags
    {
        None = 0,
        Seen = 1,
        Answered = 2,
        Flagged = 4,
        Deleted = 8,
        Draft = 16,
        Recent = 32
    }
}
