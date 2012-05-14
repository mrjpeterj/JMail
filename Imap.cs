﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;

using System.Net;
using System.Net.Sockets;

namespace Mail
{
    public enum ImapState
    {
        None,
        Connected,
        LoggedIn,
        Selected
    }

    internal class ImapRequest
    {
        internal delegate void ResponseHandler(ImapRequest request, IList<string> data);

        string id_;
        string commandName_;
        string args_;
        ResponseHandler response_;

        public string Key { get { return id_; } }
        public string Command { get { return commandName_; } }
        public string Args { get { return args_; } }

        public ImapRequest(string id, string commandName, string args, ResponseHandler handler)
        {
            id_ = id;
            commandName_ = commandName;
            args_ = args;
            response_ = handler;
        }

        public void Process(IList<string> resultData)
        {
            response_(this, resultData);
        }
    }

    public class Imap: IAccount
    {
        private AccountInfo account_;
        private TcpClient client_;
        private Stream stream_;

        private System.Text.Encoding encoder_ = System.Text.Encoding.ASCII;
        private byte[] incoming_;

        private ImapState state_;
        private int cmdId_ = 0;
        private Dictionary<string, ImapRequest> pendingCommands_;
        private List<string> currentCommand_;
        private bool lastTokenIsComplete_;

        private ThreadedList<Folder> allFolders_;
        private ThreadedList<Folder> folders_;

        private Folder currentFolder_;       

        public Imap(AccountInfo account)
        {
            account_ = account;
            state_ = ImapState.None;
            pendingCommands_ = new Dictionary<string, ImapRequest>();

            currentCommand_ = new List<string>();
            lastTokenIsComplete_ = true;

            allFolders_ = new ThreadedList<Folder>();
            folders_ = new ThreadedList<Folder>();

            client_ = new TcpClient(account.Host, account.Port);            

            if (client_.Connected)
            {
                state_ = ImapState.Connected;
                stream_ = client_.GetStream();

                incoming_ = new byte[8 * 1024];

                if (account.Encrypt)
                {
                    var sslStream = new System.Net.Security.SslStream(client_.GetStream(), false,
                        new System.Net.Security.RemoteCertificateValidationCallback(GotRemoteCert));
                    sslStream.AuthenticateAsClient(account_.Host);

                    stream_ = sslStream;
                }

                stream_.BeginRead(incoming_, 0, incoming_.Length, HandleRead, null);
            }
        }

        void HandleRead(IAsyncResult res)
        {
            int bytesRead = stream_.EndRead(res);

            if (bytesRead > 0)
            {
                string response = encoder_.GetString(incoming_, 0, bytesRead);

                ProcessResponse(response);
            }

            stream_.BeginRead(incoming_, 0, incoming_.Length, HandleRead, null);
        }

        void ProcessResponse(string responseText)
        {
            System.Diagnostics.Debug.WriteLine(">>>>>>>>");
            System.Diagnostics.Debug.Write(responseText);
            System.Diagnostics.Debug.WriteLine("<<<<<<<<");

            if (!lastTokenIsComplete_)
            {
                string lastToken = "";

                if (currentCommand_.Any())
                {
                    lastToken = currentCommand_.Last();
                    currentCommand_.RemoveAt(currentCommand_.Count - 1);
                }

                lastTokenIsComplete_ = true;

                responseText = lastToken + responseText;
            }

            List<string> responses = new List<string>();
            bool lastIsComplete = ImapData.SplitTokens(responseText, responses);

            ImapRequest request = null;
            string result = null;

            for (int i = 0; i < responses.Count; ++i)
            {
                string response = responses[i];

                if (request != null)
                {
                    if (result == null)
                    {
                        result = response;

                        bool success = false;
                        if (result == "OK")
                        {
                            success = true;
                        }
                        else if (result == "NO")
                        {
                        }
                        else if (result == "BAD")
                        {
                        }

                        request.Process(currentCommand_);

                        currentCommand_.Clear();
                        pendingCommands_.Remove(request.Key);

                        continue;
                    }
                    else
                    {
                        // Suck up the rest of the input until we see a '*' to start a new command
                        if (response == "*")
                        {
                            request = null;
                            result = null;
                        }
                        else
                        {
                            continue;
                        }
                    }
                }
                
                // match it to the request command of this name.
                pendingCommands_.TryGetValue(response, out request);

                if (request == null)
                {
                    if (state_ == ImapState.Connected && response == "OK")
                    {
                        StartUp();
                        currentCommand_.Clear();
                        return;
                    }                    
                    else if (currentCommand_.Any() || response == "*")
                    {
                        currentCommand_.Add(response);
                    }
                }
            }

            if (!lastIsComplete)
            {
                // Remember whether the last token in the split was a complete one,
                // so that we know whether to append to it in the next round.


                lastTokenIsComplete_ = lastIsComplete;
            }
        }

