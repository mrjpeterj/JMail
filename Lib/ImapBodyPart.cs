using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text;

namespace JMail.Core
{
    internal class ImapBodyPart : BodyPart
    {
        public ImapBodyPart(MessageHeader owner, string[] partDesc)
            : base(owner)
        {
            string content = ImapData.StripQuotes(partDesc[0], true).ToLower() + "/" + ImapData.StripQuotes(partDesc[1], true).ToLower();
            ContentType = new ContentType(content);
            Id = ImapData.StripQuotes(partDesc[3], true);

            int extensionStart = 7;
            if (content.StartsWith("text"))
            {
                extensionStart += 1;
            }
            else if (content.StartsWith("message"))
            {
                extensionStart += 3;
            }

            string[] partParams = ImapData.SplitToken(partDesc[2]);
            if (partParams != null)
            {
                // Final item can't be a match since there must always be a following one
                // with the value in it.
                for (int i = 0; i < partParams.Length - 1; ++i)
                {
                    if (partParams[i].Equals("\"CHARSET\"", StringComparison.OrdinalIgnoreCase))
                    {
                        ContentType.CharSet = ImapData.StripQuotes(partParams[i + 1], true).ToUpper();
                    }
                    else if (partParams[i].Equals("\"NAME\"", StringComparison.OrdinalIgnoreCase))
                    {
                        ContentType.Name = ImapData.StripQuotes(partParams[i + 1], true);
                    }
                }
            }

            Disposition.Size = Int32.Parse(partDesc[6]);

            string encoding = ImapData.StripQuotes(partDesc[5], true);
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
                    if (dispData[0].Equals("\"ATTACHMENT\"", StringComparison.OrdinalIgnoreCase))
                    {
                        Disposition.Inline = false;
                    }

                    string[] dispParams = ImapData.SplitToken(dispData[1]);
                    if (dispParams != null)
                    {
                        // Don't look at the final item as that can't be valid.
                        // Since there always needs to be another entry after 'FILENAME'
                        for (int i = 0; i < dispParams.Length - 1; ++i)
                        {
                            if (dispParams[i].Equals("\"FILENAME\"", StringComparison.OrdinalIgnoreCase))
                            {
                                Disposition.FileName = ImapData.StripQuotes(dispParams[i + 1], true);
                            }
                            else if (dispParams[i].Equals("\"SIZE\"", StringComparison.OrdinalIgnoreCase))
                            {
                                Disposition.Size = Int32.Parse(ImapData.StripQuotes(dispParams[i + 1], true));
                            }
                            else if (dispParams[i].Equals("\"CREATION-DATE\"", StringComparison.OrdinalIgnoreCase))
                            {
                                Disposition.CreationDate = ImapData.ParseDate(ImapData.StripQuotes(dispParams[i + 1], true));
                            }
                            else if (dispParams[i].Equals("\"MODIFICATION-DATE\"", StringComparison.OrdinalIgnoreCase))
                            {
                                Disposition.ModificationDate = ImapData.ParseDate(ImapData.StripQuotes(dispParams[i + 1], true));
                            }
                        }
                    }
                }
            }
        }
    }
}
