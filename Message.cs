using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.ComponentModel;

#if !NETFX_CORE
using System.Net.Mail;
#endif

namespace JMail
{
    public enum MessageFlags
    {
        Seen,
        Answered,
        Flagged,
        Deleted,
        Draft,
        Recent
    }

    public class MessageHeader
    {
        Folder folder_;
    
        List<MessageFlags> flags_;
        List<BodyPart> attachments_;
        List<BodyPart> related_;

        MailAddress from_;
        DateTime sent_;

        public Folder Folder { get { return folder_; } }
        public BodyPart Body { get; set; }
        public BodyPart FullMessage { get; set; }
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
        public int id { get; set; }
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
            set
            {
                bool current = !flags_.Contains(MessageFlags.Seen);

                if (value != current)
                {
                    Folder.Server.SetFlag(this, MessageFlags.Seen, !value);
                }
            }
        }

        public bool Deleted
        {
            get
            {
                return flags_.Contains(MessageFlags.Deleted);
            }
            set
            {
                bool current = flags_.Contains(MessageFlags.Deleted);

                if (value != current)
                {
                    Folder.Server.SetFlag(this, MessageFlags.Deleted, value);
                }
            }
        }

        public bool Flagged
        {
            get
            {
                return flags_.Contains(MessageFlags.Flagged);
            }
            set
            {
                bool current = flags_.Contains(MessageFlags.Flagged);

                if (value != current)
                {
                    Folder.Server.SetFlag(this, MessageFlags.Flagged, value);
                }
            }
        }

        public MessageHeader(int uid, Folder f)
        {
            if (uid < 0)
            {
                throw new System.ArgumentException("Invalid UID Value", "uid");
            }

            folder_ = f;
            Uid = uid;
            flags_ = new List<MessageFlags>();
            attachments_ = new List<BodyPart>();
            related_ = new List<BodyPart>();

            To = new MailAddressCollection();
            Cc = new MailAddressCollection();
        }

        public override string ToString()
        {
            return Subject;
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
        }

        public void ClearFlags()
        {
            flags_.Clear();
        }

        void SetFlag(MessageFlags flag, bool isSet)
        {
            if (isSet && !flags_.Contains(flag))
            {
                flags_.Add(flag);
            }
            else if (!isSet)
            {
                flags_.Remove(flag);
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

        public void FetchWhole()
        {
            if (FullMessage == null)
            {
                // Build the BodyPart required to pull the whole message.
                FullMessage = new BodyPart(this, "text/plain");
                FullMessage.PartNumber = "";

                folder_.Server.FetchMessage(this, FullMessage);
            }
        }
    }
}
