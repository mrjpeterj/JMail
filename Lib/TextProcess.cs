using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMail.Core
{
    internal static class EncodedText
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

        // Encoded text is of the form:
        //
        // =?charset?encoding?encoded_text?=
        public static string DecodeWord(string input)
        {
            while (true)
            {
                int encodingStart = input.IndexOf("=?");
                if (encodingStart < 0)
                {
                    break;
                }

                int charsetEnd = input.IndexOf("?", encodingStart + 2);
                if (charsetEnd < 0)
                {
                    break;
                }

                int encEnd = input.IndexOf("?", charsetEnd + 1);
                if (encEnd < 0)
                {
                    break;
                }

                int encodingEnd = input.IndexOf("?=", encEnd + 1);
                if (encodingEnd < 0)
                {
                    break;
                }
                else
                {
                    // Move it to point after the close of the encoding tag
                    encodingEnd += 2;
                }

                string charset = input.Substring(encodingStart + 2, charsetEnd - encodingStart - 2);
                string encoding = input.Substring(charsetEnd + 1, encEnd - charsetEnd - 1);
                string rest = input.Substring(encEnd + 1, encodingEnd - encEnd - 3);
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


                // Now if there is more encoding to come and it starts straight away, then it should be separated by whitespace.
                if (input.Length > encodingEnd)
                {
                    if (Char.IsWhiteSpace(input, encodingEnd))
                    {
                        ++encodingEnd;
                    }
                }

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
                            // We know that he encoding of the input is ANSi, so we can do normal char maths here.
                            // Using the Convert.ToInt32 is far too slow.

                            int c1 = 0;
                            if (Char.IsNumber(encodeVal[0]))
                            {
                                c1 = encodeVal[0] - '0';
                            }
                            else if (Char.IsLetter(encodeVal[0]))
                            {
                                c1 = 10 + (encodeVal[0] - 'A');
                            }

                            int c2 = 0;
                            if (Char.IsNumber(encodeVal[1]))
                            {
                                c2 = encodeVal[1] - '0';
                            }
                            else if (Char.IsLetter(encodeVal[1]))
                            {
                                c2 = 10 + (encodeVal[1] - 'A');
                            }


                            int charVal = c1 * 16 + c2;

                            if (charVal > 0)
                            {
                                res.Add((byte)charVal);
                            }
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
}
