using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace JMail
{
    public class MessageHeaderView: IChangingProperty, IDisposable
    {
        private Core.MessageHeader message_;

        private List<IDisposable> subscriptions_;

        public Core.Folder Folder
        {
            get
            {
                return message_.Folder;
            }
        }

        public Core.BodyPart Body
        {
            get
            {
                return message_.Body;
            }
        }

        public Core.BodyPart FullMessage
        {
            get
            {
                return message_.FullMessage;
            }
        }

        public IEnumerable<Core.BodyPart> Attachments
        {
            get
            {
                return message_.Attachments;
            }
        }

        public IEnumerable<Core.BodyPart> Related
        {
            get
            {
                return message_.Related;
            }
        }

        public bool HasAttachments { get { return Attachments.Any(); } }

        public bool Deleted { get; set; }
        public bool Flagged { get; set; }
        public bool UnRead { get; set; }

        public MailAddressCollection To { get { return message_.To; } }

        public MailAddress From { get { return message_.From; } }
        public bool TrustedSender { get { return message_.TrustedSender; } }

        public string Subject { get { return message_.Subject; } }

        public DateTime Date { get { return message_.Date; } }
        public DateTime Sent { get { return message_.Sent; } }

        public int Size { get { return message_.Size; } }

        public MessageHeaderView(Core.MessageHeader msg)
        {
            message_ = msg;

            subscriptions_ = new List<IDisposable>();

            subscriptions_.Add(msg.Deleted.SubscribeTo(this, x => x.Deleted));
            subscriptions_.Add(msg.Flagged.SubscribeTo(this, x => x.Flagged));
            subscriptions_.Add(msg.UnRead.SubscribeTo(this, x => x.UnRead));
        }

        #region IChangingProperty compliance
        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    foreach (var disposable in subscriptions_)
                    {
                        disposable.Dispose();
                    }
                }

                disposedValue = true;
            }
        }

        ~MessageHeaderView()
        {
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
            
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