        string NextCommand()
        {
            lock (this)
            {
                ++cmdId_;

                return string.Format("__XX__X_{0:D4}", cmdId_);
            }
        }

        void SendCommand(string command, string args, ImapRequest.ResponseHandler handler)
        {
            string commandId = NextCommand();
            string cmd = commandId + " " + command;
            if (args != "")
            {
                cmd += " " + args;
            }
            
            //System.Diagnostics.Debug.WriteLine("++++++++");
            //System.Diagnostics.Debug.WriteLine(cmd);
            //System.Diagnostics.Debug.WriteLine("++++++++");            

            cmd += "\r\n";

            pendingCommands_[commandId] = new ImapRequest(commandId, command, args, handler);

            byte[] bytes = encoder_.GetBytes(cmd);

            stream_.Write(bytes, 0, bytes.Length);
            stream_.Flush();
        }

        void StartUp()
        {
            Caps();
        }

        void Caps()
        {
            SendCommand("CAPABILITY", "", HandleCaps);
        }

        void HandleCaps(ImapRequest request, IEnumerable<string> resultData)
        {
            // Looks like 
            // * CAPABILITY <cap1> <cap2> <cap3>
            if (resultData.Contains("STARTTLS"))
            {
                StartTLS();
            }
            else
            {
                Login();
            }
        }

        void StartTLS()
        {
            SendCommand("STARTTLS", "", HandleTLS);
        }

        void HandleTLS(ImapRequest request, IEnumerable<string> responseData)
        {
            var sslStream = new System.Net.Security.SslStream(client_.GetStream(), false,
                new System.Net.Security.RemoteCertificateValidationCallback(GotRemoteCert));
            sslStream.AuthenticateAsClient(account_.Host);

            stream_ = sslStream;

            Login();
        }

        bool GotRemoteCert(object sender, System.Security.Cryptography.X509Certificates.X509Certificate cert,
            System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors errors)
        {
            return true;
        }

        void Login()
        {
            SendCommand("LOGIN", account_.Username + " " + account_.GetPassword(), HandleLogin);
        }

        void HandleLogin(ImapRequest request, IEnumerable<string> resultData)
        {
            state_ = ImapState.LoggedIn;
            ListFolders();
        }

        void ListFolders()
        {
            folders_.Clear();
            SendCommand("LSUB", "\"\" \"*\"", ListedFolder);
        }

        void ListedFolder(ImapRequest request, IEnumerable<string> responseData)
        {
            Folder currentParent = null;

            // each line looks like:
            // * LSUB (<flags>) "<namespace>" "<folder name>"
            // so we need to pull the data off in groups of 5.

            List<string> responseLine = new List<string>();

            foreach (var response in responseData)
            {
                responseLine.Add(response);

                if (responseLine.Count < 5)
                {
                    continue;
                }

                string flags = responseLine[2].Trim();
                flags = flags.Substring(1, flags.Length - 2);
                bool hasChildren = false;
                bool realFolder = true;

                if (flags.Contains("\\HasChildren"))
                {
                    hasChildren = true;
                }
                if (flags.Contains("\\Noselect"))
                {
                    realFolder = false;
                }

                string nameSpace = ImapData.StripQuotes(responseLine[3]);
                string folderName = ImapData.StripQuotes(responseLine[4]);
                string folderShortName = folderName;

                if (currentParent != null)
                {
                    if (!folderName.Contains(currentParent.FullName))
                    {
                        // No longer a parent of current parent, look for a new one.
                        currentParent = (from f in allFolders_
                                         where f.Children != null && f.Children.Contains(currentParent)
                                         select f).FirstOrDefault();
                    }
                }

                if (currentParent != null)
                {
                    folderShortName = folderName.Replace(currentParent.FullName, "").Substring(1);
                }

                Folder folder = new Folder(this, folderName, folderShortName, hasChildren, realFolder);

                if (currentParent != null)
                {
                    currentParent.Children.Add(folder);
                }
                else
                {
                    folders_.Add(folder);
                }

                allFolders_.Add(folder);

                if (hasChildren)
                {
                    currentParent = folder;
                }

                if (realFolder)
                {
                    CheckUnseen(folder);
                }


                responseLine.Clear();
            }
        }

