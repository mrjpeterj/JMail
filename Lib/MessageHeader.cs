using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Mail;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;

namespace JMail.Core
{
    public class MessageHeader
    {
        Folder folder_;
    
        BehaviorSubject<MessageFlags> flags_;

        List<BodyPart> attachments_;
        List<BodyPart> related_;

        MailAddress from_;
        DateTime sent_;

        public Folder Folder { get { return folder_; } }
        public BodyPart Body { get; internal set; }
        public BodyPart FullMessage { get; private set; }
        public IEnumerable<BodyPart> Attachments { get { return attachments_; } }
        public IEnumerable<BodyPart> Related { get { return related_; } }

        public MailAddress From
        {
            get
            {
                return from_;
            }
            internal set
            {
                from_ = value;
            }
        }
        public MailAddress ReplyTo { get; internal set; }
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
        public int id { get; internal set; }
        public string MessageId { get; private set; }
        public int Size { get; private set; }

        public IObservable<bool> UnRead { get; private set; }

        public IObservable<bool> Deleted { get; private set; }

        public IObservable<bool> Flagged { get; private set; }

        public bool IsUnRead { get; private set; }
        public bool IsDeleted { get; private set; }

        public bool TrustedSender { get; private set; }

        public MessageHeader(int uid, Folder f)
        {
            if (uid < 0)
            {
                throw new System.ArgumentException("Invalid UID Value", "uid");
            }

            folder_ = f;
            Uid = uid;
            flags_ = new BehaviorSubject<MessageFlags>(MessageFlags.None);

            UnRead = flags_.Select((flags) => flags.HasFlag(MessageFlags.Seen) == false);
            Deleted = flags_.Select((flags) => flags.HasFlag(MessageFlags.Deleted));
            Flagged = flags_.Select((flags) => flags.HasFlag(MessageFlags.Flagged));

            UnRead.Subscribe(val => IsUnRead = val);
            Deleted.Subscribe(val => IsDeleted = val);

            attachments_ = new List<BodyPart>();
            related_ = new List<BodyPart>();

            To = new MailAddressCollection();
            Cc = new MailAddressCollection();

            // Build the BodyPart required to pull the whole message.
            FullMessage = new BodyPart(this, "text/plain");
            FullMessage.PartNumber = "";
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
                Sent = ImapData.ParseDate(value);
                field = "sent";
            }
            else if (field.Equals("received", StringComparison.CurrentCultureIgnoreCase))
            {
                Date = ImapData.ParseDate(value);
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
            flags_.OnNext(MessageFlags.None);
        }

        void SetFlag(MessageFlags flag, bool isSet)
        {
            if (isSet && flags_.Value.HasFlag(flag) == false)
            {
                // Add Flag
                flags_.OnNext(flags_.Value | flag);
            }
            else if (!isSet && flags_.Value.HasFlag(flag) == true)
            {
                // Remove Flag
                flags_.OnNext(flags_.Value & ~flag);
            }
        }

        public void SetFlag(string flag)
        {
            if (flag.Equals(MessageFlags.Answered.ToString(), StringComparison.CurrentCultureIgnoreCase))
            {
                SetFlag(MessageFlags.Answered, true);
            }
            else if (flag.Equals(MessageFlags.Deleted.ToString(), StringComparison.CurrentCultureIgnoreCase))
            {
                SetFlag(MessageFlags.Deleted, true);
            }
            else if (flag.Equals(MessageFlags.Draft.ToString(), StringComparison.CurrentCultureIgnoreCase))
            {
                SetFlag(MessageFlags.Draft, true);
            }
            else if (flag.Equals(MessageFlags.Flagged.ToString(), StringComparison.CurrentCultureIgnoreCase))
            {
                SetFlag(MessageFlags.Flagged, true);
            }
            else if (flag.Equals(MessageFlags.Recent.ToString(), StringComparison.CurrentCultureIgnoreCase))
            {
                SetFlag(MessageFlags.Recent, true);
            }
            else if (flag.Equals(MessageFlags.Seen.ToString(), StringComparison.CurrentCultureIgnoreCase))
            {
                SetFlag(MessageFlags.Seen, true);
            }
        }

        public void AddAttachment(BodyPart b)
        {
            if (Body == b)
            {
                return;
            }

            if (b.ContentType.MediaType == "application/pkcs7-signature")
            {
                b.Updated += TestPKCS7Signature;
                folder_.Server.FetchMessage(this, b);
            }

            attachments_.Add(b);
        }

        private void TestPKCS7Signature(object sender, EventArgs e)
        {
            BodyPart b = sender as BodyPart;

            // Now look at the contents of the body as a signature
            System.Security.Cryptography.Pkcs.SignedCms cms = new System.Security.Cryptography.Pkcs.SignedCms();

            cms.Decode(b.Data);

            foreach (var sig in cms.SignerInfos)
            {
                if (sig.Certificate.Subject.Contains(From.Address))
                {
                    TrustedSender = true;

                    break;
                }
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
    }
}
