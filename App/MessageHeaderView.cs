using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace JMail
{
    public class MessageHeaderView: INotifyPropertyChanged
    {
        public MessageHeader Message { get; private set; }
        
        public MessageHeaderView(MessageHeader msg)
        {
            Message = msg;
        }

        public void Dirty()
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs("Message"));
            }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
