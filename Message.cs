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

    public class MessageHeader: INotifyPropertyChanged
    {
        int id_;

        public int id { get { return id_; } }

        public string From { get; private set; }
        public string Subject { get; private set; }
        public string Date { get; private set; }

        public MessageHeader(int id)
        {
            id_ = id;
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
