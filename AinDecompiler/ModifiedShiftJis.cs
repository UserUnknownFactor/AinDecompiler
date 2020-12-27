using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace AinDecompiler
{
    class ModifiedShiftJis : Encoding
    {
        class CharExistsInShiftJisCache
        {
            static Encoding shiftJis = Encoding.GetEncoding("shift_jis");
            Dictionary<char, bool> dic = new Dictionary<char, bool>();

            public bool CharExists(char c)
            {
                if (dic.ContainsKey(c))
                {
                    return dic[c];
                }
                else
                {
                    var bytes = shiftJis.GetBytes(c.ToString());
                    if (bytes.Length == 1 && bytes[0] < 128 && bytes[0] != c)
                    {
                        dic[c] = false;
                        return false;
                    }
                    dic[c] = true;
                    return true;
                }
            }
        }

        static CharExistsInShiftJisCache charExistsCache = new CharExistsInShiftJisCache();

        HashSet<char> set = new HashSet<char>();

        Encoding shiftJis = Encoding.GetEncoding("shift_jis");

        static bool CharExistsInShiftJis(char c)
        {
            return charExistsCache.CharExists(c);
        }

        public override int GetByteCount(char[] chars, int index, int count)
        {
            int bI = 0;
            for (int cI = 0; cI < count; cI++)
            {
                char c = chars[cI + index];
                if (c >= 0xC0 && c <= 0xFF)
                {
                    if (c == 0xD7 || c == 0xF7)  //multiply and divide symbols are two-byte characters in Shift-JIS
                    {
                        bI++;
                    }
                    bI++;
                }
                else
                {
                    if (c >= 0x100 && c < 0x700)
                    {
                        if (CharExistsInShiftJis(c))
                        {
                            bI += shiftJis.GetByteCount(chars, cI + index, 1);
                        }
                        else
                        {
                            bI += 2;
                        }
                    }
                    else if (c >= 0x1E00 && c < 0x1F00) //vietnamese
                    {
                        if (CharExistsInShiftJis(c))
                        {
                            bI += shiftJis.GetByteCount(chars, cI + index, 1);
                        }
                        else
                        {
                            bI += 2;
                        }
                    }
                    else
                    {
                        bI += shiftJis.GetByteCount(chars, cI + index, 1);
                    }
                }
            }
            return bI;
        }

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
        {
            int bI = 0;
            for (int cI = 0; cI < charCount; cI++)
            {
                char c = chars[cI + charIndex];
                if ((c >= 0xBF && c <= 0xFF) || c == 0xA1)
                {
                    //multiplication and division sign are two-byte Shift-JIS characters
                    if (c == 0xD7 || c == 0xF7)
                    {
                        bI += shiftJis.GetBytes(chars, cI + charIndex, 1, bytes, bI + byteIndex);
                    }
                    else
                    {
                        if (c == 0xA1) c = (char)0xD7;  //inverted exclamation becomes multiply
                        if (c == 0xBF) c = (char)0xF7;  //inverted question mark becomes divide

                        bytes[bI + byteIndex] = (byte)(c - 0x20);
                        bI++;
                    }
                }
                else
                {
                    bool isShiftJis = true;

                    if (c >= 0x100 && c < 0x700 && !CharExistsInShiftJis(c))
                    {
                        int value = c - 0x100;
                        int b1 = value / 0xC0 + 0xF0;
                        int b2 = value % 0xC0 + 0x40;

                        bytes[bI++ + byteIndex] = (byte)b1;
                        bytes[bI++ + byteIndex] = (byte)b2;
                    }
                    else if (c >= 0x1E00 && c < 0x1F00 && !CharExistsInShiftJis(c)) //vietnamese
                    {
                        int value = c - 0x1E00 + 0x600;
                        int b1 = value / 0xC0 + 0xF0;
                        int b2 = value % 0xC0 + 0x40;

                        bytes[bI++ + byteIndex] = (byte)b1;
                        bytes[bI++ + byteIndex] = (byte)b2;
                    }
                    else
                    {
                        bI += shiftJis.GetBytes(chars, cI + charIndex, 1, bytes, bI + byteIndex);
                    }
                }
            }
            return bI;
        }

        public override int GetCharCount(byte[] bytes, int index, int count)
        {
            //use Shift-JIS character count because halfwidth characters still work for this check
            //also invalid 2-byte shift jis sequences count as one character, so it works.
            return shiftJis.GetCharCount(bytes, index, count);
        }

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
        {
            int charCount = shiftJis.GetChars(bytes, byteIndex, byteCount, chars, charIndex);
            int cI = 0;
            for (int bI = 0; bI < byteCount; bI++)
            {
                byte b = bytes[byteIndex + bI];
                if ((b >= 0x80 && b < 0xA0) || (b >= 0xE0 && b <= 0xFF))
                {
                    bI++;
                    if (b >= 0xF0 && b <= 0xF9 && bI < byteCount)
                    {
                        int b1 = b;
                        int b2 = bytes[byteIndex + bI];
                        int value = (b1 - 0xF0) * 0xC0 + (b2 - 0x40) + 0x100;
                        if (value >= 0x700)
                        {
                            value += (0x1E00 - 0x700); //vietnamese
                        }
                        chars[cI] = (char)value;
                    }
                }
                else if (b >= 0xA0 && b < 0xE0)
                {
                    char c = (char)(b + 0x20);
                    if (c == 0xD7) c = (char)0xA1;
                    if (c == 0xF7) c = (char)0xBF;
                    chars[cI] = c;
                }
                else
                {

                }
                cI++;
            }
            return charCount;
        }

        public override int GetMaxByteCount(int charCount)
        {
            return shiftJis.GetMaxByteCount(charCount);
        }

        public override int GetMaxCharCount(int byteCount)
        {
            return shiftJis.GetMaxCharCount(byteCount);
        }
    }
}
