using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

namespace AinDecompiler
{
    class ModifiedShiftJis2 : Encoding
    {
        private static UInt16[] MapUnicodeToBytes;
        private static char[] MapBytesToUnicode;

        private static GCHandle handle1, handle2;

        private static unsafe ushort* unicodeToBytes = null;
        private static unsafe char* bytesToUnicode = null;

        static ModifiedShiftJis2()
        {
            MapUnicodeToBytes = GetMapUnicodeToBytes();
            MapBytesToUnicode = GetMapBytesToUnicode();

            unsafe
            {
                handle1 = GCHandle.Alloc(MapUnicodeToBytes, GCHandleType.Pinned);
                unicodeToBytes = (ushort*)handle1.AddrOfPinnedObject();
                handle2 = GCHandle.Alloc(MapBytesToUnicode, GCHandleType.Pinned);
                bytesToUnicode = (char*)handle2.AddrOfPinnedObject();
            }
        }

        private static ushort[] GetMapUnicodeToBytes()
        {
            KeyValuePair<ushort[], char[]> pair = GetEncodingTables();
            return pair.Key;
        }

        private static char[] GetMapBytesToUnicode()
        {
            KeyValuePair<ushort[], char[]> pair = GetEncodingTables();
            return pair.Value;
        }

        static KeyValuePair<ushort[], char[]> _encodingTables;
        private static KeyValuePair<ushort[], char[]> GetEncodingTables()
        {
            if (_encodingTables.Key != null && _encodingTables.Value != null)
            {
                return _encodingTables;
            }

            char[] bytesToChar = new char[65536];
            ushort[] charToBytes = new ushort[65536];

            //first do Shift-JIS
            var shiftJis = Encoding.GetEncoding("shift_jis", EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);

            byte[] bytes = new byte[2];
            byte[] bytes2 = new byte[2];
            char[] chars = new char[2];

            for (int b1 = 0; b1 <= 0x7F; b1++)
            {
                for (int b2 = 0; b2 < 0x100; b2++)
                {
                    int b = b1 + b2 * 0x100;
                    bytesToChar[b] = (char)b1;
                    charToBytes[b1] = (ushort)b1;
                }
            }
            for (int b1 = 0xA1; b1 <= 0xDF; b1++)
            {
                char c = (char)(b1 + 0xFF61 - 0xA1);
                for (int b2 = 0; b2 < 0x100; b2++)
                {
                    int b = b1 + b2 * 0x100;
                    bytesToChar[b] = c;
                    charToBytes[c] = (ushort)b1;
                }
            }
            for (int b1 = 0x81; b1 < 0xFC; b1++)
            {
                if (
                    ((b1 >= 0x81 && b1 <= 0x9F) || (b1 >= 0xE0 && b1 <= 0xFC)) &&
                    !(b1 == 0x85 || b1 == 0x86 || b1 == 0xEB || b1 == 0xEC || (b1 >= 0xEF && b1 <= 0xF9))
                    )
                {
                    for (int b2 = 0x40; b2 <= 0xFC; b2++)
                    {
                        if (b2 != 0x7F)
                        {
                            ushort b = (ushort)(b1 + b2 * 0x100);
                            bytes[0] = (byte)b1;
                            bytes[1] = (byte)b2;
                            shiftJis.GetChars(bytes, 0, 2, chars, 0);
                            char c = chars[0];

                            if (c != '?')
                            {
                                bytesToChar[b] = c;
                                charToBytes[c] = b;
                            }
                        }
                    }
                }
            }

            //next do the extended characters
            for (int c = 0xA0; c < 0x500; c++)
            {
                int c2 = c - 0xA0;
                int b1 = c2 / 255 + 0xEF;
                int b2 = (c2 % 255) + 1;
                ushort b = (ushort)(b1 + b2 * 0x100);

                if (bytesToChar[b] == 0)
                {
                    bytesToChar[b] = (char)c;
                }
                if (charToBytes[c] == 0)
                {
                    charToBytes[c] = b;
                }
            }
            for (int c = 0x1E00; c < 0x2100; c++)
            {
                int c2 = c - 0x1E00 + (0x500 - 0xA0);
                int b1 = c2 / 255 + 0xEF;
                int b2 = (c2 % 255) + 1;
                ushort b = (ushort)(b1 + b2 * 0x100);

                if (bytesToChar[b] == 0)
                {
                    bytesToChar[b] = (char)c;
                }
                if (charToBytes[c] == 0)
                {
                    charToBytes[c] = b;
                }
            }
            for (int c = 0x2600; c < 0x2800; c++)
            {
                int c2 = c - 0x2600 + (0x500 - 0xA0 + 0x2100 - 0x1E00);
                int b1 = c2 / 255 + 0xEF;
                int b2 = (c2 % 255) + 1;
                ushort b = (ushort)(b1 + b2 * 0x100);

                if (bytesToChar[b] == 0)
                {
                    bytesToChar[b] = (char)c;
                }
                if (charToBytes[c] == 0)
                {
                    charToBytes[c] = b;
                }
            }

            _encodingTables = new KeyValuePair<ushort[], char[]>(charToBytes, bytesToChar);
            return _encodingTables;
        }

