using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mail
{
    public static class ImapData
    {
        public static string StripQuotes(string data)
        {
            if (data.StartsWith("\"") && data.EndsWith("\""))
            {
                return data.Substring(1, data.Length - 2);
            }
            else if (data == "NIL")
            {
                return "";
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

        public static string[] SplitToken(string token)
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

        public static bool SplitTokens(string data, IList<string> tokens)
        {
            bool lastIsComplete = false;
            int token = 0;
            while (true)
            {
                string tokenStr = NextToken(data, token, out token);

                if (tokenStr.Trim().Length != 0)
                {
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

        static string NextToken(string data, int start, out int end)
        {
            if (start < 0)
            {
                end = -1;
                return null;
            }

            StringBuilder output = new StringBuilder();
            int pos = start;
            int byteCounterStart = 0;
            char prevChar = '\0';

            Stack<char> toMatch = new Stack<char>();

            while (pos != data.Length)
            {
                char current = data[pos];

                char matchFor = '\0';
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
                            data.CopyTo(byteCounterStart, destination, 0, bytesLenLen);
                            int bytesLen = Int32.Parse(new string(destination));

                            if (bytesLen <= data.Length - pos - 3)
                            {
                                if (toMatch.Count == 1)
                                {
                                    int charsToRemove = bytesLenLen + 1;
                                    output.Remove(output.Length - charsToRemove, charsToRemove);

                                    string dataStr = data.Substring(pos + 3, bytesLen);
                                    output.Append('\"');
                                    output.Append(dataStr);
                                    output.Append('\"');
                                }
                                else
                                {
                                    // Add the string on, but don't remove the length
                                    // identifier, until we are a top level token.
                                    output.Append(data.Substring(pos, bytesLen + 3));
                                }

                                pos += bytesLen + 2;
                                current = '\0';
                            }
                            else
                            {
                                // Append all of the rest of input data, to return an incomplete token.
                                output.Append(data.Substring(pos));

                                end = -1;
                                return output.ToString();
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

                if (prevChar == '\\' && toMatch.Count == 1 && toMatch.First() == '"')
                {
                    // Handle backslashified char.
                    // I think that there we are really only handling \" quoted pair and maybe \\
                    // However, we don't want to do this substitution until we are at the last level 
                    // of splitting, otherwise the next time that we come in here we won't have
                    // backslashed quotes and they will be parsed wrongly.
                    output.Remove(output.Length - 1, 1);
                    prevChar = '\0';
                }
                else
                {
                    prevChar = current;


                    if (foundMatch)
                    {
                        char lastChar = toMatch.Pop();
                        if (toMatch.Count == 0)
                        {
                            end = pos + 1;

                            int matchedLen = pos - start;
                            if (lastChar != ' ' && current != '\0')
                            {
                                // If the closing token wasn't <space> then we want to include it.
                                output.Append(current);
                            }
                            return output.ToString();
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
                    output.Append(current);
                }
            }

            if (output.Length > 0)
            {
                end = -1;
            }
            else
            {
                end = pos;
            }

            return output.ToString();
        }
    }

    public enum TextEncoding
    {
        SevenBit,
        EightBit,
        Binary,
        QuotedPrintable,
        Base64,
        IetfToken,
        XToken
    }

    public static class EncodedText
    {
        public static TextEncoding BuildEncoding(string encoding)
        {
            if (encoding.Equals("7BIT", StringComparison.InvariantCultureIgnoreCase))
            {
                return TextEncoding.SevenBit;
            } else if (encoding.Equals("8BIT", StringComparison.InvariantCultureIgnoreCase))
            {
                return TextEncoding.EightBit;
            } else if (encoding.Equals("BINARY", StringComparison.InvariantCultureIgnoreCase))
            {
                return TextEncoding.Binary;
            } else if (encoding.Equals("QUOTED-PRINTABLE", StringComparison.InvariantCultureIgnoreCase))
            {
                return TextEncoding.QuotedPrintable;
            } else if (encoding.Equals("BASE64", StringComparison.InvariantCultureIgnoreCase))
            {
                return TextEncoding.Base64;
            } else if (encoding.Equals("IETF-TOKEN", StringComparison.InvariantCultureIgnoreCase))
            {
                return TextEncoding.IetfToken;
            }
            else if (encoding.Equals("X-TOKEN", StringComparison.InvariantCultureIgnoreCase))
            {
                return TextEncoding.XToken;
            }
            else
            {
                return TextEncoding.SevenBit;
            }
        }

        public static string DecodeWord(string input)
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
                    var restBytes = Encoding.UTF8.GetBytes(rest);
                    data = QuottedPrintableDecode(restBytes, true);
                }

                string res = Encoding.GetEncoding(charset).GetString(data);

                input = input.Substring(0, encodingStart) + res + input.Substring(encodingEnd, input.Length - encodingEnd);
            }

            return input;
        }

        public static byte[] QuottedPrintableDecode(byte[] input, bool isWord)
        {
            List<byte> res = new List<byte>();
            bool encoding = false;
            char[] encodeVal = new char[2];
            int encodingPos = 0;

            foreach (var c in input)
            {
                if (encoding)
                {
                    encodeVal[encodingPos] = (char)c;
                    ++encodingPos;

                    if (encodingPos == 2)
                    {
                        try
                        {
                            int charVal = Convert.ToInt32(new string(encodeVal), 16);
                            res.Add((byte)charVal);
                        }
                        catch
                        {
                        }

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
                        if (isWord)
                        {
                            res.Add(32);
                        }
                        else
                        {
                            res.Add(c);
                        }
                    }
                    else
                    {
                        res.Add(c);
                    }
                }
            }

            return res.ToArray();
        }
    }

    public static class TextProcessing
    {
        public static string CamelCase(string label)
        {
            string lower = label.Substring(1).ToLower();
            string first = label[0].ToString().ToUpper();

            return first + lower;
        }
    }
}
