using System;
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

        private byte[] incoming_;

        private ImapState state_;
        private int cmdId_ = 0;
        private Dictionary<string, ImapRequest> pendingCommands_;
        private List<string> currentCommand_;
        private bool lastTokenIsComplete_;

        private ThreadedList<Folder> allFolders_;
        private ThreadedList<Folder> folders_;

        private Folder currentFolder_;
        
        private MessageStore messages_;

        public Imap(AccountInfo account)
        {
            account_ = account;
            state_ = ImapState.None;
            pendingCommands_ = new Dictionary<string, ImapRequest>();

            currentCommand_ = new List<string>();
            lastTokenIsComplete_ = true;

            allFolders_ = new ThreadedList<Folder>();
            folders_ = new ThreadedList<Folder>();
            messages_ = new MessageStore();

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
                string response = Encoding.UTF8.GetString(incoming_, 0, bytesRead);

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
                string lastToken = currentCommand_.Last();
                currentCommand_.RemoveAt(currentCommand_.Count - 1);

                lastTokenIsComplete_ = true;

                responseText = lastToken + responseText;
            }

            List<string> responses = new List<string>();
            bool lastIsComplete = SplitTokens(responseText, responses);

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

            byte[] bytes = Encoding.UTF8.GetBytes(cmd);

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

                string nameSpace = StripQuotes(responseLine[3]);
                string folderName = StripQuotes(responseLine[4]);
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

            messages_.Clear();
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
                        string[] responseInfo = SplitToken(response);

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
                var infoData = SplitToken(info);

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

                if (!messages_.Contains(id))
                {
                    messages_.Add(new MessageHeader(id, currentFolder_));

                    if (idList.Length > 0)
                    {
                        idList += ",";
                    }
                    idList += id;
                }

                if (i % 50 == 49)
                {
                    // Batch into 50's

                    SendCommand("FETCH", idList + " (FLAGS INTERNALDATE UID RFC822.SIZE ENVELOPE)", ProcessMessage);

                    idList = "";
                }
            }

            if (idList.Length > 0)
            {
                SendCommand("FETCH", idList + " (FLAGS INTERNALDATE UID RFC822.SIZE ENVELOPE)", ProcessMessage);
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
                    msg = messages_.Message(id);

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

        string StripQuotes(string data)
        {
            if (data.StartsWith("\"") && data.EndsWith("\""))
            {
                return data.Substring(1, data.Length - 2);
            }
            else
            {
                return data;
            }
        }

        string[] SplitToken(string token)
        {
            if (token == "NIL")
            {
                return null;
            }
            else
            {
                List<string> tokens = new List<string>();
                SplitTokens(token.Substring(1, token.Length - 2), tokens);

                return tokens.ToArray();
            }
        }

        bool SplitTokens(string data, IList<string> tokens)
        {
            bool lastIsComplete = false;
            int token = 0;
            while (true)
            {
                string tokenStr = NextToken(data, token, out token);

                if (token == data.Length)
                {
                    lastIsComplete = true;
                }
                
                if (tokenStr.Trim().Length != 0)
                {
                    tokens.Add(tokenStr);
                }

                if (token < 0)
                {
                    break;
                }
            }

            return lastIsComplete;
        }

        string NextToken(string data, int start, out int end)
        {
            if (start < 0)
            {
                end = -1;
                return null;
            }

            StringBuilder output = new StringBuilder();
            int pos = start;
            int byteCounterStart = 0; ;

            Stack<char> toMatch = new Stack<char>();
            toMatch.Push(' ');

            while (pos != data.Length)
            {
                char current = data[pos];

                char matchFor = toMatch.Peek();
                bool foundMatch = false;

                switch (matchFor)
                {
                    case '[':
                        foundMatch = (current == ']');
                        break;

                    case '(':
                        foundMatch = (current == ')');
                        break;

                    case '{':
                        foundMatch = (current == '}');
                        if (foundMatch)
                        {
                            int bytesLenLen = pos - byteCounterStart;

                            char[] destination = new char[bytesLenLen];
                            output.CopyTo(byteCounterStart - start, destination, 0, bytesLenLen);
                            output.Remove(byteCounterStart - start - 1, bytesLenLen + 1);

                            int bytesLen = Int32.Parse(new string(destination));

                            string dataStr = data.Substring(pos + 3, bytesLen);
                            output.Append('\"');
                            output.Append(dataStr);
                            output.Append('\"');

                            pos += bytesLen + 2;

                            current = '\0';
                        }
                        break;

                    case '<':
                        foundMatch = (current == '>');
                        break;

                    case '\"':
                        foundMatch = (current == '\"');
                        break;

                    case ' ':
                        foundMatch = (current == ' ' || current == '\r' || current == '\n');
                        break;

                    default:
                        foundMatch = false;
                        break;
                }

                if (foundMatch)
                {
                    char lastChar = toMatch.Pop();
                    if (toMatch.Count == 0)
                    {
                        end = pos + 1;

                        int matchedLen = pos - start;
                        if (lastChar != ' ')
                        {
                            // If the closing token wasn't <space> then we want to include it.
                            ++matchedLen;
                        }
                        return output.ToString();
                    }
                }
                else if (current == '[' ||
                         current == '(' ||
                         current == '<' ||
                         current == '\"')
                {
                    toMatch.Push(current);
                }
                else if (current == '{')
                {
                    toMatch.Push(current);
                    byteCounterStart = pos + 1;
                }

                ++pos;
                if (current != '\0')
                {
                    output.Append(current);
                }
            }

            end = -1;
            return output.ToString();
        }

        void ExtractValues(MessageHeader msg, string data)
        {
            string[] values = SplitToken(data);

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
            }
        }

        void ParseEnvelope(MessageHeader msg, string envData)
        {
            string[] envItems = SplitToken(envData);

            // Basic string fields
            string dataStr = StripQuotes(envItems[0]);
            string subject = StripQuotes(envItems[1]);
            string inReplyTo = StripQuotes(envItems[8]);
            string msgId = StripQuotes(envItems[9]);

            msg.SetValue("Date", dataStr);
            msg.SetValue("Subject", Decode(subject));
            msg.SetValue("In-Reply-To", inReplyTo);
            msg.SetValue("Message-Id", msgId);

            string[] from = SplitToken(envItems[2]);
            string[] sender = SplitToken(envItems[3]);
            string[] replyTo = SplitToken(envItems[4]);
            string[] to = SplitToken(envItems[5]);
            string[] cc = SplitToken(envItems[6]);
            string[] bcc = SplitToken(envItems[7]);

            msg.SetValue("From", AddressBuilder(SplitToken(from[0])));
        }

        void ExtractSingle(MessageHeader msg, string value, string key)
        {
            msg.SetValue(key, value);
        }

        void ExtractFlags(MessageHeader msg, string flagString)
        {
            // Clip off surrounding ( )
            flagString = flagString.Substring(1, flagString.Length - 2);

            // Process flags here
            msg.ClearFlags();

            if (flagString != "")
            {
                string[] flags = flagString.Split(' ');
                foreach (var flag in flags)
                {
                    if (flag[0] == '\\')
                    {
                        // Standard flag
                        msg.SetFlag(flag.Substring(1));
                    }
                }
            }
        }

        void ExtractDate(MessageHeader msg, string dateString)
        {
            msg.SetValue("Received", dateString.Substring(1, dateString.Length - 2));
        }

        void ExtractBodyInfo(MessageHeader msg, string data)
        {
            msg.Body = Decode(data);
        }

        string Decode(string input)
        {
            while (true)
            {
                int encodingStart = input.IndexOf("=?");
                if (encodingStart < 0) 
                {
                    break;
                }

                int encodingEnd = input.IndexOf("?=", encodingStart);
                if (encodingEnd < 0)
                {
                    break;
                }
                else
                {
                    // Move it to point after the close of the encoding tag
                    encodingEnd += 2;                    
                }

                string encoded = input.Substring(encodingStart, encodingEnd - encodingStart);
                

                string[] pieces = encoded.Split(new char[] { '?' });
                string charset = pieces[1];
                string encoding = pieces[2];
                
                string rest = string.Join("?", pieces.ToList().GetRange(3, pieces.Length - 4));
                byte[] data;

                if (encoding == "B")
                {
                    data = Convert.FromBase64String(rest);
                }
                else
                {
                    data = QuottedPrintableDecode(rest);
                }

                string res = Encoding.GetEncoding(charset).GetString(data);

                input = input.Substring(0, encodingStart) + res + input.Substring(encodingEnd, input.Length - encodingEnd);
            }

            return input;
        }

        byte[] QuottedPrintableDecode(string input)
        {
            List<byte> res = new List<byte>();
            bool encoding = false;
            char[] encodeVal = new char[2];
            int encodingPos = 0;

            foreach (var c in input)
            {
                if (encoding)
                {
                    encodeVal[encodingPos] = c;
                    ++encodingPos;

                    if (encodingPos == 2)
                    {
                        int charVal = Convert.ToInt32(new string(encodeVal), 16);
                        res.Add((byte)charVal);

                        encoding = false;
                    }
                }
                else
                {
                    if (c == '=')
                    {
                        encoding = true;
                        encodingPos = 0;
                    }
                    else if (c == '_')
                    {
                        res.Add(32);
                    }
                    else
                    {
                        res.Add((byte)c);
                    }
                }
            }

            return res.ToArray();
        }

        string AddressBuilder(string[] addressParts)
        {
            string address = "";
            if (addressParts[0] != "NIL")
            {
                address = addressParts[0];
            }
            else
            {
                address = addressParts[2] + "@" + addressParts[3];
            }

            return address.Replace("\"", ""); 
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

        public IEnumerable<MessageHeader> MessageList
        {
            get { return messages_; }
        }

        public void SelectFolder(Folder f)
        {
            currentFolder_ = f;
            SendCommand("SELECT", "\"" + f.FullName + "\"", SelectedFolder);
        }

        public void FetchMessage(MessageHeader m)
        {
            SendCommand("FETCH", m.id + " (FLAGS BODY.PEEK[1])", ProcessMessage);
        }


        #endregion
    }
}
