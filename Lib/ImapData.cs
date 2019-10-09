using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMail.Core
{
    public static class ImapData
    {
        public static DateTime ParseDate(string value)
        {
            try
            {
                return DateTime.Parse(value);
            }
            catch (Exception)
            {
            }

            if (value.EndsWith(")"))
            {
                // strip off this trailing tz annotation.
                int newEndPos = value.LastIndexOf("(");
                value = value.Substring(0, newEndPos);

                try
                {
                    return DateTime.Parse(value);
                }
                catch (Exception)
                {
                }
            }

            DateTime invalid = new DateTime();

            return invalid;
        }

        public static string StripQuotes(string data, bool stripWhitespace)
        {
            if (data.StartsWith("\"") && data.EndsWith("\""))
            {
                string res = data.Substring(1, data.Length - 2);

                if (stripWhitespace)
                {
                    return res.Trim();
                }
                else
                {
                    return res;
                }
            }
            else if (data == "NIL")
            {
                return "";
            }
            else if (stripWhitespace)
            {
                return data.Trim();
            }
            else
            {
                return data;
            }
        }

        public static bool IsArray(string data)
        {
            return data[0] == '(' && data.Last() == ')';
        }

        /// <summary>
        /// Used to take a token that has already been extracted from the input and split it again.
        /// This implies that is has array characters around it.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string[] SplitToken(string token)
        {
            if (token == "NIL")
            {
                return null;
            }

            string splitStr = token.Substring(1, token.Length - 2);

            var data = System.Text.Encoding.ASCII.GetBytes(splitStr);
            List<byte[]> tokens = new List<byte[]>();

            SplitTokens(data, tokens);

            List<string> res = new List<string>();
            foreach (var t in tokens)
            {
                res.Add(System.Text.Encoding.ASCII.GetString(t));
            }

            return res.ToArray();
        }

        public static IList<byte[]> SplitToken(byte[] token)
        {
            string tokenStr = System.Text.Encoding.ASCII.GetString(token);
            if (tokenStr == "NIL")
            {
                return null;
            }


            byte[] dataToken = new byte[token.Length - 2];
            Array.Copy(token, 1, dataToken, 0, token.Length - 2);
            List<byte[]> tokens = new List<byte[]>();

            SplitTokens(dataToken, tokens);

            return tokens;
        }

        public static bool SplitTokens(byte[] data, IList<byte[]> tokens)
        {
            bool lastIsComplete = false;
            int token = 0;
            while (true)
            {
                var tokenStr = NextToken(data, token, out token);

                // Check for is string just whitespace
                if (tokenStr.Length != 0)
                {
                    string tokenT = System.Text.Encoding.ASCII.GetString(tokenStr);
                    if (string.IsNullOrWhiteSpace(tokenT))
                    {
                        int a = 0;
                    }



                    tokens.Add(tokenStr);
                }

                if (token == data.Length)
                {
                    lastIsComplete = true;
                    break;
                }

                if (token < 0)
                {
                    break;
                }
            }

            return lastIsComplete;
        }

        static byte[] NextToken(byte[] data, int start, out int end)
        {
            if (start < 0)
            {
                end = -1;
                return null;
            }

            List<byte> output = new List<byte>();
            int pos = start;
            int byteCounterStart = 0;
            var prevChar = '\0';

            Stack<char> toMatch = new Stack<char>();

            while (pos != data.Length)
            {
                var current = System.Text.Encoding.ASCII.GetChars(data, pos, 1)[0];

                var matchFor = '\0';
                if (toMatch.Any())
                {
                    matchFor = toMatch.Peek();
                }
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

                            Array.Copy(data, byteCounterStart, destination, 0, bytesLenLen);

                            int bytesLen = Int32.Parse(new string(destination));

                            if (bytesLen <= data.Length - pos - 3)
                            {
                                if (toMatch.Count == 1)
                                {
                                    int charsToRemove = bytesLenLen + 1;
                                    output.RemoveRange(output.Count() - charsToRemove, charsToRemove);

                                    var dataStr = new byte[bytesLen];
                                    Array.Copy(data, pos + 3, dataStr, 0, bytesLen);

                                    output.Add((byte)'\"');
                                    output = output.Concat(dataStr).ToList();
                                    output.Add((byte)'\"');
                                }
                                else
                                {
                                    // Add the string on, but don't remove the length
                                    // identifier, until we are a top level token.
                                    var subData = new byte[bytesLen + 3];
                                    Array.Copy(data, pos, subData, 0, bytesLen + 3);

                                    output = output.Concat(subData).ToList();
                                }

                                pos += bytesLen + 2;
                                current = '\0';
                            }
                            else
                            {
                                // Append all of the rest of input data, to return an incomplete token.
                                var subData = data.Skip(pos);
                                output = output.Concat(subData).ToList();

                                end = -1;
                                return output.ToArray();
                            }
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

                if (prevChar == '\\')
                {
                    if (toMatch.Count == 1 && toMatch.First() == '"')
                    {
                        // Handle backslashified char.
                        // I think that there we are really only handling \" quoted pair and maybe \\
                        // However, we don't want to do this substitution until we are at the last level 
                        // of splitting, otherwise the next time that we come in here we won't have
                        // backslashed quotes and they will be parsed wrongly.
                        output.RemoveRange(output.Count() - 1, 1);
                        prevChar = '\0';
                    }
                    else
                    {
                        // Don't do any processing on this current char

                        prevChar = current;
                    }
                }
                else
                {
                    prevChar = current;


                    if (foundMatch)
                    {
                        var lastChar = toMatch.Pop();
                        if (toMatch.Count == 0)
                        {
                            end = pos + 1;

                            int matchedLen = pos - start;
                            if (lastChar != ' ' && current != '\0')
                            {
                                // If the closing token wasn't <space> then we want to include it.
                                output.Add((byte)current);
                            }
                            return output.ToArray();
                        }
                    }
                    else if (current == '\"')
                    {
                        toMatch.Push(current);
                    }
                    else if (current == '[' ||
                             current == '(' ||
                             current == '<')
                    {
                        if (!toMatch.Contains('\"'))
                        {
                            toMatch.Push(current);
                        }
                    }
                    else if (current == '{')
                    {
                        if (!toMatch.Contains('\"'))
                        {
                            toMatch.Push(current);
                            byteCounterStart = pos + 1;
                        }
                    }
                    else if (!toMatch.Any())
                    {
                        if (current == '\r' || current == '\n' || current == ' ')
                        {
                            // Don't put this in as the start char
                            current = '\0';
                        }
                        else
                        {
                            // First character and wasn't another token character
                            toMatch.Push(' ');
                        }
                    }
                }


                ++pos;

                if (current != '\0')
                {
                    output.Add((byte)current);
                }
            }

            if (output.Count() > 0)
            {
                end = -1;
            }
            else
            {
                end = pos;
            }

            return output.ToArray();
        }
    }
}
