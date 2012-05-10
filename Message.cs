using System;
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
        Folder folder_;
        int id_;
        List<MessageFlags> flags_;

        public int id { get { return id_; } }

        public BodyPart Body { get; set; }

        public string From { get; private set; }
        public string Subject { get; private set; }
        public DateTime Sent { get; private set; }
        public DateTime Date { get; private set; }
        public string Uid { get; private set; }
        public int Size { get; private set; }
        public int AttachementCount { get; set; }

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

        public MessageHeader(int id, Folder f)
        {
            folder_ = f;
            id_ = id;
            flags_ = new List<MessageFlags>();
        }

        public void SetValue(string field, string value)
        {
            if (value == "NIL")
            {
                value = "";
            }

            if (field.Equals("subject", StringComparison.CurrentCultureIgnoreCase))
            {
                Subject = value;
            }
            else if (field.Equals("date", StringComparison.CurrentCultureIgnoreCase))
            {
                Sent = ParseDate(value);
                field = "sent";
            }
            else if (field.Equals("received", StringComparison.CurrentCultureIgnoreCase))
            {
                Date = ParseDate(value);
                field = "date";
            }
            else if (field.Equals("from", StringComparison.CurrentCultureIgnoreCase))
            {
                From = value;
            }
            else if (field.Equals("uid", StringComparison.CurrentCultureIgnoreCase))
            {
                Uid = value;
            }
            else if (field.Equals("size", StringComparison.CurrentCultureIgnoreCase))
            {
                Size = Int32.Parse(value);
            }


            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(TextProcessing.CamelCase(field)));
            }
        }

        public void ClearFlags()
        {
            flags_.Clear();

            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs("UnRead"));
                PropertyChanged(this, new PropertyChangedEventArgs("Deleted"));
            }
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
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("Deleted"));
                }
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

                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("UnRead"));
                }
            }
        }

        DateTime ParseDate(string value)
        {
            try
            {
                return DateTime.Parse(value);
            }
            catch (Exception)
            {
            }

            if (value.EndsWith(")"))
            {
                // strip off this trailing tz annotation.
                int newEndPos = value.LastIndexOf("(");
                value = value.Substring(0, newEndPos);

                try
                {
                    return DateTime.Parse(value);
                }
                catch (Exception)
                {
                }
            }

            return DateTime.UtcNow;
        }

        public void Fetch()
        {
            if (Body.Text == null)
            {
                folder_.Server.FetchMessage(this, Body);
            }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
