using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using JMail.Core;

namespace JMail
{
    /// <summary>
    /// Interaction logic for MessageProps.xaml
    /// </summary>
    public partial class MessageProps: Window
    {
        MessageHeader msg_;

        public MessageProps(MessageHeader msg, Window owner)
        {
            msg_ = msg;
            Owner = owner;

            msg.FullMessage.Updated += BodyUpdated;

            InitializeComponent();

            u_body.Text = msg_.FullMessage.Text;
        }

        void BodyUpdated(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                u_body.Text = msg_.FullMessage.Text;
            }));
        }
    }
}
