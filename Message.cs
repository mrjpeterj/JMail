using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.ComponentModel;
using System.Net.Mail;

namespace Mail
{
    public class MessageStore: ThreadedList<MessageHeader>
    {
        public bool ContainsUID(int id)
        {
            var matches = from m in this
                          where m.id == id
                          select m;

            return matches.Any();
        }

        public MessageHeader MessageByID(int id)
        {
            var matches = from m in this
                          where m.id == id
                          select m;

            return matches.FirstOrDefault();
        }

        public MessageHeader MessageByUID(int id)
        {
            var matches = from m in this
                          where m.Uid == id
                          select m;

            return matches.FirstOrDefault();
        }

        public MessageHeader Next(MessageHeader msg)
        {
            int idx = this.IndexOf(msg);
            if (idx >= 0 && idx < Count - 1)
            {
                return this[idx + 1];
            }
            else
            {
                return null;
            }
        }

        public MessageHeader Prev(MessageHeader msg)
        {
            int idx = this.IndexOf(msg);
            if (idx >= 1 && idx < Count)
            {
                return this[idx - 1];
            }
            else
            {
                return null;
            }
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
        List<BodyPart> attachments_;
        List<BodyPart> related_;

        MailAddress from_;
        DateTime sent_;

        public int id { get { return id_; } }
        public Folder Folder { get { return folder_; } }

        public BodyPart Body { get; set; }
        public IEnumerable<BodyPart> Attachments { get { return attachments_; } }
        public IEnumerable<BodyPart> Related { get { return related_; } }

        public MailAddress From
        {
            get
            {
                return from_;
            }
            set
            {
                from_ = value;
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("From"));
                }
            }
        }
        public MailAddress ReplyTo { get; set; }
        public MailAddressCollection To { get; private set; }
        public MailAddressCollection Cc { get; private set; }

        public string Subject { get; private set; }
        public DateTime Sent
        {
            get
            {
                if (sent_.Ticks == 0)
                {
                    return Date;
                }
                else
                {
                    return sent_;
                }
            }
            private set
            {
                sent_ = value;
            }
        }
        public DateTime Date { get; private set; }
        public int Uid { get; private set; }
        public string MessageId { get; private set; }
        public int Size { get; private set; }
        public bool HasAttachments
        {
            get
            {
                return attachments_.Any();
            }
        }

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
            attachments_ = new List<BodyPart>();
            related_ = new List<BodyPart>();

            To = new MailAddressCollection();
            Cc = new MailAddressCollection();
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
            else if (field.Equals("uid", StringComparison.CurrentCultureIgnoreCase))
            {
                Uid = Int32.Parse(value);
            }
            else if (field.Equals("size", StringComparison.CurrentCultureIgnoreCase))
            {
                Size = Int32.Parse(value);
            }
            else if (field.Equals("message-id", StringComparison.CurrentCultureIgnoreCase))
            {
                MessageId = value;
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

            DateTime invalid = new DateTime();

            return invalid;
        }

        public void AddAttachment(BodyPart b)
        {
            if (Body == b)
            {
                return;
            }

            attachments_.Add(b);
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs("HasAttachments"));
            }
        }

        public void AddRelated(BodyPart b)
        {
            if (Body == b)
            {
                return;
            }

            related_.Add(b);
        }

        public void Fetch()
        {
            if (Body.Text == null)
            {
                folder_.Server.FetchMessage(this, Body);
            }
        }

        public MessageHeader Next()
        {
            var nextMsg = Folder.FindNext(this);
            if (nextMsg != null)
            {
                nextMsg.Fetch();
            }

            return nextMsg;
        }

        public MessageHeader Prev()
        {
            var nextMsg = Folder.FindPrev(this);
            if (nextMsg != null)
            {
                nextMsg.Fetch();
            }

            return nextMsg;
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }

    public class AddressDisplay : System.Windows.Data.IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            MailAddress addr = value as MailAddress;
            if (addr == null)
            {
                return null;
            }

            if (targetType != typeof(string))
            {
                return null;
            }

            if (addr.DisplayName.Length > 0)
            {
                return addr.DisplayName;
            }
            else
            {
                return addr.Address;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