        public static ModifiedShiftJis2 GetEncoding()
        {
            return GetEncoding(EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
        }

        public static ModifiedShiftJis2 GetEncoding(EncoderFallback encoderFallback, DecoderFallback decoderFallback)
        {
#if NET45
            //In .NET framework 4.5, DecoderFallback and EncoderFallback properties can not be set
            return new ModifiedShiftJis2(encoderFallback, decoderFallback);
#else
            var encoding = (ModifiedShiftJis2)(new ModifiedShiftJis2().Clone());
            encoding.DecoderFallback = decoderFallback;
            encoding.EncoderFallback = encoderFallback;
            return encoding;
#endif
        }

        private ModifiedShiftJis2()
            : base(932)
        {

        }
#if NET45
        private ModifiedShiftJis2(EncoderFallback encoderFallback, DecoderFallback decoderFallback) : base(932, encoderFallback, decoderFallback)
        {

        }
#endif

        private EncoderFallbackBuffer encoderFallbackBuffer = null;
        private DecoderFallbackBuffer decoderFallbackBuffer = null;

        public override int GetByteCount(char[] chars, int index, int count)
        {
            if (count == 0)
            {
                return 0;
            }
            if (chars == null)
            {
                throw new ArgumentNullException("chars");
            }
            if (index < 0 || index >= chars.Length)
            {
                throw new ArgumentOutOfRangeException("index");
            }
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count");
            }
            if (index + count > chars.Length)
            {
                throw new ArgumentOutOfRangeException("count");
            }

            unsafe
            {
                fixed (char* c = chars)
                {
                    return GetByteCount(c, count);
                }
            }
        }

        public unsafe override int GetByteCount(char* chars, int count)
        {
            int byteCount = 0;
            char* charLimit = chars + count;
            char* charPtr = chars;
            while (charPtr < charLimit)
            {
                char c = *charPtr;
                charPtr++;
                int value = unicodeToBytes[c];
                if (!(value == 0 && c != 0))
                {
                    byteCount++;
                    if (value >= 256)
                    {
                        byteCount++;
                    }
                }
                else
                {
                    int charIndex = (int)(charPtr - chars);
                    byteCount += DoEncoderFallbackForByteCount(c, charIndex);
                }
            }
            return byteCount;
        }

        public override int GetByteCount(string s)
        {
            if (s == null) { throw new ArgumentNullException("s"); }
            if (s.Length == 0) { return 0; }
            unsafe
            {
                fixed (char* chars = s)
                {
                    return GetByteCount(chars, s.Length);
                }
            }
        }

