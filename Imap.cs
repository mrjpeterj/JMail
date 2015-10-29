using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;

using System.Net;
using System.Net.Sockets;

namespace JMail
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
        internal delegate void ResponseHandler(ImapRequest request, IList<string> responseData, object data);

        string id_;
        string commandName_;
        string args_;
        DateTime requestStart_;
        ResponseHandler response_;

        object data_;

        public string Key { get { return id_; } }
        public string Command { get { return commandName_; } }
        public string Args { get { return args_; } }
        public DateTime RequestStart { get { return requestStart_; } }

        public ImapRequest(string id, string commandName, string args, ResponseHandler handler, object data)
        {
            id_ = id;
            commandName_ = commandName;
            args_ = args;
            requestStart_ = DateTime.Now;
            response_ = handler;
            data_ = data;
        }

        public void Process(IList<string> resultData)
        {
            response_(this, resultData, data_);
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
        private List<string> currentCommand_;
        private bool lastTokenIsComplete_;

        private List<Folder> allFolders_;
        private List<Folder> folders_;

        private Folder currentFolder_;

        private bool supportsIdle_;
        private bool idling_;

        private System.Timers.Timer folderCheckTimer_;

        public Imap(AccountInfo account)
        {
            account_ = account;
            state_ = ImapState.None;
            pendingResponses_ = new Dictionary<string, ImapRequest>();
            pendingRequests_ = new Queue<ImapRequest>();

            currentCommand_ = new List<string>();
            lastTokenIsComplete_ = true;

            allFolders_ = new List<Folder>();
            folders_ = new List<Folder>();

            supportsIdle_ = false;

            folderCheckTimer_ = new System.Timers.Timer(30 * 1000);
            folderCheckTimer_.Elapsed += CheckCurrent;

            incoming_ = new byte[8 * 1024];

            Connect();
        }

        void Connect()
        {
            // On reconnect, we might have requests in the pending queue that prevent the connect from happen.
            // We need to find a solution for this.

            System.Diagnostics.Debug.WriteLine("Attempting connect to " + account_.Host);

            try
            {
                client_ = new TcpClient(account_.Host, account_.Port);
            } catch (SocketException e)
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
                int bytesRead = stream_.EndRead(res);


                if (bytesRead > 0)
                {
                    string response = encoder_.GetString(incoming_, 0, bytesRead);

                    ProcessResponse(response);
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

        void ProcessResponse(string responseText)
        {
            System.Diagnostics.Debug.WriteLine(">>>>>>>> " + account_.Host + " <<<<<<<");
            System.Diagnostics.Debug.Write(responseText);
            System.Diagnostics.Debug.WriteLine("========");

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
                            int a = 0;
                        }

                        request.Process(currentCommand_);

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
                        StartUp();
                        currentCommand_.Clear();
                        return;
                    }                  
                    else if (currentCommand_.Any() || response == "*")
                    {
                        currentCommand_.Add(response);
                    }
                }
                else
                {
                    currentCommand_.Add(response);
                }
            }

            if (!lastIsComplete)
            {
                // Remember whether the last token in the split was a complete one,
                // so that we know whether to append to it in the next round.

                lastTokenIsComplete_ = lastIsComplete;
            }
            else if (idling_ && currentCommand_.Any())
            {
                // IDLE doesn't have a completion command until it kicks you off the end.
                UpdateStatus(null, currentCommand_, null);
            }

            if (idling_ && pendingResponses_.Any())
            {
                // The IDLE command can end up expecting a response, when it doesn't get one.
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

        void SendCommand(string command, string args, ImapRequest.ResponseHandler handler, object data = null)
        {
            if (folderCheckTimer_.Enabled)
            {
                // We are off to do some other requests, so don't run the folder updater.
                folderCheckTimer_.Stop();
            }

            if (idling_)
            {
                idling_ = false;

                // Need to terminate the idle first.
                string endIdle = "DONE\r\n";

                byte[] bytes = encoder_.GetBytes(endIdle);

                stream_.Write(bytes, 0, bytes.Length);
                stream_.Flush();
            }


            string commandId = NextCommand();

            var request = new ImapRequest(commandId, command, args, handler, data);
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

            stream_.Write(bytes, 0, bytes.Length);
            stream_.Flush();
        }

        void ProcessPending()
        {
            if (!pendingRequests_.Any())
            {
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
        }

        void StartUp()
        {
            Caps();
        }

        void Caps()
        {
            SendCommand("CAPABILITY", "", HandleCaps);
        }

        void HandleCaps(ImapRequest request, IEnumerable<string> resultData, object data)
        {
            if (resultData.Contains("IDLE"))
            {
                supportsIdle_ = true;
            }

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

        void HandleTLS(ImapRequest request, IEnumerable<string> responseData, object data)
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
            SendCommand("AUTHENTICATE", "PLAIN", HandleAuth);

            string response = '\0' + account_.Username + '\0' + account_.GetPassword();

            string encResponse = Convert.ToBase64String(Encoding.UTF8.GetBytes(response));

            SendRawResponse(encResponse);

            //SendCommand("LOGIN", account_.Username + " " + account_.GetPassword(), HandleLogin);
        }

        void HandleAuth(ImapRequest request, IEnumerable<string> resultData, object data)
        {
            state_ = ImapState.LoggedIn;
            ListFolders();
        }

        void HandleLogin(ImapRequest request, IEnumerable<string> resultData, object data)
        {
            state_ = ImapState.LoggedIn;
            ListFolders();
        }

        void ListFolders()
        {
            folders_.Clear();
            SendCommand("LSUB", "\"\" \"*\"", ListedFolder);
        }

        void ListedFolder(ImapRequest request, IEnumerable<string> responseData, object data)
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

        void SelectedFolder(ImapRequest request, IEnumerable<string> responseData, object data)
        {
            ListMessages(request, responseData, data as Folder);

            SendCommand("UID SEARCH", "UNDELETED", AvailableMessages);       
        }

        void RenamedFolder(ImapRequest request, IEnumerable<string> responseData, object data)
        {
            var response = responseData.ToList();
            if (response[1] == "OK")
            {
                SubscribeFolder(data.ToString());
            }
        }

        void SubscribedFolder(ImapRequest request, IEnumerable<string> responseData, object data)
        {
            ListFolders();
        }

        // Returns whether it thinks that the message list should be updated.
        bool ListMessages(ImapRequest request, IEnumerable<string> responseData, Folder f)
        {
            Folder folder = f;
            if (folder == null)
            {
                folder = currentFolder_;
            }

            bool updated = false;

            if (folder != null)
            {
                string lastValue = null;
                bool subProcessNext = false;
                MessageHeader msg = null;

                int prevExists = folder.Exists;
                folder.Recent = 0;

                bool refreshStatus = false;

                foreach (var response in responseData)
                {
                    if (subProcessNext)
                    {
                        string[] responseInfo = ImapData.SplitToken(response);

                        subProcessNext = false;
                    }
                    else if (msg != null)
                    {
                        bool updateFolder = ExtractValues(-1, ref msg, null, response);
                        if (updateFolder && !refreshStatus)
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
                        MessagesChanged(this, new MessagesChangedEventArgs(folder));
                    }
                }

                if (folder.Exists != prevExists || folder.Recent > 0)
                {
                    updated = true;
                }
            }

            return updated;
        }

        void CheckUnseen(Folder f)
        {
            SendCommand("STATUS", "\"" + f.FullName + "\"" + " (MESSAGES UNSEEN RECENT)", UnreadCount);
        }

        void UnreadCount(ImapRequest request, IList<string> responseData, object data)
        {
            string folderName =  ImapData.StripQuotes(responseData[2]);

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
                    MessagesChanged(this, new MessagesChangedEventArgs(folder));
                }
            }            
        }

        void UpdateStatus(ImapRequest request, IEnumerable<string> responseData, object data)
        {
            if (responseData.Any())
            {
                if (ListMessages(request, responseData, data as Folder))
                {
                    SendCommand("UID SEARCH", "UNDELETED", AvailableMessages);
                }
            }
        }

        void AvailableMessages(ImapRequest request, IEnumerable<string> responseData, object data)
        {
            // Each line is of the form:
            // * SEARCH <list of ids>

            if (currentFolder_ == null)
            {
                // Got out of sync somewhere.

                // TODO: This can happen when we expunge a folder on leaving it, which
                // then wrongly causes a refresh on the folder, even though we no longer care
                // about it.

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
                currentFolder_.Messages.Remove(currentFolder_.MessageByUID(uid));
            }

            if (newIds.Any())
            {
                FetchMessage(newIds, false);
            }

            if (existingIds.Any())
            {
                FetchMessage(existingIds, true);
            }

            if (MessagesChanged != null)
            {
                MessagesChanged(this, new MessagesChangedEventArgs(currentFolder_));
            }
        }

        void FetchMessage(IList<int> ids, bool flagsOnly)
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

                    SendCommand("UID FETCH", idList + " " + command, ProcessMessage);

                    idList = "";
                }
            }

            if (idList.Length > 0)
            {
                SendCommand("UID FETCH", idList + " " + command, ProcessMessage);
            }
        }

        void ProcessMessage(ImapRequest request, IEnumerable<string> responseData, object data)
        {
            // Format of this is:
            // * {id} FETCH (<field> <field data> <field> <field data> ......

            bool isId = false;
            bool isResponse = false;

            int msgId = -1;
            BodyPart body = data as BodyPart;

            Folder refreshFolder = null;

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
                    msgId = Int32.Parse(response);
                    
                    isId = false;
                }
                else if (isResponse)
                {
                    MessageHeader msg = null;
                    bool updateFolder = ExtractValues(msgId, ref msg, body, response);
                    if (updateFolder && refreshFolder == null)
                    {
                        refreshFolder = msg.Folder;
                    }
                    
                    isResponse = false;
                }
            }

            if (refreshFolder != null)
            {
                CheckUnseen(refreshFolder);

                if (MessagesChanged != null)
                {
                    MessagesChanged(this, new MessagesChangedEventArgs(refreshFolder));
                }
            }
        }

        bool ExtractValues(int msgId, ref MessageHeader msgHdr, BodyPart body, string data)
        {
            string[] values = ImapData.SplitToken(data);
            Dictionary<string, string> dictValues = new Dictionary<string, string>();
            for (int i = 0; i < values.Length; i = i + 2)
            {
                dictValues.Add(values[i], values[i + 1]);
            }

            string uidStr;
            int uid = -1;
            if (dictValues.TryGetValue("UID", out uidStr))
            {
                Int32.TryParse(uidStr, out uid);
            }

            if (uid >= 0)
            {
                msgHdr = currentFolder_.MessageByUID(uid);
            }
            else if (msgId >= 0)
            {
                msgHdr = currentFolder_.MessageByID(msgId);
            }

            bool updateFolder = false;

            if (msgHdr == null)
            {
                msgHdr = new MessageHeader(uid, currentFolder_);
                currentFolder_.Messages.Add(msgHdr);

                msgHdr.id = msgId;

                updateFolder = true;
            }
            
            foreach (var val in dictValues)
            {
                string key = val.Key;
                string value = val.Value;

                if (key == "FLAGS")
                {
                    ExtractFlags(msgHdr, value);

                    // If we have flags values (well more like, if they have changed)
                    // the we need to update the status of the Folder.
                    updateFolder = true;
                }
                else if (key.StartsWith("BODY["))
                {
                    ExtractBodyInfo(msgHdr, body, value);
                }
                else if (key == "INTERNALDATE")
                {
                    ExtractDate(msgHdr, value);
                }
                else if (key == "RFC822.SIZE")
                {
                    ExtractSingle(msgHdr, value, "SIZE");
                }
                else if (key == "ENVELOPE")
                {
                    ParseEnvelope(msgHdr, value);
                }
                else if (key == "BODYSTRUCTURE")
                {
                    ParseBodyStructure(msgHdr, value, "");
                }
            }

            return updateFolder;
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
            msg.SetValue("Subject", EncodedText.DecodeWord(subject));
            msg.SetValue("In-Reply-To", inReplyTo);
            msg.SetValue("Message-Id", msgId);

            string[] from = ImapData.SplitToken(envItems[2]);
            string[] sender = ImapData.SplitToken(envItems[3]);
            string[] replyTo = ImapData.SplitToken(envItems[4]);
            string[] to = ImapData.SplitToken(envItems[5]);
            string[] cc = ImapData.SplitToken(envItems[6]);
            string[] bcc = ImapData.SplitToken(envItems[7]);

            var senderAddr = AddressBuilder(ImapData.SplitToken(sender[0]));

            msg.From = AddressBuilder(ImapData.SplitToken(from[0]));
            msg.ReplyTo = AddressBuilder(ImapData.SplitToken(replyTo[0]));

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
                    if (!ImapData.IsArray(dataPieces[p])) {
                        typePos = p;
                        break;
                    }
                }

                // Analyze multi-part type                
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

                if (ImapData.StripQuotes(dataPieces[0]) == "TEXT")
                {
                    string textType = ImapData.StripQuotes(dataPieces[1]);

                    if (textType.Equals("HTML", StringComparison.InvariantCultureIgnoreCase))
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

        void ExtractBodyInfo(MessageHeader msg, BodyPart body, string data)
        {
            var bytes = encoder_.GetBytes(ImapData.StripQuotes(data));

            body.SetContent(bytes);
        }

        System.Net.Mail.MailAddress AddressBuilder(string[] addressParts)
        {
            string address = ImapData.StripQuotes(addressParts[2]) + "@" + ImapData.StripQuotes(addressParts[3]);
            string displayName = EncodedText.DecodeWord(ImapData.StripQuotes(addressParts[0]));

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

        void IdleComplete(ImapRequest req, IEnumerable<string> data, object state)
        {
            idling_ = false;
        }

        void CheckCurrent(object state, EventArgs e)
        {
            if (currentFolder_ != null)
            {
                if (supportsIdle_ && !idling_)
                {
                    // Check the current status first before going into
                    // idle otherwise things that happened since the
                    // previous poll won't get noticed.
                    SendCommand("NOOP", "", UpdateStatus, currentFolder_);

                    SendCommand("IDLE", "", IdleComplete, currentFolder_);
                    idling_ = true;
                }
                else
                {
                    SendCommand("NOOP", "", UpdateStatus, currentFolder_);

                    // We have to poll in this case, so start the timer again.
                    folderCheckTimer_.Start();
                }
            }
        }

        #region IAccount Members

        public event EventHandler FoldersChanged;
        public event EventHandler<MessagesChangedEventArgs> MessagesChanged;

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
            SendCommand("RENAME", oldName + " " + newName, RenamedFolder, newName);
        }

        public void SubscribeFolder(string folderName)
        {
            SendCommand("SUBSCRIBE", folderName, SubscribedFolder);
        }

        public void FetchMessage(MessageHeader m, BodyPart body)
        {
            if (body == null)
            {
                SendCommand("UID FETCH", m.Uid + " (FLAGS BODY.PEEK[])", ProcessMessage, body);
            }
            else if (body == m.Body)
            {
                SendCommand("UID FETCH", m.Uid + " (FLAGS BODY.PEEK[" + body.PartNumber + "])", ProcessMessage, body);
            }
            else
            {
                SendCommand("UID FETCH", m.Uid + " (BODY.PEEK[" + body.PartNumber + "])", ProcessMessage, body);
            }
        }

        public void SetFlag(MessageHeader m, MessageFlags flag, bool isSet)
        {
            /// TODO: Ensure current folder selected

            string command = m.id + " ";
            if (isSet)
            {
                command += "+";
            } else {
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

            SendCommand("STORE", command, ProcessMessage);
        }

        public void ExpungeFolder()
        {
            SendCommand("EXPUNGE", "", UpdateStatus, currentFolder_);
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
                        stream_.Close();
                        client_.Close();
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
