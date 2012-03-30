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
        string id_;
        string commandName_;
        string args_;
        Imap.ResponseHandler response_;

        public string Command { get { return commandName_; } }

        public ImapRequest(string id, string commandName, string args, Imap.ResponseHandler handler)
        {
            id_ = id;
            commandName_ = commandName;
            args_ = args;
            response_ = handler;
        }

        public void Process(IEnumerable<string> resultData)
        {
            response_(resultData);
        }
    }

    public class Imap: IAccount
    {
        public delegate void ResponseHandler(IEnumerable<string> data);

        private AccountInfo account_;
        private TcpClient client_;
        private Stream stream_;

        private int readStart_;
        private byte[] incoming_;

        private ImapState state_;
        private int cmdId_ = 0;
        private Dictionary<string, ImapRequest> pendingCommands_;

        private ThreadedList<Folder> folders_;
        private MessageStore messages_;

        public Imap(AccountInfo account)
        {
            account_ = account;
            state_ = ImapState.None;
            pendingCommands_ = new Dictionary<string, ImapRequest>();

            folders_ = new ThreadedList<Folder>();
            messages_ = new MessageStore();

            client_ = new TcpClient(account.Host, account.Port);            

            if (client_.Connected)
            {
                state_ = ImapState.Connected;
                stream_ = client_.GetStream();

                incoming_ = new byte[8 * 1024];
                readStart_ = 0;

                stream_.BeginRead(incoming_, readStart_, incoming_.Length, HandleRead, null);
            }
        }

        void HandleRead(IAsyncResult res)
        {
            int bytesRead = stream_.EndRead(res) + readStart_;

            if (bytesRead > 0)
            {
                string response = UTF8Encoding.UTF8.GetString(incoming_, 0, bytesRead);

                ProcessResponse(response);
            }

            stream_.BeginRead(incoming_, readStart_, incoming_.Length - readStart_, HandleRead, null);
        }

        void ProcessResponse(string responseText)
        {
            System.Diagnostics.Debug.Write(responseText);

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

                readStart_ = UTF8Encoding.UTF8.GetBytes(leftOverResponse, 0, leftOverResponse.Length, incoming_, 0);
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

        void SendCommand(string command, string args, ResponseHandler handler)
        {
            string commandId = NextCommand();
            string cmd = commandId + " " + command;
            if (args != "")
            {
                cmd += " " + args;
            }
            cmd += "\r\n";

            pendingCommands_[commandId] = new ImapRequest(commandId, command, args, handler);

            byte[] bytes = UTF8Encoding.UTF8.GetBytes(cmd);

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

        void HandleCaps(IEnumerable<string> resultData)
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

        void HandleTLS(IEnumerable<string> responseData)
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
            SendCommand("LOGIN", account_.Username + " " + account_.Password, HandleLogin);
        }

        void HandleLogin(IEnumerable<string> resultData)
        {
            state_ = ImapState.LoggedIn;
            ListFolders();
        }

        void ListFolders()
        {
            folders_.Clear();
            SendCommand("LSUB", "\"\" \"*\"", ListedFolder);
        }

        void ListedFolder(IEnumerable<string> responseData)
        {
            foreach (var response in responseData)
            {
                // each line looks like:
                // * LSUB (<flags>) "<namespace>" "<folder name>"

                string responseBody = response.Substring(response.IndexOf('(') - 1);
                string[] data = responseBody.Split(new char[] { '\"' }, StringSplitOptions.RemoveEmptyEntries);

                string flags = data[0].Trim();
                flags = flags.Substring(1, flags.Length - 2);

                string nameSpace = data[1];
                string folder = data[3];

                folders_.Add(new Folder(folder));
            }

            SelectFolder(folders_[0]);
        }

        void ListMessages(IEnumerable<string> responseData)
        {
            state_ = ImapState.Selected;

            messages_.Clear();
            SendCommand("SEARCH", "ALL", AvailableMessages);
        }

        void AvailableMessages(IEnumerable<string> responseData)
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

                SendCommand("FETCH", id + " (FLAGS BODY.PEEK[HEADER.FIELDS (DATE FROM SUBJECT)])", ProcessMessage);
            }
        }

        void ProcessMessage(IEnumerable<string> responseData)
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

                ++pos;
            }

            return -1;
        }

        void ExtractValues(MessageHeader msg, string data)
        {
            if (data == "")
            {
                return;
            }

            int nextCut = FindTokenEnd(data);
            string key = data.Substring(0, nextCut);
            string remaining = data.Substring(nextCut + 1, data.Length - nextCut - 1);

            if (key == "FLAGS")
            {
                remaining = ExtractFlags(msg, remaining);
                ExtractValues(msg, remaining);
            } else if (key.StartsWith("BODY[")) {
                remaining = ExtractBodyInfo(msg, remaining);
                ExtractValues(msg, remaining);
            }
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

                msg.SetValue(field.Trim(), value.Trim());
            }

            return remaining.Substring(fieldStart, remaining.Length - fieldStart);
        }

        #region IAccount Members

        public IEnumerable<Folder> FolderList
        {
            get { return folders_; }
        }

        public IEnumerable<MessageHeader> MessageList
        {
            get { return messages_; }
        }

        public void SelectFolder(Folder f)
        {
            SendCommand("SELECT", f.FullName, ListMessages);
        }

        #endregion
    }
}