        unsafe private int DoEncoderFallbackForByteCount(char c, int charIndex)
        {
            int byteCount = 0;
            int value;
            if (encoderFallbackBuffer == null)
            {
                encoderFallbackBuffer = this.EncoderFallback.CreateFallbackBuffer();
            }
            encoderFallbackBuffer.Fallback(c, charIndex);

            while (encoderFallbackBuffer.Remaining > 0)
            {
                c = encoderFallbackBuffer.GetNextChar();
                if (c == 0)
                {
                    break;
                }
                value = unicodeToBytes[c];
                if (value != 0)
                {
                    byteCount++;
                    if (value >= 256)
                    {
                        byteCount++;
                    }
                }
                else
                {
                    //would output FFFF
                    byteCount += 2;
                }
            }
            return byteCount;
        }

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
        {
            if (charCount == 0)
            {
                return 0;
            }
            if (chars == null)
            {
                throw new ArgumentNullException("chars");
            }
            if (bytes == null)
            {
                throw new ArgumentNullException("bytes");
            }
            if (charCount < 0)
            {
                throw new ArgumentOutOfRangeException("charCount");
            }
            if (charIndex < 0 || charIndex >= chars.Length)
            {
                throw new ArgumentOutOfRangeException("charIndex");
            }
            if (charIndex + charCount > chars.Length)
            {
                throw new ArgumentOutOfRangeException("charCount");
            }
            if (byteIndex < 0 || byteIndex >= bytes.Length)
            {
                throw new ArgumentOutOfRangeException("byteIndex");
            }

            int maxBytes = bytes.Length - byteIndex;

            unsafe
            {
                fixed (char* charsPtr = chars)
                {
                    fixed (byte* bytesPtr = bytes)
                    {
                        return GetBytes(charsPtr + charIndex, charCount, bytesPtr + byteIndex, maxBytes);
                    }
                }
            }
        }

        public unsafe override int GetBytes(char* chars, int charCount, byte* bytes, int byteCount)
        {
            char* charPtr = chars;
            char* limit = chars + charCount;

            byte* bytePtr = bytes;
            byte* byteLimit = bytes + byteCount;

            while (charPtr < limit && bytePtr < byteLimit)
            {
                char c = *charPtr;
                charPtr++;
                int value = unicodeToBytes[c];
                if (!(value == 0 && c != 0))
                {
                    *bytePtr = (byte)(value & 0xFF);
                    bytePtr++;
                    if (value >= 256)
                    {
                        if (bytePtr < byteLimit)
                        {
                            *bytePtr = (byte)(value >> 8);
                            bytePtr++;
                        }
                        else
                        {
                            throw new IndexOutOfRangeException("Not enough space in bytes to hold encoded characters");
                        }
                    }
                }
                else
                {
                    int charIndex = (int)(charPtr - chars);
                    bytePtr = DoEncoderFallbackForGetBytes(bytePtr, byteLimit, c, charIndex);
                }
            }
            if (charPtr < limit)
            {
                throw new IndexOutOfRangeException("Not enough space in bytes to hold encoded characters");
            }
            return (int)(bytePtr - bytes);
        }

        unsafe private byte* DoEncoderFallbackForGetBytes(byte* bytePtr, byte* byteLimit, char c, int charIndex)
        {
            //do the fallback stuff
            if (this.encoderFallbackBuffer != null)
            {
                this.encoderFallbackBuffer = this.EncoderFallback.CreateFallbackBuffer();
            }
            encoderFallbackBuffer.Fallback(c, charIndex);

            while (encoderFallbackBuffer.Remaining > 0)
            {
                char c2 = encoderFallbackBuffer.GetNextChar();
                if (c2 == 0)
                {
                    break;
                }
                int value2 = unicodeToBytes[c2];
                if (value2 > 0)
                {
                    if (bytePtr < byteLimit)
                    {
                        *bytePtr = (byte)(value2 & 0xFF);
                        bytePtr++;
                    }
                    else
                    {
                        throw new IndexOutOfRangeException("Not enough space in bytes to hold encoded characters");
                    }
                    if (value2 > 256)
                    {
                        if (bytePtr < byteLimit)
                        {
                            *bytePtr = (byte)(value2 >> 8);
                            bytePtr++;
                        }
                        else
                        {
                            throw new IndexOutOfRangeException("Not enough space in bytes to hold encoded characters");
                        }
                    }
                }
                else
                {
                    if (bytePtr + 1 < byteLimit)
                    {
                        *bytePtr = (byte)0xFF;
                        bytePtr++;
                        *bytePtr = (byte)0xFF;
                        bytePtr++;
                    }
                }
            }
            return bytePtr;
        }

