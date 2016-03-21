using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.ComponentModel;
using System.Net.Mime;

namespace JMail
{
    public class BodyPart
    {
        public event EventHandler Updated;

        private MessageHeader owner_;
        private string saveLocation_;
        private Action<BodyPart> saveCompletion_;

        private string text_;
        private byte[] data_;

        public string Text { get { return text_; } }
        public byte[] Data { get { return data_; } }

        public string CacheFile { get; private set; }

        public ContentType ContentType { get; protected set; }
        public ContentDisposition Disposition { get; protected set; }
        public TextEncoding Encoding { get; protected set; }
        public string PartNumber { get; set; }
        public string Id { get; protected set; }
        public long Size
        {
            get
            {
                return Disposition.Size;
            }
        }

        public BodyPart(MessageHeader owner, string contentType)
            : this(owner)
        {
            ContentType = new ContentType(contentType);
        }

        public BodyPart(MessageHeader owner)
        {
            ContentType = new ContentType();
            Disposition = new ContentDisposition();
            Disposition.Inline = true;

            saveLocation_ = null;
            owner_ = owner;
        }

        public void SetContent(byte[] content)
        {
            byte[] bytes = null;

            if (Encoding != TextEncoding.Binary)
            {
                if (Encoding == TextEncoding.QuotedPrintable)
                {
                    bytes = EncodedText.QuottedPrintableDecode(content, false);
                }
                else if (Encoding == TextEncoding.Base64)
                {
                    string base64Data = System.Text.Encoding.ASCII.GetString(content);
                    bytes = Convert.FromBase64String(base64Data);
                }
                else
                {
                    bytes = content;
                }
            } else {
                bytes = content;
            }

            SetContentInternal(bytes);
        }

        void SetContentInternal(byte[] bytes)
        {
            if (ContentType.MediaType.StartsWith("text/"))
            {
                System.Text.Encoding encoder = System.Text.Encoding.ASCII;
                if (ContentType.CharSet != null)
                {
                    encoder = System.Text.Encoding.GetEncoding(ContentType.CharSet);
                }

                string text = encoder.GetString(bytes);

                text_ = ImapData.StripQuotes(text);
            }
            else
            {
                data_ = bytes;
            }

            if (Updated != null)
            {
                Updated(this, null);
            }

            if (saveLocation_ != null)
            {
                SaveFile(saveCompletion_);
                saveCompletion_ = null;
            }
        }

        public void Save(Action<BodyPart> saveComplete, string location = "")
        {
            if (location.Length == 0)
            {
                string filename = Disposition.FileName;
                if (filename == null)
                {
                    filename = ContentType.Name;
                }
                if (filename == null)
                {
                    filename = Id;

                    // Id can have random non-file characters in it.
                    // For example it can be an email address.
                    foreach (var badChar in System.IO.Path.GetInvalidFileNameChars())
                    {
                        filename = filename.Replace(badChar, '_');
                    }
                }
                if (filename.Length == 0)
                {
                    filename = "base";
                }

                if (filename == System.IO.Path.GetFileNameWithoutExtension(filename))
                {
                    filename += "." + ContentType.MediaType.Substring(ContentType.MediaType.IndexOf('/') + 1);
                }

                location = System.IO.Path.Combine(new string[] {
                    System.IO.Path.GetTempPath(),
                    "JMail",
                    "Cache",
                    MessageIdToDirName(owner_.MessageId),
                    filename
                });
            }

            saveLocation_ = location;

            if (System.IO.File.Exists(saveLocation_))
            {
                var info = new System.IO.FileInfo(saveLocation_);
                if (info.Length > 0)
                {
                    // File already here, don't try and save it.
                    CacheFile = saveLocation_;
                    saveLocation_ = null;

                    // Now Load it if required.
                    // We might be here because the file already exists on disk from a previous run
                    if (Data == null || Data.Length != info.Length)
                    {
                        SetContentInternal(System.IO.File.ReadAllBytes(CacheFile));
                    }
                }
            }

            if (Text == null && Data == null)
            {
                saveCompletion_ = saveComplete;

                owner_.Folder.Server.FetchMessage(owner_, this);
                
                // This will return into SetContent and then if saveLocation_ != null, SaveFile gets called to complete
                // the task.
            }
            else
            {
                SaveFile(saveComplete);
            }
        }

        string MessageIdToDirName(string messageId)
        {
            return messageId.Replace('<', '_').Replace('>', '_').Replace('@', '=');
        }

        void EnsureDir(string fileName)
        {
            string dirName = System.IO.Path.GetDirectoryName(fileName);
            if (!System.IO.Directory.Exists(dirName))
            {
                System.IO.Directory.CreateDirectory(dirName);
            }
        }

        void SaveFile(Action<BodyPart> saveComplete)
        {
            if (saveLocation_ != null)
            {
                EnsureDir(saveLocation_);
                System.IO.FileStream outFile = new System.IO.FileStream(saveLocation_, System.IO.FileMode.Create, System.IO.FileAccess.Write);

                if (Data != null)
                {
                    outFile.Write(Data, 0, Data.Length);
                }
                else if (Text != null)
                {
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(Text);
                    outFile.Write(bytes, 0, bytes.Length);
                }

                outFile.Close();

                CacheFile = saveLocation_;
                saveLocation_ = null;
            }

            if (saveComplete != null)
            {
                saveComplete(this);
            }
        }
    }

    internal class ImapBodyPart: BodyPart
    {
        public ImapBodyPart(MessageHeader owner, string[] partDesc)
            : base(owner)
        {
            string content = ImapData.StripQuotes(partDesc[0]).ToLower() + "/" + ImapData.StripQuotes(partDesc[1]).ToLower();
            ContentType = new ContentType(content);
            Id = ImapData.StripQuotes(partDesc[3]);

            int extensionStart = 7;
            if (content.StartsWith("text"))
            {
                extensionStart += 1;
            }
            else if (content.StartsWith("message"))
            {
                extensionStart += 3;
            }

            string[] partParams = ImapData.SplitToken(partDesc[2].ToUpper());
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
