using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;

using System.Net;
using System.Net.Sockets;

namespace JMail.Core
{
    public enum ImapState
    {
        None,
        Connected,
        LoggedIn,
        Selected
    }

    public enum IdleState
    {
        // Not suppport
        None,

        // Asked server to enter the mode
        ReqestedStart,

        // Mode entered
        On,

        // Asked server to stop the mode
        RequestStop,

        // Not in the IDLE mode
        Off
    }

    internal class ImapRequest
    {
        internal delegate void ResponseHandler(ImapRequest request, IList<string> responseData, IList<byte[]> resposeBytes, object data);

        string id_;
        string commandName_;
        string args_;
        DateTime requestStart_;
        ResponseHandler response_;
        ResponseHandler errorResponse_;

        object data_;

        public string Key { get { return id_; } }
        public string Command { get { return commandName_; } }
        public string Args { get { return args_; } }
        public DateTime RequestStart { get { return requestStart_; } }

        public ImapRequest(string id, string commandName, string args, ResponseHandler handler, ResponseHandler errorHandler, object data)
        {
            id_ = id;
            commandName_ = commandName;
            args_ = args;
            requestStart_ = DateTime.Now;
            response_ = handler;
            errorResponse_ = errorHandler;
            data_ = data;
        }

        public void Process(IList<byte[]> resultData, bool success)
        {
            List<string> results = new List<string>();
            foreach (var res in resultData)
            {
                results.Add(System.Text.Encoding.ASCII.GetString(res));
            }

            if (success)
            {
                response_(this, results, resultData, data_);
            }
            else
            {
                if (errorResponse_ != null)
                {
                    errorResponse_(this, results, resultData, data_);
                }
            }
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
        private Queue<ImapRequest> pendingRequests_;
        private Dictionary<string, ImapRequest> pendingResponses_;
        private List<byte[]> currentCommand_;
        private bool lastTokenIsComplete_;

        private List<Folder> allFolders_;
        private List<Folder> folders_;

        private Folder currentFolder_;

        private bool authPlain_;

        private volatile IdleState idling_;

        private System.Timers.Timer folderCheckTimer_;

        public Imap(AccountInfo account)
        {
            account_ = account;
            state_ = ImapState.None;
            pendingResponses_ = new Dictionary<string, ImapRequest>();
            pendingRequests_ = new Queue<ImapRequest>();

            currentCommand_ = new List<byte[]>();
            lastTokenIsComplete_ = true;

            allFolders_ = new List<Folder>();
            folders_ = new List<Folder>();

            authPlain_ = false;
            idling_ = IdleState.None;

            folderCheckTimer_ = new System.Timers.Timer(30 * 1000);
            folderCheckTimer_.Elapsed += CheckCurrent;

            incoming_ = new byte[8 * 1024];
        }

        // Instead of calling Connect to initiate a network data stream,
        // just call this to explicitly set as custom one.
        public void SetStream(Stream s, ImapState newState)
        {
            stream_ = s;

            state_ = newState;
            stream_.BeginRead(incoming_, 0, incoming_.Length, HandleRead, null);
        }

        public void Connect()
        {
            // On reconnect, we might have requests in the pending queue that prevent the connect from happen.
            // We need to find a solution for this.

            System.Diagnostics.Debug.WriteLine("Attempting connect to " + account_.Host);

            client_ = new TcpClient();

            client_.BeginConnect(account_.Host, account_.Port, ConnectComplete, null);
        }

        private void ConnectComplete(IAsyncResult aRes)
        {
            try
            {
                client_.EndConnect(aRes);
            }
            catch (IOException e)
            {
                return;
            }

            if (client_.Connected)
            {
                state_ = ImapState.Connected;
                stream_ = client_.GetStream();

                if (account_.Encrypt)
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
            try
            {
                if (stream_ == null)
                {
                    // Shutdown has happened elsewhere
                    state_ = ImapState.None;

                    return;
                }

                int bytesRead = stream_.EndRead(res);

                if (bytesRead > 0)
                {
                    ProcessResponse(bytesRead);
                }

                stream_.BeginRead(incoming_, 0, incoming_.Length, HandleRead, null);
            }
            catch (System.IO.IOException)
            {
                // Socket read failure.
                // Reconnect.

                stream_.Close();
                client_.Close();

                state_ = ImapState.None;

                Connect();
                return;
            }
            catch (System.ObjectDisposedException)
            {
                // Most likely shutdown happened

                state_ = ImapState.None;

                return;
            }
        }

        void ProcessResponse(int bytesRead)
        {
            {
                string responseText = encoder_.GetString(incoming_, 0, bytesRead);

                System.Diagnostics.Debug.WriteLine(">>>>>>>> " + account_.Host + " <<<<<<<");
                System.Diagnostics.Debug.Write(responseText);
                System.Diagnostics.Debug.WriteLine("========");
            }

            byte[] currentResponse = null;

            if (!lastTokenIsComplete_)
            {
                byte[] lastToken = null;

                if (currentCommand_.Any())
                {
                    lastToken = currentCommand_.Last();
                    currentCommand_.RemoveAt(currentCommand_.Count - 1);

                    currentResponse = new byte[bytesRead + lastToken.Length];
                    Array.Copy(lastToken, 0, currentResponse, 0, lastToken.Length);
                    Array.Copy(incoming_, 0, currentResponse, lastToken.Length, bytesRead);
                }

                lastTokenIsComplete_ = true;
            }

            if (currentResponse == null)
            {
                currentResponse = new byte[bytesRead];
                Array.Copy(incoming_, 0, currentResponse, 0, bytesRead);
            }

            List<byte[]> responses = new List<byte[]>();
            bool lastIsComplete = ImapData.SplitTokens(currentResponse, responses);

            ImapRequest request = null;
            string result = null;

            for (int i = 0; i < responses.Count; ++i)
            {
                string response = encoder_.GetString(responses[i]);

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
                            int a = 0;
                        }
                        else if (result == "BAD")
                        {
                            int a = 0;
                        }

                        request.Process(currentCommand_, success);

                        currentCommand_.Clear();
                        pendingResponses_.Remove(request.Key);

                        ProcessPending();

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
                pendingResponses_.TryGetValue(response, out request);

                if (request == null)
                {
                    if (state_ == ImapState.Connected && response == "OK")
                    {
                        StartUp(responses);
                        currentCommand_.Clear();
                        return;
                    }
                    else if (currentCommand_.Any() || response == "*")
                    {
                        currentCommand_.Add(responses[i]);
                    }
                }
                else
                {
                    currentCommand_.Add(responses[i]);
                }
            }

            if (!lastIsComplete)
            {
                // Remember whether the last token in the split was a complete one,
                // so that we know whether to append to it in the next round.

                lastTokenIsComplete_ = lastIsComplete;
            }
            else if (idling_ == IdleState.On && currentCommand_.Any())
            {
                // IDLE doesn't have a completion command until it kicks you off the end.
                List<string> udResponses = new List<string>();
                foreach (var res in currentCommand_)
                {
                    udResponses.Add(encoder_.GetString(res));
                }
                UpdateStatus(null, udResponses, currentCommand_, currentFolder_);
            }

            if (idling_ == IdleState.On && pendingResponses_.Any())
            {
                // The IDLE command can end up expecting a response, when it doesn't get one.
                int a = 0;
            }

            if (!pendingRequests_.Any() && !pendingResponses_.Any() &&
                !folderCheckTimer_.Enabled && currentFolder_ != null)
            {
                CheckCurrent(this, null);
            }
        }

        string NextCommand()
        {
            lock (this)
            {
                ++cmdId_;

                string accountName = account_.Name.Replace(' ', '_');

                return string.Format("__{1}__X_{0:D4}", cmdId_, accountName);
            }
        }

        void SendCommand(string command, string args, ImapRequest.ResponseHandler handler, ImapRequest.ResponseHandler errorHandler, object data)
        {
            if (folderCheckTimer_.Enabled)
            {
                // We are off to do some other requests, so don't run the folder updater.
                folderCheckTimer_.Stop();
            }

            string commandId = NextCommand();

            var request = new ImapRequest(commandId, command, args, handler, errorHandler, data);
            pendingRequests_.Enqueue(request);

            if (pendingResponses_.Count < 5)
            {
                ProcessPending();
            }
        }

        void SendRawResponse(string response)
        {
            System.Diagnostics.Debug.WriteLine("++++++++");
            System.Diagnostics.Debug.WriteLine(response);
            System.Diagnostics.Debug.WriteLine("++++++++");

            response += "\r\n";


            byte[] bytes = encoder_.GetBytes(response);

            try
            {
                stream_.Write(bytes, 0, bytes.Length);
                stream_.Flush();
            }
            catch (System.IO.IOException)
            {
                // Socket write failure.
                // Reconnect.

                stream_.Close();
                client_.Close();

                state_ = ImapState.None;

                Connect();
                return;
            }
        }

        void ProcessPending()
        {
            if (!pendingRequests_.Any())
            {
                return;
            }

            if (idling_ == IdleState.On)
            {
                idling_ = IdleState.RequestStop;

                // Need to terminate the idle first.
                string endIdle = "DONE";

                SendRawResponse(endIdle);

                return;
            }
            else if (idling_ == IdleState.ReqestedStart)
            {
                var requestPeek = pendingRequests_.Peek();

                if (requestPeek.Command == "IDLE")
                {
                    idling_ = IdleState.On;
                }
            }
            else if (idling_ == IdleState.RequestStop)
            {
                // Wait until it really has stopped.
                return;
            }


            var request = pendingRequests_.Dequeue();

            string cmd = request.Key + " " + request.Command;
            if (request.Args != "")
            {
                cmd += " " + request.Args;
            }

            pendingResponses_[request.Key] = request;

            SendRawResponse(cmd);

            if (request.Command == "IDLE")
            {
                System.Diagnostics.Debug.WriteLine("IDLE Started");
            }
        }

        void StartUp(IEnumerable<byte[]> responseData)
        {
            foreach (var resp in responseData)
            {
                string respString = encoder_.GetString(resp);

                if (respString.StartsWith("[CAPABILITY "))
                {
                    var tokens = ImapData.SplitToken(respString);

                    // Look to see if STARTTLS is supported before asking for caps
                    // as caps might be different after TLS.

                    foreach (var token in tokens)
                    {
                        if (token == "STARTTLS")
                        {
                            StartTLS(Caps);

                            return;
                        }
                    }
                }
            }

            Caps();
        }

        void Caps()
        {
            SendCommand("CAPABILITY", "", HandleCaps, null, null);
        }

        void HandleCaps(ImapRequest request, IEnumerable<string> resultData, IEnumerable<byte[]> responseBytes, object data)
        {
            // Looks like 
            // * CAPABILITY <cap1> <cap2> <cap3>

            bool hasTLS = false;

            foreach (var cap in resultData)
            {
                if (cap == "*" || cap == "CAPABILITY")
                {
                    continue;
                }


                if (cap.StartsWith("AUTH="))
                {
                    if (cap.EndsWith("=PLAIN"))
                    {
                        authPlain_ = true;
                    }
                }
                else if (cap == ("STARTTLS"))
                {
                    hasTLS = true;
                }
                else if (cap == "IDLE")
                {
                    idling_ = IdleState.Off;
                }
            }

            if (hasTLS)
            {
                StartTLS(Login);
            }
            else
            {
                Login();
            }
        }

        void StartTLS(Action continuation)
        {
            if (stream_ is System.Net.Security.SslStream)
            {
                // TLS already started
                continuation();
            }
            else
            {
                SendCommand("STARTTLS", "", HandleTLS, null, continuation);
            }
        }

        void HandleTLS(ImapRequest request, IEnumerable<string> responseData, IEnumerable<byte[]> responseBytes, object data)
        {
            var sslStream = new System.Net.Security.SslStream(client_.GetStream(), false,
                new System.Net.Security.RemoteCertificateValidationCallback(GotRemoteCert));
            sslStream.AuthenticateAsClient(account_.Host);

            stream_ = sslStream;

            // Data should be the follow function
            Action continuation = data as Action;

            if (continuation != null)
            {
                continuation();
            }
        }

        bool GotRemoteCert(object sender, System.Security.Cryptography.X509Certificates.X509Certificate cert,
            System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors errors)
        {
            return true;
        }

        void Login()
        {
            if (authPlain_)
            {
                DoAuthPlain();
            }
            else
            {
                DoLogin();
            }
        }

        void DoAuthPlain()
        {
            SendCommand("AUTHENTICATE", "PLAIN", HandleAuth, HandleAuthFailed, null);

            string response = '\0' + account_.Username + '\0' + account_.GetPassword();

            string encResponse = Convert.ToBase64String(Encoding.UTF8.GetBytes(response));

            SendRawResponse(encResponse);
        }

        void DoLogin()
        {
            SendCommand("LOGIN", account_.Username + " " + account_.GetPassword(), HandleLogin, HandleAuthFailed, null);
        }

        void HandleAuth(ImapRequest request, IEnumerable<string> resultData, IEnumerable<byte[]> responseBytes, object data)
        {
            state_ = ImapState.LoggedIn;
            ListFolders();
        }

        void HandleAuthFailed(ImapRequest request, IEnumerable<string> resultData, IEnumerable<byte[]> responseBytes, object data)
        {
            if (AuthFailed != null)
            {
                AuthFailed(this, new EventArgs());
            }
        }

        void HandleLogin(ImapRequest request, IEnumerable<string> resultData, IEnumerable<byte[]> responseBytes, object data)
        {
            state_ = ImapState.LoggedIn;
            ListFolders();
        }

        void ListFolders()
        {
            allFolders_.Clear();
            folders_.Clear();

            SendCommand("LSUB", "\"\" \"*\"", ListedFolder, null, null);
        }

        void ListedFolder(ImapRequest request, IEnumerable<string> responseData, IEnumerable<byte[]> responseBytes, object data)
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

                string nameSpace = ImapData.StripQuotes(responseLine[3], false);
                string folderName = ImapData.StripQuotes(responseLine[4], false);
                string folderShortName = folderName;

                while (currentParent != null)
                {
                    if (!folderName.Contains(currentParent.FullName))
                    {
                        // No longer a parent of current parent, look for a new one.
                        currentParent = (from f in allFolders_
                                         where f.Children != null && f.Children.Contains(currentParent)
                                         select f).FirstOrDefault();
                    }
                    else
                    {
                        break;
                    }
                }

                if (currentParent != null)
                {
                    folderShortName = folderName.Replace(currentParent.FullName, "").Substring(1);
                }

                Folder folder = new Folder(this, folderName, folderShortName, nameSpace, hasChildren, realFolder);

                if (currentParent != null)
                {
                    currentParent.Children.Add(folder);
                }
                else
                {
                    // Inbox should always be first
                    if (folderName.Equals("Inbox", StringComparison.OrdinalIgnoreCase))
                    {
                        folders_.Insert(0, folder);
                    }
                    else
                    {
                        folders_.Add(folder);
                    }
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

            if (FoldersChanged != null)
            {
                FoldersChanged(this, null);
            }
        }

        void SelectedFolder(ImapRequest request, IEnumerable<string> responseData, IEnumerable<byte[]> responseBytes, object data)
        {
            currentFolder_ = data as Folder;

            ListMessages(request, responseData, responseBytes, data as Folder);

            SendCommand("UID SEARCH", "UNDELETED", AvailableMessages, null, data);
        }

        void RenamedFolder(ImapRequest request, IEnumerable<string> responseData, IEnumerable<byte[]> responseBytes, object data)
        {
            var response = responseData.ToList();
            if (response[1] == "OK")
            {
                SubscribeFolder(data.ToString());
            }
        }

        void SubscribedFolder(ImapRequest request, IEnumerable<string> responseData, IEnumerable<byte[]> responseBytes, object data)
        {
            ListFolders();
        }

        // Returns whether it thinks that the message list should be updated.
        bool ListMessages(ImapRequest request, IEnumerable<string> responseData, IEnumerable<byte[]> responseBytes, Folder folder)
        {
            bool updated = false;

            string lastValue = null;
            bool subProcessNext = false;
            MessageHeader msg = null;

            int prevExists = folder.Exists;
            folder.Recent = 0;

            bool refreshStatus = false;

            var responseStringList = responseData.ToList();
            var responseBytesList = responseBytes.ToList();

            for (int i = 0; i < responseStringList.Count(); ++i)
            {
                string response = responseStringList[i];

                if (subProcessNext)
                {
                    string[] responseInfo = ImapData.SplitToken(response);

                    subProcessNext = false;
                }
                else if (msg != null)
                {
                    var res = ExtractValues(msg.id, null, responseBytesList[i], folder);
                    if (res.IsNew && !refreshStatus)
                    {
                        refreshStatus = true;
                    }

                    msg = null;
                }

                if (response == "EXISTS")
                {
                    folder.Exists = Int32.Parse(lastValue);
                }
                else if (response == "RECENT")
                {
                    folder.Recent = Int32.Parse(lastValue);
                }
                else if (response == "EXPUNGE")
                {
                    int msgId = Int32.Parse(lastValue);

                    MessageHeader exMsg = folder.MessageByID(msgId);
                    folder.Expunge(exMsg, msgId);

                    updated = true;
                }
                else if (response == "FETCH")
                {
                    int msgId = Int32.Parse(lastValue);

                    msg = folder.MessageByID(msgId);
                }
                else if (response == "OK")
                {
                    subProcessNext = true;
                }

                lastValue = response;
            }

            if (refreshStatus)
            {
                CheckUnseen(folder);

                if (MessagesChanged != null)
                {
                    MessagesChanged(this, new MessagesChangedEventArgs(folder, null));
                }
            }

            if (folder.Exists != prevExists || folder.Recent > 0)
            {
                updated = true;
            }

            return updated;
        }

        void CheckUnseen(Folder f)
        {
            SendCommand("STATUS", "\"" + f.FullName + "\"" + " (MESSAGES UNSEEN RECENT)", UnreadCount, StatusFailed, f);
        }

        void UnreadCount(ImapRequest request, IList<string> responseData, IEnumerable<byte[]> responseBytes, object data)
        {
            string folderName = ImapData.StripQuotes(responseData[2], false);

            Folder folder = (from f in AllFolders
                             where f.FullName == folderName
                             select f).FirstOrDefault();

            if (folder != null)
            {
                string info = responseData[3];
                var infoData = ImapData.SplitToken(info);

                bool changed = false;

                for (int i = 0; i < infoData.Length; i = i + 2)
                {
                    string key = infoData[i];
                    string valueStr = infoData[i + 1];

                    try
                    {
                        int value = Int32.Parse(valueStr);

                        if (key == "UNSEEN")
                        {
                            if (folder.Unseen != value)
                            {
                                changed = true;
                            }

                            folder.Unseen = value;
                        }
                        else if (key == "MESSAGES")
                        {
                            if (folder.Exists != value)
                            {
                                changed = true;
                            }

                            folder.Exists = value;
                        }
                        else if (key == "RECENT")
                        {
                            if (folder.Recent != value)
                            {
                                changed = true;
                            }

                            folder.Recent = value;
                        }
                    }
                    catch (FormatException) { }
                }

                if (changed && MessagesChanged != null)
                {
                    MessagesChanged(this, new MessagesChangedEventArgs(folder, null));
                }
            }
        }

        void StatusFailed(ImapRequest request, IList<string> responseData, IEnumerable<byte[]> responseBytes, object data)
        {
            // This folder can't be polled for a status, so it probably doesn't really exist.

            Folder f = data as Folder;

            folders_.Remove(f);
            allFolders_.Remove(f);

            if (FoldersChanged != null)
            {
                FoldersChanged(this, null);
            }
        }

        void UpdateStatus(ImapRequest request, IEnumerable<string> responseData, IEnumerable<byte[]> responseBytes, object data)
        {
            if (responseData.Any())
            {
                if (ListMessages(request, responseData, responseBytes, data as Folder))
                {
                    SendCommand("UID SEARCH", "UNDELETED", AvailableMessages, null, data);
                }
            }
        }

        void AvailableMessages(ImapRequest request, IEnumerable<string> responseData, IEnumerable<byte[]> responseBytes, object data)
        {
            // Each line is of the form:
            // * SEARCH <list of ids>

            Folder folder = data as Folder;

            List<int> msgIds = new List<int>();

            foreach (var msg in responseData)
            {
                int msgId = -1;
                if (Int32.TryParse(msg, out msgId))
                {
                    msgIds.Add(msgId);
                }
            }

            var currentIds = (from m in folder.Messages
                              select m.Uid).ToList();
            List<int> newIds = new List<int>();
            List<int> existingIds = new List<int>();

            foreach (var uid in msgIds)
            {
                if (!currentIds.Contains(uid))
                {
                    newIds.Add(uid);
                }
                else
                {
                    currentIds.Remove(uid);
                    existingIds.Add(uid);
                }
            }

            foreach (var uid in currentIds)
            {
                // These are no longer in the folder
                folder.Messages.Remove(folder.MessageByUID(uid));
            }

            if (newIds.Any())
            {
                FetchMessage(newIds, false, folder);
            }

            if (existingIds.Any())
            {
                FetchMessage(existingIds, true, folder);
            }

            if (MessagesChanged != null)
            {
                MessagesChanged(this, new MessagesChangedEventArgs(folder, null));
            }
        }

        void FetchMessage(IList<int> ids, bool flagsOnly, Folder f)
        {
            string idList = "";
            string command = "(FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE)";
            if (flagsOnly)
            {
                command = "(FLAGS)";
            }

            for (int i = 0; i < ids.Count; ++i)
            {
                int id = ids[i];

                if (idList.Length > 0)
                {
                    idList += ",";
                }
                idList += id;

                if (i % 50 == 49)
                {
                    // Batch into 50's

                    SendCommand("UID FETCH", idList + " " + command, ProcessMessage, null, f);

                    idList = "";
                }
            }

            if (idList.Length > 0)
            {
                SendCommand("UID FETCH", idList + " " + command, ProcessMessage, null, f);
            }
        }

        void ProcessMessage(ImapRequest request, IEnumerable<string> responseData, IEnumerable<byte[]> responseBytes, object data)
        {
            // Format of this is:
            // * {id} FETCH (<field> <field data> <field> <field data> ......

            bool isId = false;
            bool isResponse = false;

            int msgId = -1;

            // data might be one of two things
            BodyPart body = data as BodyPart;
            Folder folder = data as Folder;
            if (folder == null && body != null)
            {
                // When we have forwarded a BodyPart through the request, we
                // want the current folder
                folder = currentFolder_;
            }

            Folder refreshFolder = null;
            List<MessageHeader> refreshMessages = new List<MessageHeader>();

            var responseStringList = responseData.ToList();
            var responseBytesList = responseBytes.ToList();

            for (int i = 0; i < responseStringList.Count(); ++i)
            {
                string response = responseStringList[i];

                if (response == "*")
                {
                    isId = true;
                }
                else if (response == "FETCH")
                {
                    isResponse = true;
                }
                else if (response == "EXPUNGE")
                {
                    refreshFolder = currentFolder_;
                    refreshMessages.Clear();

                    MessageHeader exMsg = refreshFolder.MessageByID(msgId);
                    refreshFolder.Expunge(exMsg, msgId);
                }
                else if (isId)
                {
                    msgId = Int32.Parse(response);

                    isId = false;
                }
                else if (isResponse)
                {
                    var res = ExtractValues(msgId, body, responseBytesList[i], folder);
                    if (refreshFolder == null)
                    {
                        if (res.IsNew)
                        {
                            refreshFolder = res.Message.Folder;

                            if (refreshMessages.Any())
                            {
                                // The whole folder needs a refresh anyway
                                refreshMessages.Clear();
                            }
                        }
                        else if (res.IsModified)
                        {
                            refreshMessages.Add(res.Message);
                        }
                    }

                    isResponse = false;
                }
            }

            if (refreshFolder != null || refreshMessages.Any())
            {
                if (refreshFolder == null)
                {
                    // If we just have a list of messages, then we didn't want to indicate a full 
                    // folder update, but now we need to know what the folder is.
                    refreshFolder = refreshMessages.First().Folder;
                }

                if (MessagesChanged != null)
                {
                    MessagesChanged(this, new MessagesChangedEventArgs(refreshFolder, refreshMessages));
                }
            }
        }

        MessageHeaderProcessResult ExtractValues(int msgId, BodyPart body, byte[] data, Folder folder)
        {
            var values = ImapData.SplitToken(data);
            Dictionary<string, byte[]> dictValues = new Dictionary<string, byte[]>();
            for (int i = 0; i < values.Count(); i = i + 2)
            {
                string val1 = encoder_.GetString(values[i]);

                dictValues.Add(val1, values[i + 1]);
            }

            byte[] uidStr;
            int uid = -1;
            if (dictValues.TryGetValue("UID", out uidStr))
            {
                Int32.TryParse(encoder_.GetString(uidStr), out uid);
            }

            MessageHeaderProcessResult res = new MessageHeaderProcessResult();

            if (uid >= 0)
            {
                res.Message = folder.MessageByUID(uid);
            }
            else if (msgId >= 0)
            {
                res.Message = folder.MessageByID(msgId);
            }

            if (res.Message == null)
            {
                res.Message = new MessageHeader(uid, folder);
                folder.Messages.Add(res.Message);

                res.Message.id = msgId;

                res.IsNew = true;
            }

            foreach (var val in dictValues)
            {
                string key = val.Key;
                string value = encoder_.GetString(val.Value);

                if (key == "FLAGS")
                {
                    ExtractFlags(res, value);

                    // This message needs to be marked as updated for the UI.
                    res.IsModified = true;
                }
                else if (key.StartsWith("BODY["))
                {
                    ExtractBodyInfo(res.Message, body, val.Value);
                }
                else if (key == "INTERNALDATE")
                {
                    ExtractDate(res.Message, value);
                }
                else if (key == "RFC822.SIZE")
                {
                    ExtractSingle(res.Message, value, "SIZE");
                }
                else if (key == "ENVELOPE")
                {
                    ParseEnvelope(res.Message, value);
                }
                else if (key == "BODYSTRUCTURE")
                {
                    ParseBodyStructure(res.Message, value, "");
                }
            }

            return res;
        }

        void ParseEnvelope(MessageHeader msg, string envData)
        {
            string[] envItems = ImapData.SplitToken(envData);

            // Basic string fields
            string dateStr = ImapData.StripQuotes(envItems[0], true);
            string subject = ImapData.StripQuotes(envItems[1], false);
            string inReplyTo = ImapData.StripQuotes(envItems[8], false);
            string msgId = ImapData.StripQuotes(envItems[9], false);

            msg.SetValue("Date", dateStr);
            msg.SetValue("Subject", EncodedText.DecodeWord(subject));
            msg.SetValue("In-Reply-To", inReplyTo);
            msg.SetValue("Message-Id", msgId);

            string[] from = ImapData.SplitToken(envItems[2]);
            string[] sender = ImapData.SplitToken(envItems[3]);
            string[] replyTo = ImapData.SplitToken(envItems[4]);
            string[] to = ImapData.SplitToken(envItems[5]);
            string[] cc = ImapData.SplitToken(envItems[6]);
            string[] bcc = ImapData.SplitToken(envItems[7]);

            //var senderAddr = AddressBuilder(ImapData.SplitToken(sender[0]));

            msg.From = AddressBuilder(ImapData.SplitToken(from[0]));

            if (replyTo != null)
            {
                msg.ReplyTo = AddressBuilder(ImapData.SplitToken(replyTo[0]));
            }

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
                // Find where the sub parts finish.
                int typePos = 0;

                for (int p = 0; p < dataPieces.Length; ++p)
                {
                    if (!ImapData.IsArray(dataPieces[p]))
                    {
                        typePos = p;
                        break;
                    }
                }

                // Analyze multi-part type                
                string multiType = ImapData.StripQuotes(dataPieces[typePos], false);
                string[] paramSet = ImapData.SplitToken(dataPieces[typePos + 1]);
                List<string> paramList = null;
                if (paramSet != null)
                {
                    paramList = paramSet.ToList();
                    for (int i = 0; i < paramList.Count; ++i)
                    {
                        paramList[i] = ImapData.StripQuotes(paramList[i], false);
                    }
                }

                if (multiType.Equals("ALTERNATIVE", StringComparison.InvariantCultureIgnoreCase))
                {
                    for (int i = 0; i < typePos; ++i)
                    {
                        string subLoc = AppendTextLocation(loc, i + 1);

                        ParseBodyStructure(msg, dataPieces[i], subLoc);
                    }
                }
                else if (multiType.Equals("RELATED", StringComparison.InvariantCultureIgnoreCase))
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

                    for (int i = 0; i < typePos; ++i)
                    {
                        string subLoc = AppendTextLocation(loc, i + 1);
                        var part = ParseBodyStructure(msg, dataPieces[i], subLoc);
                        if (part != null)
                        {
                            if (i + 1 == start)
                            {
                            }
                            else
                            {
                                msg.AddRelated(part);
                            }
                        }
                    }
                }
                else // if (multiType == "MIXED")
                     // spec states that the default behaviour should be the same as for 'mixed' 
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

                return null;
            }
            else
            {
                if (loc == "")
                {
                    loc += 1;
                }

                ImapBodyPart bodyPart = new ImapBodyPart(msg, dataPieces);
                bodyPart.PartNumber = loc;

                if (bodyPart.ContentType.MediaType.StartsWith("text"))
                {
                    if (bodyPart.ContentType.MediaType.EndsWith("/html"))
                    {
                        msg.Body = bodyPart;
                    }
                    else if (msg.Body == null)
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

        void ExtractFlags(MessageHeaderProcessResult res, string flagString)
        {
            // Process flags here
            var msg = res.Message;

            // To avoid always having to update the folder list, we try and manage the UnRead count.

            bool wasRead = !msg.UnRead;

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

            bool isRead = !msg.UnRead;

            if (isRead != wasRead)
            {
                if (res.IsNew)
                {
                }
                else
                {
                    if (isRead)
                    {
                        --msg.Folder.Unseen;
                    }
                    else
                    {
                        ++msg.Folder.Unseen;
                    }
                }
            }
        }

        void ExtractDate(MessageHeader msg, string dateString)
        {
            msg.SetValue("Received", dateString.Substring(1, dateString.Length - 2));
        }

        void ExtractBodyInfo(MessageHeader msg, BodyPart body, byte[] data)
        {
            // quotes should be stripped by the other side as it is easier for it to do that.

            body.SetContent(data);
        }

        static System.Net.Mail.MailAddress AddressBuilder(string[] addressParts)
        {
            string address = ImapData.StripQuotes(addressParts[2], false) + "@" + ImapData.StripQuotes(addressParts[3], false);
            string displayName = EncodedText.DecodeWord(ImapData.StripQuotes(addressParts[0], false));

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

        void IdleComplete(ImapRequest req, IEnumerable<string> data, IEnumerable<byte[]> responseBytes, object state)
        {
            System.Diagnostics.Debug.WriteLine("Stopped IDLE");

            idling_ = IdleState.Off;

            // Now send the message that we queued up while we waited for IDLE to complete.
            ProcessPending();
        }

        void CheckCurrent(object state, EventArgs e)
        {
            if (currentFolder_ != null)
            {
                if (idling_ == IdleState.Off)
                {
                    idling_ = IdleState.ReqestedStart;

                    // Check the current status first before going into
                    // idle otherwise things that happened since the
                    // previous poll won't get noticed.
                    SendCommand("NOOP", "", UpdateStatus, null, currentFolder_);

                    SendCommand("IDLE", "", IdleComplete, null, currentFolder_);
                }
                else if (idling_ == IdleState.None)
                {
                    SendCommand("NOOP", "", UpdateStatus, null, currentFolder_);

                    // We have to poll in this case, so start the timer again.
                    folderCheckTimer_.Start();
                }
            }
        }

        void SearchResponse(ImapRequest req, IEnumerable<string> responseData, IEnumerable<byte[]> responseBytes, object state)
        {
            // Response looks like:
            // * SEARCH <list of ids>

            if (state != currentFolder_)
            {
                return;
            }

            List<int> msgIds = new List<int>();

            foreach (var msg in responseData)
            {
                int msgId = -1;
                if (Int32.TryParse(msg, out msgId))
                {
                    msgIds.Add(msgId);
                }
            }

            var currentIds = (from m in currentFolder_.Messages
                              select m.id).ToList();

            List<MessageHeader> selected = new List<MessageHeader>();

            foreach (var id in msgIds)
            {
                var msg = currentFolder_.MessageByID(id);

                if (msg != null)
                {
                    selected.Add(msg);
                }
                else
                {
                    int a = 0;
                }
            }

            currentFolder_.ViewMessages = selected;

            if (MessagesChanged != null)
            {
                MessagesChanged(this, new MessagesChangedEventArgs(currentFolder_, null));
            }
        }

        #region IAccount Members

        public event EventHandler FoldersChanged;
        public event EventHandler<MessagesChangedEventArgs> MessagesChanged;
        public event EventHandler AuthFailed;

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
            SendCommand("SELECT", "\"" + f.FullName + "\"", SelectedFolder, null, f);
        }

        public void UnselectFolder(Folder f)
        {
            if (currentFolder_ == f)
            {
                currentFolder_ = null;

                folderCheckTimer_.Stop();
            }
        }

        public void RenameFolder(string oldName, string newName)
        {
            SendCommand("RENAME", oldName + " " + newName, RenamedFolder, null, newName);
        }

        public void SubscribeFolder(string folderName)
        {
            SendCommand("SUBSCRIBE", folderName, SubscribedFolder, null, null);
        }

        public void FetchMessage(MessageHeader m, BodyPart body)
        {
            if (body == null)
            {
                SendCommand("UID FETCH", m.Uid + " (FLAGS BODY.PEEK[])", ProcessMessage, null, body);
            }
            else if (body == m.Body)
            {
                SendCommand("UID FETCH", m.Uid + " (FLAGS BODY.PEEK[" + body.PartNumber + "])", ProcessMessage, null, body);
            }
            else
            {
                SendCommand("UID FETCH", m.Uid + " (BODY.PEEK[" + body.PartNumber + "])", ProcessMessage, null, body);
            }
        }

        public void SetFlag(MessageHeader m, MessageFlags flag, bool isSet)
        {
            /// TODO: Ensure current folder selected

            string command = m.id + " ";
            if (isSet)
            {
                command += "+";
            }
            else
            {
                command += "-";
            }
            command += "FLAGS (\\";

            switch (flag)
            {
                case MessageFlags.Answered:
                    command += "Answered";
                    break;
                case MessageFlags.Deleted:
                    command += "Deleted";
                    break;
                case MessageFlags.Draft:
                    command += "Draft";
                    break;
                case MessageFlags.Flagged:
                    command += "Flagged";
                    break;
                case MessageFlags.Recent:
                    command += "Recent";
                    break;
                case MessageFlags.Seen:
                    command += "Seen";
                    break;
            }

            command += ")";

            SendCommand("STORE", command, ProcessMessage, null, m.Folder);
        }

        public void ExpungeFolder()
        {
            SendCommand("EXPUNGE", "", UpdateStatus, null, currentFolder_);
        }

        public void SearchFolder(string searchText)
        {
            SendCommand("SEARCH", "NOT DELETED TEXT " + searchText, SearchResponse, null, currentFolder_);
        }

        public void SearchEnd()
        {
            currentFolder_.ViewMessages = currentFolder_.Messages;

            if (MessagesChanged != null)
            {
                MessagesChanged(this, new MessagesChangedEventArgs(currentFolder_, null));
            }
        }

        public void PollFolders()
        {
            foreach (var folder in AllFolders)
            {
                if (folder.CanHaveMessages)
                {
                    CheckUnseen(folder);
                }
            }
        }

        public void Shutdown()
        {
            // Try to shutdown cleanly for 10s

            for (int i = 0; i < 10; ++i)
            {
                if (!pendingRequests_.Any() && !pendingResponses_.Any())
                {
                    try
                    {
                        if (stream_ != null)
                        {
                            stream_.Close();
                            stream_ = null;
                        }

                        if (client_ != null)
                        {
                            client_.Close();
                            client_ = null;
                        }
                    }
                    catch (Exception)
                    {
                    }

                    state_ = ImapState.None;

                    break;
                }

                System.Threading.Thread.Sleep(1000);
            }
        }

        #endregion
    }
}
