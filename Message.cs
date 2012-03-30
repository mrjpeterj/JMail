﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.ComponentModel;

namespace Mail
{
    public class MessageStore: ThreadedList<MessageHeader>
    {
        public bool Contains(int id)
        {
            var matches = from m in this
                          where m.id == id
                          select m;

            return matches.Any();
        }

        public MessageHeader Message(int id)
        {
            var matches = from m in this
                          where m.id == id
                          select m;

            return matches.FirstOrDefault();
        }
    }

    public enum MessageFlags
    {
        Seen,
        Answered,
        Flagged,
        Deleted,
        Draft,
        Recent
    }

    public class MessageHeader: INotifyPropertyChanged
    {
        int id_;
        List<MessageFlags> flags_;

        public int id { get { return id_; } }

        public string From { get; private set; }
        public string Subject { get; private set; }
        public string Date { get; private set; }

        public bool UnRead
        {
            get
            {
                return !flags_.Contains(MessageFlags.Seen);
            }
        }

        public bool Deleted
        {
            get
            {
                return flags_.Contains(MessageFlags.Deleted);
            }
        }

        public MessageHeader(int id)
        {
            id_ = id;
            flags_ = new List<MessageFlags>();
        }

        public void SetValue(string field, string value)
        {
            if (field.Equals("subject", StringComparison.CurrentCultureIgnoreCase))
            {
                Subject = value;
            }
            else if (field.Equals("date", StringComparison.CurrentCultureIgnoreCase))
            {
                Date = value;
            }
            else if (field.Equals("from", StringComparison.CurrentCultureIgnoreCase))
            {
                From = value;
            }

            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(CamelCase(field)));
            }
        }

        public void ClearFlags()
        {
            flags_.Clear();
        }

        public void SetFlag(string flag)
        {
            if (flag.Equals(MessageFlags.Answered.ToString(), StringComparison.CurrentCultureIgnoreCase))
            {
                flags_.Add(MessageFlags.Answered);
            }
            else if (flag.Equals(MessageFlags.Deleted.ToString(), StringComparison.CurrentCultureIgnoreCase))
            {
                flags_.Add(MessageFlags.Deleted);
            }
            else if (flag.Equals(MessageFlags.Draft.ToString(), StringComparison.CurrentCultureIgnoreCase))
            {
                flags_.Add(MessageFlags.Draft);
            }
            else if (flag.Equals(MessageFlags.Flagged.ToString(), StringComparison.CurrentCultureIgnoreCase))
            {
                flags_.Add(MessageFlags.Flagged);
            }
            else if (flag.Equals(MessageFlags.Recent.ToString(), StringComparison.CurrentCultureIgnoreCase))
            {
                flags_.Add(MessageFlags.Recent);
            }
            else if (flag.Equals(MessageFlags.Seen.ToString(), StringComparison.CurrentCultureIgnoreCase))
            {
                flags_.Add(MessageFlags.Seen);
            }
        }

        string CamelCase(string label)
        {
            string lower = label.Substring(1).ToLower();
            string first = label[0].ToString().ToUpper();

            return first + lower;
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