        void SelectedFolder(ImapRequest request, IEnumerable<string> responseData)
        {
            ListMessages(request, responseData);

            SendCommand("SEARCH", "UNDELETED", AvailableMessages);       
        }

        void ListMessages(ImapRequest request, IEnumerable<string> responseData)
        {
            string folderName = request.Args.Substring(1, request.Args.Length - 2);

            state_ = ImapState.Selected;

            Folder folder = (from f in AllFolders
                             where f.FullName == folderName
                             select f).FirstOrDefault();

            if (folder != null)
            {
                string lastValue = null;
                bool subProcessNext = false;

                foreach (var response in responseData)
                {
                    if (subProcessNext)
                    {
                        string[] responseInfo = ImapData.SplitToken(response);

                        subProcessNext = false;
                    }


                    if (response == "EXISTS")
                    {
                        folder.Exists = Int32.Parse(lastValue);
                    }
                    else if (response == "RECENT")
                    {
                        folder.Recent = Int32.Parse(lastValue);
                    }
                    else if (response == "OK")
                    {
                        subProcessNext = true;
                    }

                    lastValue = response;
                }
            }
        }

        void CheckUnseen(Folder f)
        {
            SendCommand("STATUS", "\"" + f.FullName + "\"" + " (MESSAGES UNSEEN RECENT)", UnreadCount);
        }

        void UnreadCount(ImapRequest request, IList<string> responseData)
        {
            string folderName = responseData[2];

            Folder folder = (from f in AllFolders
                             where f.FullName == folderName
                             select f).FirstOrDefault();

            if (folder != null)
            {
                string info = responseData[3];
                var infoData = ImapData.SplitToken(info);

                for (int i = 0; i < infoData.Length; i = i + 2) 
                {
                    string key = infoData[i];
                    string valueStr = infoData[i + 1];

                    try
                    {
                        int value = Int32.Parse(valueStr);

                        if (key == "UNSEEN")
                        {
                            folder.Unseen = value;
                        }
                        else if (key == "MESSAGES")
                        {
                            folder.Exists = value;
                        }
                        else if (key == "RECENT")
                        {
                            folder.Recent = value;
                        }
                    }
                    catch (FormatException) { }
                }
            }            
        }


        void AvailableMessages(ImapRequest request, IEnumerable<string> responseData)
        {
            // Each line is of the form:
            // * SEARCH <list of ids>

            List<int> msgIds = new List<int>();

            foreach (var msg in responseData)
            {
                int msgId = -1;
                if (Int32.TryParse(msg, out msgId))
                {
                    msgIds.Add(msgId);
                }
            }

            if (msgIds.Count > 0)
            {
                FetchMessage(msgIds);
            }
        }

        void FetchMessage(IList<int> ids)
        {
            string idList = "";

            for (int i = 0; i < ids.Count; ++i)
            {
                int id = ids[i];

                currentFolder_.Messages.Add(new MessageHeader(id, currentFolder_));

                if (idList.Length > 0)
                {
                    idList += ",";
                }
                idList += id;

                if (i % 50 == 49)
                {
                    // Batch into 50's

                    SendCommand("FETCH", idList + " (FLAGS INTERNALDATE UID RFC822.SIZE ENVELOPE BODYSTRUCTURE)", ProcessMessage);

                    idList = "";
                }
            }

            if (idList.Length > 0)
            {
                SendCommand("FETCH", idList + " (FLAGS INTERNALDATE UID RFC822.SIZE ENVELOPE BODYSTRUCTURE)", ProcessMessage);
            }
        }