        public override byte[] GetBytes(char[] chars, int index, int count)
        {
            if (count == 0)
            {
                return new byte[0];
            }
            if (chars == null)
            {
                throw new ArgumentNullException("chars");
            }
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("charCount");
            }
            if (index < 0 || index >= chars.Length)
            {
                throw new ArgumentOutOfRangeException("charIndex");
            }
            if (index + count > chars.Length)
            {
                throw new ArgumentOutOfRangeException("charCount");
            }

            int byteCount = GetByteCount(chars, index, count);
            byte[] bytes = new byte[byteCount];
            GetBytes(chars, index, count, bytes, 0);
            return bytes;
        }

        public override byte[] GetBytes(string s)
        {
            int byteCount = GetByteCount(s);
            byte[] bytes = new byte[byteCount];

            unsafe
            {
                fixed (char* charPtr = s)
                {
                    fixed (byte* bytePtr = bytes)
                    {
                        GetBytes(charPtr, s.Length, bytePtr, bytes.Length);
                        return bytes;
                    }
                }
            }
        }

        public override int GetBytes(string s, int charIndex, int charCount, byte[] bytes, int byteIndex)
        {
            if (charCount == 0)
            {
                return 0;
            }
            if (s == null)
            {
                throw new ArgumentNullException("chars");
            }
            if (bytes == null)
            {
                throw new ArgumentNullException("bytes");
            }
            if (charCount < 0)
            {
                throw new ArgumentOutOfRangeException("charCount");
            }
            if (charIndex < 0 || charIndex >= s.Length)
            {
                throw new ArgumentOutOfRangeException("charIndex");
            }
            if (charIndex + charCount > s.Length)
            {
                throw new ArgumentOutOfRangeException("charCount");
            }
            if (byteIndex < 0 || byteIndex >= bytes.Length)
            {
                throw new ArgumentOutOfRangeException("byteIndex");
            }

            unsafe
            {
                fixed (char* charPtr = s)
                {
                    fixed (byte* bytePtr = bytes)
                    {
                        return GetBytes(charPtr, s.Length, bytePtr, bytes.Length);
                    }
                }
            }
        }

        public override int GetCharCount(byte[] bytes, int index, int count)
        {
            if (count == 0)
            {
                return 0;
            }
            if (bytes == null)
            {
                throw new ArgumentNullException("chars");
            }
            if (index < 0 || index >= bytes.Length)
            {
                throw new ArgumentOutOfRangeException("index");
            }
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count");
            }
            int last = index + count;
            if (last > bytes.Length)
            {
                throw new ArgumentOutOfRangeException("count");
            }

