using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.ComponentModel;
using System.Net.Mime;

namespace Mail
{
    public class BodyPart: INotifyPropertyChanged
    {
        private string text_;
        private byte[] data_;

        public string Text
        {
            get { return text_; }
            set
            {
                text_ = value;
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("Text"));
                }
            }
        }
        public byte[] Data { get; set; }

        public ContentType ContentType { get; protected set; }
        public ContentDisposition Disposition { get; protected set; }
        public TextEncoding Encoding { get; protected set; }
        public string PartNumber { get; set; }
        public long Size
        {
            get
            {
                return Disposition.Size;
            }
        }

        public BodyPart(string contentType)
        {
            ContentType = new ContentType(contentType);
            Disposition = new ContentDisposition();
            Disposition.Inline = true;
        }

        public BodyPart()
        {
            ContentType = new ContentType();
            Disposition = new ContentDisposition();
            Disposition.Inline = true;
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }

    internal class ImapBodyPart: BodyPart
    {
        public ImapBodyPart(string[] partDesc)
        {
            string content = ImapData.StripQuotes(partDesc[0]).ToLower() + "/" + ImapData.StripQuotes(partDesc[1]).ToLower();
            ContentType = new ContentType(content);

            int extensionStart = 7;
            if (content.StartsWith("TEXT"))
            {
                extensionStart += 1;
            }
            else if (content.StartsWith("MESSAGE"))
            {
                extensionStart += 3;
            }

            string[] partParams = ImapData.SplitToken(partDesc[2]);
            if (partParams != null)
            {
                List<string> paramList = partParams.ToList();

                int charsPos = paramList.IndexOf("\"CHARSET\"");
                if (charsPos >= 0)
                {
                    ContentType.CharSet = ImapData.StripQuotes(partParams[charsPos + 1]);
                }

                int namePos = paramList.IndexOf("\"NAME\"");
                if (namePos >= 0)
                {
                    ContentType.Name = ImapData.StripQuotes(partParams[namePos + 1]);
                }
            }

            Disposition.Size = Int32.Parse(partDesc[6]);

            string encoding = ImapData.StripQuotes(partDesc[5]);
            if (encoding.Length > 0)
            {
                Encoding = EncodedText.BuildEncoding(encoding);
            }

            if (extensionStart + 1 < partDesc.Length)
            {
                string dispose = partDesc[extensionStart + 1];

                string[] dispData = ImapData.SplitToken(dispose);
                if (dispData != null)
                {
                    if (dispData[0] == "\"ATTACHMENT\"")
                    {
                        Disposition.Inline = false;
                    }

                    string[] dispParams = ImapData.SplitToken(dispData[1]);
                    if (dispParams != null)
                    {
                        List<string> dispParamList = dispParams.ToList();
                        int filePos = dispParamList.IndexOf("\"FILENAME\"");
                        if (filePos >= 0)
                        {
                            Disposition.FileName = ImapData.StripQuotes(dispParams[filePos + 1]);
                        }
                    }
                }
            }
        }
    }
}
