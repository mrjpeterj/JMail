using System;
using System.Collections.Generic;
using System.Text;

namespace JMail.Core
{
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
}