        void ProcessMessage(ImapRequest request, IEnumerable<string> responseData)
        {
            // Format of this is:
            // * {id} FETCH (<field> <field data> <field> <field data> ......

            bool isId = false;
            bool isResponse = false;

            MessageHeader msg = null;

            foreach (var response in responseData)
            {
                if (response == "*")
                {
                    isId = true;
                }
                else if (response == "FETCH")
                {
                    isResponse = true;
                }
                else if (isId)
                {
                    int id = Int32.Parse(response);
                    msg = currentFolder_.Messages.Message(id);

                    isId = false;
                }
                else if (isResponse && msg != null)
                {
                    ExtractValues(msg, response);

                    isResponse = false;
                    msg = null;
                }
            }
        }

        void ExtractValues(MessageHeader msg, string data)
        {
            string[] values = ImapData.SplitToken(data);

            for (int i = 0; i < values.Length; i = i + 2)
            {
                string key = values[i];
                string value = values[i + 1];

                if (key == "FLAGS")
                {
                    ExtractFlags(msg, value);
                }
                else if (key.StartsWith("BODY["))
                {
                    ExtractBodyInfo(msg, value);
                }
                else if (key == "INTERNALDATE")
                {
                    ExtractDate(msg, value);
                }
                else if (key == "UID")
                {
                    ExtractSingle(msg, value, "UID");
                }
                else if (key == "RFC822.SIZE")
                {
                    ExtractSingle(msg, value, "SIZE");
                }
                else if (key == "ENVELOPE")
                {
                    ParseEnvelope(msg, value);
                }
                else if (key == "BODYSTRUCTURE")
                {
                    ParseBodyStructure(msg, value, "");
                }
            }
        }

        void ParseEnvelope(MessageHeader msg, string envData)
        {
            string[] envItems = ImapData.SplitToken(envData);

            // Basic string fields
            string dataStr = ImapData.StripQuotes(envItems[0]);
            string subject = ImapData.StripQuotes(envItems[1]);
            string inReplyTo = ImapData.StripQuotes(envItems[8]);
            string msgId = ImapData.StripQuotes(envItems[9]);

            msg.SetValue("Date", dataStr);
            msg.SetValue("Subject", EncodedText.Decode(subject));
            msg.SetValue("In-Reply-To", inReplyTo);
            msg.SetValue("Message-Id", msgId);

            string[] from = ImapData.SplitToken(envItems[2]);
            string[] sender = ImapData.SplitToken(envItems[3]);
            string[] replyTo = ImapData.SplitToken(envItems[4]);
            string[] to = ImapData.SplitToken(envItems[5]);
            string[] cc = ImapData.SplitToken(envItems[6]);
            string[] bcc = ImapData.SplitToken(envItems[7]);

            msg.From = AddressBuilder(ImapData.SplitToken(from[0]));
            msg.ReplyTo = AddressBuilder(ImapData.SplitToken(from[0]));

            if (to != null)
            {
                foreach (var addr in to)
                {
                    var address = AddressBuilder(ImapData.SplitToken(addr));

                    if (address != null)
                    {
                        msg.To.Add(address);
                    }
                }
            }

            if (cc != null)
            {
                foreach (var addr in cc)
                {
                    var address = AddressBuilder(ImapData.SplitToken(addr));

                    if (address != null)
                    {
                        msg.Cc.Add(address);
                    }
                }
            }
        }

        string AppendTextLocation(string loc, int idx)
        {
            if (loc.Length > 0)
            {
                loc += ".";
            }

            loc += idx;

            return loc;
        }