            unsafe
            {
                fixed (byte* bytesPtr = bytes)
                {
                    return GetCharCount(bytesPtr, count);
                }
            }
        }

        public override unsafe int GetCharCount(byte* bytes, int count)
        {
            byte* bytesLimit = bytes + count;
            byte* bytesPtr = bytes;
            int charCount = 0;
            while (bytesPtr + 1 < bytesLimit)
            {
                ushort* word = (ushort*)(bytesPtr);
                int value = *word;
                char c = bytesToUnicode[value];
                if (!(c == 0 && (value & 0xFF) != 0))
                {
                    charCount++;

                    int value2 = unicodeToBytes[c];
                    if (value2 < 256)
                    {
                        bytesPtr++;
                    }
                    else
                    {
                        bytesPtr += 2;
                    }
                }
                else
                {
                    charCount += DoDecoderFallbackForGetCharCount(value);
                    bytesPtr++;
                }
            }
            if (bytesPtr == bytesLimit - 1)
            {
                int value = *bytesPtr;
                bytesPtr++;
                char c = bytesToUnicode[value];
                if (!(c == 0 && value != 0))
                {
                    charCount++;
                }
                else
                {
                    charCount += DoDecoderFallbackForGetCharCount(value);
                }
            }
            return charCount;
        }

        private int DoDecoderFallbackForGetCharCount(int value)
        {
            int charCount = 0;
            if (decoderFallbackBuffer == null)
            {
                decoderFallbackBuffer = this.DecoderFallback.CreateFallbackBuffer();
            }
            decoderFallbackBuffer.Fallback(new byte[] { (byte)(value & 0xFF), (byte)(value >> 8) }, 0);
            while (decoderFallbackBuffer.Remaining > 0)
            {
                char c2 = decoderFallbackBuffer.GetNextChar();
                if (c2 == 0)
                {
                    break;
                }
                charCount++;
            }
            return charCount;
        }

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
        {
            if (byteCount == 0)
            {
                return 0;
            }
            if (chars == null)
            {
                throw new ArgumentNullException("chars");
            }
            if (bytes == null)
            {
                throw new ArgumentNullException("bytes");
            }
            if (byteCount < 0)
            {
                throw new ArgumentOutOfRangeException("byteCount");
            }
            if (byteIndex < 0 || byteIndex >= bytes.Length)
            {
                throw new ArgumentOutOfRangeException("byteIndex");
            }
            if (byteIndex + byteCount > bytes.Length)
            {
                throw new ArgumentOutOfRangeException("byteCount");
            }
            if (byteIndex < 0 || byteIndex >= bytes.Length)
            {
                throw new ArgumentOutOfRangeException("byteIndex");
            }
            if (charIndex < 0 || charIndex >= chars.Length)
            {
                throw new ArgumentOutOfRangeException("charIndex");
            }

            unsafe
            {
                fixed (byte* bytesPtr = bytes)
                {
                    fixed (char* charsPtr = chars)
                    {
                        return GetChars(bytesPtr, byteCount, charsPtr, chars.Length - charIndex);
                    }
                }
            }
        }

        public override unsafe int GetChars(byte* bytes, int byteCount, char* chars, int charCount)
        {
            byte* byteLimit = bytes + byteCount;
            char* charLimit = chars + charCount;

            byte* bytesPtr = bytes;
            char* charsPtr = chars;
            while (bytesPtr - 1 < byteLimit && charsPtr < charLimit)
            {
                ushort* words = (ushort*)bytesPtr;
                int value = *words;
                char c = bytesToUnicode[value];
                if (!(c == 0 && (value & 0xFF) != 0))
                {
                    *charsPtr = c;
                    charsPtr++;
                    int value2 = unicodeToBytes[c];
                    if (value2 < 256)
                    {
                        bytesPtr++;
                    }
                    else
                    {
                        bytesPtr += 2;
                    }
                }
                else
                {
                    charsPtr = DoDecoderFallbackForGetChars(charsPtr, charLimit, value);
                    bytesPtr++;
                }
            }
            if (bytesPtr == byteLimit - 1 && charsPtr < charLimit)
            {
                int value = *bytesPtr;
                bytesPtr++;
                char c = bytesToUnicode[value];
                if (!(c == 0 && (value & 0xFF) != 0))
                {
                    *charsPtr = c;
                    charsPtr++;
                }
                else
                {
                    charsPtr = DoDecoderFallbackForGetChars(charsPtr, charLimit, value);
                }
            }
            if (bytesPtr != byteLimit)
            {
                throw new IndexOutOfRangeException("Not enough space in characters for encoded bytes");
            }
            return (int)(charsPtr - chars);
        }

        unsafe private char* DoDecoderFallbackForGetChars(char* charsPtr, char* charLimit, int value)
        {
            if (this.decoderFallbackBuffer == null)
            {
                this.decoderFallbackBuffer = this.DecoderFallback.CreateFallbackBuffer();
            }
            this.decoderFallbackBuffer.Fallback(new byte[] { (byte)(value & 0xFF), (byte)(value >> 8) }, 0);
            while (this.decoderFallbackBuffer.Remaining > 0)
            {
                char c2 = this.decoderFallbackBuffer.GetNextChar();
                if (c2 == 0)
                {
                    break;
                }
                if (charsPtr < charLimit)
                {
                    *charsPtr = c2;
                    charsPtr++;
                }
                else
                {
                    throw new IndexOutOfRangeException("Not enough space in characters for encoded bytes");
                }
            }
            return charsPtr;
        }

        public override int GetMaxByteCount(int charCount)
        {
            return charCount * 2;
        }

        public override int GetMaxCharCount(int byteCount)
        {
            return byteCount;
        }

        public override string EncodingName
        {
            get
            {
                return "Modified Shift-JIS 2.0";
            }
        }
    }
}
