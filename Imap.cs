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
        private ThreadedList<MessageHeader> messages_;

        public Imap(AccountInfo account)
        {
            account_ = account;
            state_ = ImapState.None;
            pendingCommands_ = new Dictionary<string, ImapRequest>();

            folders_ = new ThreadedList<Folder>();
            messages_ = new ThreadedList<MessageHeader>();

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

                    if (result == "OK")
                    {
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
                                    ProcessMessage(commandResponse);
                                }
                                break;
                        }
                    }
                    else if (result == "NO")
                    {
                    }
                    else if (result == "BAD")
                    {
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
                        messages_.Add(new MessageHeader(msgId));

                        FetchMessage(msgId);
                    }
                }
            }
        }

        void FetchMessage(int id)
        {
            SendCommand("FETCH", id + " (FLAGS BODY[HEADER.FIELDS (DATE FROM SUBJECT)])");
        }

        void ProcessMessage(IEnumerable<string> responseData)
        {
            
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
