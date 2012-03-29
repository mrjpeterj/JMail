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

    public class Imap: IAccount
    {
        private AccountInfo account_;
        private TcpClient client_;        

        private byte[] incoming_;

        private ImapState state_;
        private int cmdId_ = 0;
        private Dictionary<string, string> pendingCommands_;

        private ThreadedList<Folder> folders_;
        private ThreadedList<MessageHeader> messages_;

        public Imap(AccountInfo account)
        {
            account_ = account;
            state_ = ImapState.None;
            pendingCommands_ = new Dictionary<string, string>();

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

            foreach (var responseLine in responses)
            {
                string responseTo = null;
                string result = null;
                string resultData = null;
                bool commandComplete = false;

                string[] responseData = responseLine.Split(new char[] { ' ' });
                if (responseData[0] == "*")
                {
                    if (state_ == ImapState.Connected)
                    {
                        result = responseData[1];
                    }
                    else
                    {
                        responseTo = responseData[1];
                        result = "OK";
                    }

                    resultData = string.Join(" ", responseData, 2, responseData.Length - 2);
                }
                else
                {
                    // match it to the request command of this name.
                    string sentCommand = pendingCommands_[responseData[0]];
                    string[] command = sentCommand.Split(new char[] { ' ' });

                    responseTo = command[0];
                    int resultOffset = 2;
                    if (responseData[resultOffset] != responseTo)
                    {
                        --resultOffset;
                    }
                    
                    result = responseData[resultOffset];
                    resultData = string.Join(" ", responseData, resultOffset + 1, responseData.Length - resultOffset - 1);
                    commandComplete = true;

                    pendingCommands_.Remove(responseData[0]);
                }

                if (result == "OK")
                {
                    switch (state_)
                    {
                        case ImapState.Connected:
                            if (responseTo == null)
                            {
                                Login();
                            }
                            else if (responseTo == "LOGIN")
                            {
                                state_ = ImapState.LoggedIn;

                                ListFolders();
                            }
                            break;

                        case ImapState.LoggedIn:
                            if (responseTo == "LSUB")
                            {
                                if (commandComplete)
                                {
                                    SelectFolder(folders_[0]);
                                }
                                else
                                {
                                    ListedFolder(resultData);
                                }
                            }
                            else if (responseTo == "SELECT")
                            {
                                state_ = ImapState.Selected;
                                ListMessages();
                            }
                            break;

                        case ImapState.Selected:
                            if (!commandComplete)
                            {

                                if (responseTo == "SEARCH")
                                {
                                    AvailableMessages(resultData);
                                }
                                else if (responseTo == "FETCH")
                                {
                                    ProcessMessage(resultData);
                                }
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
            }
        }

        string NextCommand()
        {
            lock (this)
            {
                ++cmdId_;

                return string.Format("A{0:D4}", cmdId_);
            }
        }

        void SendCommand(string command)
        {
            string commandId = NextCommand();
            string cmd = commandId + " " + command + "\r\n";

            pendingCommands_[commandId] = command;

            byte[] bytes = UTF8Encoding.UTF8.GetBytes(cmd);

            client_.GetStream().Write(bytes, 0, bytes.Length);
        }

        void Login()
        {
            string command = "LOGIN " + account_.Username + " " + account_.Password;

            SendCommand(command);
        }

        void ListFolders()
        {
            folders_.Clear();

            string command = "LSUB " + "\"\" \"*\"";
            SendCommand(command);
        }

        void ListedFolder(string responseData)
        {
            string[] data = responseData.Split(new char[] { '\"' }, StringSplitOptions.RemoveEmptyEntries);

            string flags = data[0].Trim();
            flags = flags.Substring(1, flags.Length - 2);

            string nameSpace = data[1];
            string folder = data[3];

            folders_.Add(new Folder(folder));
        }

        void ListMessages()
        {
            messages_.Clear();
            SendCommand("SEARCH ALL");
        }

        void AvailableMessages(string responseData)
        {
            string[] messages = responseData.Split(new char[] { ' ' });

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

        void FetchMessage(int id)
        {
            //SendCommand("FETCH " + id + " ALL");
        }

        void ProcessMessage(string responseData)
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
            SendCommand("SELECT " + f.FullName);
        }

        #endregion
    }
}
