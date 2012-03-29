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
        string response_;

        public string Command { get { return commandName_; } }

        public ImapRequest(string id, string commandName, string args)
        {
            id_ = id;
            commandName_ = commandName;
            args_ = args;
        }
    }

    public class Imap: IAccount
    {
        private AccountInfo account_;
        private TcpClient client_;        

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

                incoming_ = new byte[client_.Available];
                client_.GetStream().BeginRead(incoming_, 0, incoming_.Length, HandleRead, null);
            }
        }

        void HandleRead(IAsyncResult res)
        {
            int bytesRead = client_.GetStream().EndRead(res);

            if (bytesRead > 0)
            {
                string response = UTF8Encoding.UTF8.GetString(incoming_, 0, bytesRead);

                ProcessResponse(response);
            }

            incoming_ = new byte[client_.Available];
            client_.GetStream().BeginRead(incoming_, 0, incoming_.Length, HandleRead, null);
        }

        void ProcessResponse(string responseText)
        {
            System.Diagnostics.Debug.Write(responseText);

            string[] responses = responseText.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            List<string> commandResponse = new List<string>();

            foreach (var responseLine in responses)
            {
                string[] responseData = responseLine.Split(new char[] { ' ' });

                // match it to the request command of this name.
                ImapRequest request = null;
                pendingCommands_.TryGetValue(responseData[0], out request);

                if (request == null)
                {
                    if (state_ == ImapState.Connected && responseData[1] == "OK")
                    {
                        Login();
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


                    switch (state_)
                    {
                        case ImapState.Connected:
                            if (request.Command == "LOGIN")
                            {
                                state_ = ImapState.LoggedIn;
                                ListFolders();
                            }
                            break;

                        case ImapState.LoggedIn:
                            if (request.Command == "LSUB")
                            {
                                ListedFolder(commandResponse);
                                SelectFolder(folders_[0]);
                            }
                            else if (request.Command == "SELECT")
                            {
                                state_ = ImapState.Selected;
                                ListMessages();
                            }
                            break;

                        case ImapState.Selected:
                            if (request.Command == "SEARCH")
                            {
                                AvailableMessages(commandResponse);
                            }
                            else if (request.Command == "FETCH")
                            {
                                ProcessMessage(commandResponse, success);
                            }
                            break;
                    }

                    commandResponse.Clear();
                }
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

        void SendCommand(string command, string args)
        {
            string commandId = NextCommand();
            string cmd = commandId + " " + command;
            if (args != "")
            {
                cmd += " " + args;
            }
            cmd += "\r\n";

            pendingCommands_[commandId] = new ImapRequest(commandId, command, args);

            byte[] bytes = UTF8Encoding.UTF8.GetBytes(cmd);

            client_.GetStream().Write(bytes, 0, bytes.Length);
        }

        void Login()
        {
            SendCommand("LOGIN", account_.Username + " " + account_.Password);
        }

        void ListFolders()
        {
            folders_.Clear();
            SendCommand("LSUB", "\"\" \"*\"");
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
        }

        void ListMessages()
        {
            messages_.Clear();
            SendCommand("SEARCH", "ALL");
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

                SendCommand("FETCH", id + " (FLAGS BODY.PEEK[HEADER.FIELDS (DATE FROM SUBJECT)])");
            }
        }

        void ProcessMessage(IEnumerable<string> responseData, bool success)
        {
            if (!success)
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
            string flags = data.Substring(0, dataEnd);
            string remaining = data.Substring(dataEnd + 1, data.Length - dataEnd - 1);

            // Process flags here

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
            SendCommand("SELECT", f.FullName);
        }

        #endregion
    }
}
