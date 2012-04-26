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
        internal delegate void ResponseHandler(ImapRequest request, IEnumerable<string> data);

        string id_;
        string commandName_;
        string args_;
        ResponseHandler response_;

        public string Command { get { return commandName_; } }
        public string Args { get { return args_; } }

        public ImapRequest(string id, string commandName, string args, ResponseHandler handler)
        {
            id_ = id;
            commandName_ = commandName;
            args_ = args;
            response_ = handler;
        }

        public void Process(IEnumerable<string> resultData)
        {
            response_(this, resultData);
        }
    }

    public class Imap: IAccount
    {
        private AccountInfo account_;
        private TcpClient client_;
        private Stream stream_;

        private int readStart_;
        private byte[] incoming_;

        private ImapState state_;
        private int cmdId_ = 0;
        private Dictionary<string, ImapRequest> pendingCommands_;

        private ThreadedList<Folder> allFolders_;
        private ThreadedList<Folder> folders_;
        private MessageStore messages_;

        public Imap(AccountInfo account)
        {
            account_ = account;
            state_ = ImapState.None;
            pendingCommands_ = new Dictionary<string, ImapRequest>();

            allFolders_ = new ThreadedList<Folder>();
            folders_ = new ThreadedList<Folder>();
            messages_ = new MessageStore();

            client_ = new TcpClient(account.Host, account.Port);            

            if (client_.Connected)
            {
                state_ = ImapState.Connected;
                stream_ = client_.GetStream();

                incoming_ = new byte[8 * 1024];
                readStart_ = 0;

                if (account.Encrypt)
                {
                    var sslStream = new System.Net.Security.SslStream(client_.GetStream(), false,
                        new System.Net.Security.RemoteCertificateValidationCallback(GotRemoteCert));
                    sslStream.AuthenticateAsClient(account_.Host);

                    stream_ = sslStream;
                }

                stream_.BeginRead(incoming_, readStart_, incoming_.Length, HandleRead, null);
            }
        }

        void HandleRead(IAsyncResult res)
        {
            int bytesRead = stream_.EndRead(res) + readStart_;

            if (bytesRead > 0)
            {
                string response = Encoding.UTF8.GetString(incoming_, 0, bytesRead);

                ProcessResponse(response);
            }

            stream_.BeginRead(incoming_, readStart_, incoming_.Length - readStart_, HandleRead, null);
        }

        void ProcessResponse(string responseText)
        {
            //System.Diagnostics.Debug.Write(responseText);

            string[] responses = responseText.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            string lastResponse = responses.Last();

            List<string> commandResponse = new List<string>();

            foreach (var responseLine in responses)
            {
                if (responseLine == lastResponse && !responseText.EndsWith("\r\n"))
                {
                    // Incomplete response.
                    commandResponse.Add(responseLine);
                    break;
                }

                string[] responseData = responseLine.Split(new char[] { ' ' });

                // match it to the request command of this name.
                ImapRequest request = null;
                pendingCommands_.TryGetValue(responseData[0], out request);

                if (request == null)
                {
                    if (state_ == ImapState.Connected && responseData[1] == "OK")
                    {
                        StartUp();
                    }
                    else
                    {
                        commandResponse.Add(responseLine);
                    }

                    continue;
                }
                else
                {
                    string result = null;
                    string resultData = null;

                    int resultOffset = 2;
                    if (responseData[resultOffset] != request.Command)
                    {
                        --resultOffset;
                    }

                    result = responseData[resultOffset];
                    resultData = string.Join(" ", responseData, resultOffset + 1, responseData.Length - resultOffset - 1);

                    pendingCommands_.Remove(responseData[0]);

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

                    request.Process(commandResponse);

                    commandResponse.Clear();
                }
            }

            if (commandResponse.Any())
            {
                // We need to store this text up and append the next read to it.
                string leftOverResponse = string.Join("\r\n", commandResponse);

                readStart_ = Encoding.UTF8.GetBytes(leftOverResponse, 0, leftOverResponse.Length, incoming_, 0);
            }
            else
            {
                readStart_ = 0;
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
            string[] caps = string.Join(" ", resultData).Split(' ');

            if (caps.Contains("STARTTLS"))
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

            foreach (var response in responseData)
            {
                // each line looks like:
                // * LSUB (<flags>) "<namespace>" "<folder name>"

                string responseBody = response.Substring(response.IndexOf('(') - 1);
                string[] data = responseBody.Split(new char[] { '\"' }, StringSplitOptions.RemoveEmptyEntries);

                string flags = data[0].Trim();
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

                string nameSpace = data[1];
                string folderName = data[3];
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
                foreach (var responseLine in responseData)
                {
                    string[] responseSplit = responseLine.Split(new char[] { ' ' });
                    if (responseSplit[2] == "EXISTS")
                    {
                        folder.Exists = Int32.Parse(responseSplit[1]);
                    }
                    else if (responseSplit[2] == "RECENT")
                    {
                        folder.Recent = Int32.Parse(responseSplit[1]);
                    }
                    else if (responseSplit[1] == "OK")
                    {
                        string responseLineRest = string.Join(" ", responseSplit.ToList().GetRange(2, responseSplit.Length - 2));

                        int infoEnd = FindTokenEnd(responseLineRest);
                        string info = responseLineRest.Substring(1, infoEnd - 2);

                        int keywordEnd = FindTokenEnd(info);
                        string keyword = info.Substring(0, keywordEnd);
                    }
                }
            }
        }

        void CheckUnseen(Folder f)
        {
            SendCommand("STATUS", "\"" + f.FullName + "\"" + " (MESSAGES UNSEEN RECENT)", UnreadCount);
        }

        void UnreadCount(ImapRequest request, IEnumerable<string> responseData)
        {
            int folderNameEnd = FindTokenEnd(request.Args);
            string folderName = request.Args.Substring(1, folderNameEnd - 2);

            Folder folder = (from f in AllFolders
                             where f.FullName == folderName
                             select f).FirstOrDefault();

            if (folder != null)
            {
                string responseLine = responseData.First();

                for (int i = 0; i < 3; ++i)
                {
                    // Walk the first args.
                    int responseStart = FindTokenEnd(responseLine);
                    responseLine = responseLine.Substring(responseStart + 1, responseLine.Length - responseStart - 1);

                }

                string remaining = responseLine.Substring(1, responseLine.Length - 2);

                while (remaining.Length > 0)
                {

                    int nextCut = FindTokenEnd(remaining);
                    string key = remaining.Substring(0, nextCut);
                    remaining = remaining.Substring(nextCut + 1, remaining.Length - nextCut - 1);

                    nextCut = FindTokenEnd(remaining);
                    string valueStr = remaining.Substring(0, nextCut);

                    if (nextCut == remaining.Length)
                    {
                        remaining = "";
                    }
                    else
                    {
                        remaining = remaining.Substring(nextCut + 1, remaining.Length - nextCut - 1);
                    }

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
            foreach (var response in responseData)
            {
                // Each line is of the form:
                // * SEARCH <list of ids>

                string[] messages = response.Split(new char[] { ' ' });

                foreach (var msg in messages)
                {
                    int msgId = -1;
                    if (Int32.TryParse(msg, out msgId))
                    {
                        FetchMessage(msgId);
                    }
                }
            }
        }

        void FetchMessage(int id)
        {
            if (!messages_.Contains(id))
            {
                messages_.Add(new MessageHeader(id));

                SendCommand("FETCH", id + " (FLAGS INTERNALDATE UID RFC822.SIZE BODY.PEEK[HEADER.FIELDS (DATE FROM SUBJECT)])", ProcessMessage);
            }
        }

        void ProcessMessage(ImapRequest request, IEnumerable<string> responseData)
        {
            if (false)
            {
                // Remove the message, but how do we know which one it is that failed ?
            }

            string info = string.Join(" \r\n", responseData);
            // Format of this is:
            // * {id} FETCH (<field> <field data> <field> <field data> ......

            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"\* (?<id>[0-9]+) FETCH ");

            var match = regex.Match(info);

            var idStr = match.Groups["id"].Value;
            try
            {
                int id = Int32.Parse(idStr);

                var data = info.Substring(match.Length + 1, info.Length - match.Length - 2);

                MessageHeader msg = messages_.Message(id);
                ExtractValues(msg, data);
            }
            catch (Exception) { }
        }

        int FindTokenEnd(string data)
        {
            int pos = 0;
            Stack<char> toMatch = new Stack<char>();
            toMatch.Push(' ');

            while (pos != data.Length)
            {
                char current = data[pos];

                if (toMatch.Peek() == current)
                {
                    // This is an end token, so make sure that it
                    // isn't picked up as a start token.
                    current = '\0';

                    toMatch.Pop();
                    if (toMatch.Count == 0)
                    {
                        return pos;
                    }
                }

                if (current == '[') toMatch.Push(']');
                if (current == '(') toMatch.Push(')');
                if (current == '{') toMatch.Push('}');
                if (current == '<') toMatch.Push('>');
                if (current == '\"') toMatch.Push('\"');

                ++pos;
            }

            return data.Length;
        }

        void ExtractValues(MessageHeader msg, string data)
        {
            string remaining = data;

            while (remaining.Length > 0)
            {
                int nextCut = FindTokenEnd(remaining);
                string key = remaining.Substring(0, nextCut);
                remaining = remaining.Substring(nextCut + 1, remaining.Length - nextCut - 1);

                if (key == "FLAGS")
                {
                    remaining = ExtractFlags(msg, remaining);
                }
                else if (key.StartsWith("BODY["))
                {
                    remaining = ExtractBodyInfo(msg, remaining);
                }
                else if (key == "INTERNALDATE")
                {
                    remaining = ExtractDate(msg, remaining);
                }
                else if (key == "UID")
                {
                    remaining = ExtractSingle(msg, remaining, "UID");
                }
                else if (key == "RFC822.SIZE")
                {
                    remaining = ExtractSingle(msg, remaining, "SIZE");
                }
                else
                {
                    break;
                }
            }
        }

        string ExtractSingle(MessageHeader msg, string data, string key)
        {
            int dataEnd = FindTokenEnd(data);
            string value = data.Substring(0, dataEnd);
            string remaining = data.Substring(dataEnd + 1, data.Length - dataEnd - 1);

            msg.SetValue(key, value);

            return remaining;
        }

        string ExtractFlags(MessageHeader msg, string data)
        {
            // Flags are surrounded by ( ... )
            int dataEnd = FindTokenEnd(data);
            string flagString = data.Substring(1, dataEnd - 2);
            string remaining = data.Substring(dataEnd + 1, data.Length - dataEnd - 1);

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

            return remaining;
        }

        string ExtractDate(MessageHeader msg, string data)
        {
            // Date is surrounded by " ... "
            int dataEnd = FindTokenEnd(data);
            string dateString = data.Substring(1, dataEnd - 2);
            string remaining = data.Substring(dataEnd + 1, data.Length - dataEnd - 1);

            msg.SetValue("Received", dateString);

            return remaining;
        }

        string ExtractBodyInfo(MessageHeader msg, string data)
        {
            string remaining = data;

            if (data[0] == '{')
            {
                // We don't need this info
                int dataEnd = FindTokenEnd(data);
                remaining = data.Substring(dataEnd + 1, data.Length - dataEnd - 1);
            }

            // Now we should have lines that look like email header lines.
            int fieldStart = 0;

            while (true)
            {
                int fieldEnd = remaining.IndexOf(": ", fieldStart);
                if (fieldEnd == -1)
                {
                    break;
                }

                int valueEnd = remaining.IndexOf("\r\n", fieldEnd);

                string field = remaining.Substring(fieldStart, fieldEnd - fieldStart);
                string value = remaining.Substring(fieldEnd + 1, valueEnd - fieldEnd - 1);

                fieldStart = valueEnd + 2;

                value = Decode(value.Trim());

                msg.SetValue(field.Trim(), value);
            }

            return remaining.Substring(fieldStart, remaining.Length - fieldStart);
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
            SendCommand("SELECT", "\"" + f.FullName + "\"", SelectedFolder);
        }

        #endregion
    }
}