        ImapBodyPart ParseBodyStructure(MessageHeader msg, string structData, string loc)
        {
            string[] dataPieces = ImapData.SplitToken(structData);

            if (ImapData.IsArray(dataPieces[0]))
            {
                // Analyze multi-part type
                int typePos = dataPieces.Length - 4;
                string multiType = ImapData.StripQuotes(dataPieces[typePos]);
                string[] paramSet = ImapData.SplitToken(dataPieces[typePos + 1]);
                List<string> paramList = null;
                if (paramSet != null)
                {
                    paramList = paramSet.ToList();
                    for (int i = 0; i < paramList.Count; ++i)
                    {
                        paramList[i] = ImapData.StripQuotes(paramList[i]);
                    }
                }

                if (multiType == "MIXED")
                {
                    for (int i = 0; i < typePos; ++i)
                    {
                        string subLoc = AppendTextLocation(loc, i + 1);

                        var part = ParseBodyStructure(msg, dataPieces[i], subLoc);
                        if (part != null)
                        {
                            msg.AddAttachment(part);
                        }
                    }
                }
                else if (multiType == "ALTERNATIVE")
                {
                    for (int i = 0; i < typePos; ++i)
                    {
                        string subLoc = AppendTextLocation(loc, i + 1);

                        ParseBodyStructure(msg, dataPieces[i], subLoc);
                    }
                }
                else if (multiType == "RELATED")
                {
                    int relTypePos = paramList.IndexOf("TYPE");
                    string relatedType = paramList[relTypePos + 1];

                    int relStartPos = paramList.IndexOf("START");
                    int start = 1;
                    if (relStartPos >= 0)
                    {
                        string startStr = paramList[relStartPos + 1];
                        start = Int32.Parse(startStr);
                    }

                    string subLoc = AppendTextLocation(loc, start);
                    ParseBodyStructure(msg, dataPieces[start - 1], subLoc);
                }
                else
                {
                    string subLoc = AppendTextLocation(loc, 1);
                    ParseBodyStructure(msg, dataPieces[0], subLoc);
                }

                return null;
            }
            else
            {
                if (loc == "")
                {
                    loc += 1;
                }

                ImapBodyPart bodyPart = new ImapBodyPart(dataPieces);
                bodyPart.PartNumber = loc;

                if (ImapData.StripQuotes(dataPieces[0]) == "TEXT")
                {
                    string textType = ImapData.StripQuotes(dataPieces[1]);

                    if (textType == "PLAIN")
                    {
                        msg.Body = bodyPart;
                    }
                }

                return bodyPart;
            }
        }

        void ExtractSingle(MessageHeader msg, string value, string key)
        {
            msg.SetValue(key, value);
        }

        void ExtractFlags(MessageHeader msg, string flagString)
        {
            // Process flags here
            msg.ClearFlags();

            string[] flags = ImapData.SplitToken(flagString);

            foreach (var flag in flags)
            {
                if (flag[0] == '\\')
                {
                    // Standard flag
                    msg.SetFlag(flag.Substring(1));
                }
            }
        }

        void ExtractDate(MessageHeader msg, string dateString)
        {
            msg.SetValue("Received", dateString.Substring(1, dateString.Length - 2));
        }

        void ExtractBodyInfo(MessageHeader msg, string data)
        {
            var bytes = encoder_.GetBytes(data);

            msg.Body.SetContent(bytes);
        }

        System.Net.Mail.MailAddress AddressBuilder(string[] addressParts)
        {
            string address = ImapData.StripQuotes(addressParts[2]) + "@" + ImapData.StripQuotes(addressParts[3]);
            string displayName = ImapData.StripQuotes(addressParts[0]);

            try
            {
                if (displayName.Length > 0)
                {
                    return new System.Net.Mail.MailAddress(address, displayName);
                }
                else
                {
                    return new System.Net.Mail.MailAddress(address);
                }
            }
            catch (FormatException e)
            {
                return null;
            }
        }

        #region IAccount Members

        public IEnumerable<Folder> FolderList
        {
            get { return folders_; }
        }

        public IEnumerable<Folder> AllFolders
        {
            get { return allFolders_; }
        }

        public void SelectFolder(Folder f)
        {
            currentFolder_ = f;
            SendCommand("SELECT", "\"" + f.FullName + "\"", SelectedFolder);
        }

        public void FetchMessage(MessageHeader m, BodyPart body)
        {
            SendCommand("FETCH", m.id + " (FLAGS BODY.PEEK[" + body.PartNumber + "])", ProcessMessage);
        }


        #endregion
    }
}
