using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace AinDecompiler
{
    static class EncodingDetector
    {
        private static bool IsValidAscii(byte[] bytes)
        {
            return IsValidAscii(bytes, 0, bytes.Length);
        }
        private static bool IsValidAscii(byte[] bytes, int startPosition, int endPosition)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException("bytes");
            }
            if (startPosition < 0 || startPosition > bytes.Length)
            {
                throw new ArgumentOutOfRangeException("startPosition", "Start position is out of the array bounds");
            }
            if (endPosition < 0 || endPosition > bytes.Length)
            {
                throw new ArgumentOutOfRangeException("startPosition", "End position is out of the array bounds");
            }
            if (startPosition > endPosition)
            {
                throw new ArgumentOutOfRangeException("startPosition", "Start position is beyond end position");
            }
            if (startPosition == endPosition)
            {
                return true;
            }

            for (int i = startPosition; i < endPosition; i++)
            {
                int b = bytes[i];
                if ((b >= 0x20 && b < 0x7F) || b == '\r' || b == '\n' || b == '\t')
                {

                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        public static Encoding DetectEncoding(string fileName)
        {
            return DetectEncoding(fileName, false);
        }

        public static Encoding DetectEncoding(string fileName, bool preferUtf8)
        {
            var utf8 = new UTF8Encoding(true, false).Clone() as UTF8Encoding;
            //This next line must be modified for .NET 4.0:
            utf8.DecoderFallback = new NullDecoderFallback();
            var shiftJis = Encoding.GetEncoding("shift_jis", EncoderFallback.ExceptionFallback, new NullDecoderFallback());
            var utf8Decoder = utf8.GetDecoder();
            var utf8Fallback = utf8Decoder.FallbackBuffer as NullDecoderFallbackBuffer;
            var shiftJisDecoder = shiftJis.GetDecoder();
            var shiftJisFallback = shiftJisDecoder.FallbackBuffer as NullDecoderFallbackBuffer;

            bool hasBom = false;
            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                //peek at first 3 bytes for BOM marker
                byte[] first3Bytes = new byte[3];
                fs.Read(first3Bytes, 0, 3);
                fs.Position = 0;
                if (first3Bytes[0] == 0xEF && first3Bytes[1] == 0xBB && first3Bytes[2] == 0xBF)
                {
                    hasBom = true;
                }

                long length = fs.Length;
                if (length == 0)
                {
                    return new UTF8Encoding(true, false);
                }

                int readSize = 4096;
                byte[] bytes = new byte[4096];
                bool fileWillEnd = false;

                while (true)
                {
                    if (length - fs.Position <= readSize)
                    {
                        readSize = (int)(length - fs.Position);
                        fileWillEnd = true;
                    }

                    fs.Read(bytes, 0, readSize);

                    bool asciiOnly = IsValidAscii(bytes, 0, readSize);

                    if (asciiOnly)
                    {
                        //had no characters outside of ASCII range
                        if (fileWillEnd)
                        {
                            return new UTF8Encoding(hasBom, false);
                        }
                        continue;
                    }

                    int utf8CharCount = utf8Decoder.GetCharCount(bytes, 0, readSize, fileWillEnd);
                    if (utf8Fallback.UsedFallback)
                    {
                        utf8CharCount = -1;
                    }
                    int shiftJisCharCount = shiftJisDecoder.GetCharCount(bytes, 0, readSize, fileWillEnd);
                    if (shiftJisFallback.UsedFallback)
                    {
                        shiftJisCharCount = -1;
                    }

                    if (utf8CharCount > 0 && shiftJisCharCount == -1)
                    {
                        return new UTF8Encoding(hasBom, false);
                    }
                    if (shiftJisCharCount > 0 && utf8CharCount == -1)
                    {
                        return Encoding.GetEncoding("shift_jis");
                    }
                    //file validates as both encodings, pick one?
                    //TODO: check for kana?
                    if (preferUtf8)
                    {
                        return new UTF8Encoding(hasBom, false);
                    }
                    else
                    {
                        return Encoding.GetEncoding("shift_jis");
                    }
                }
            }
        }

        class NullDecoderFallback : DecoderFallback
        {
            public override DecoderFallbackBuffer CreateFallbackBuffer()
            {
                return new NullDecoderFallbackBuffer(this);
            }

            public override int MaxCharCount
            {
                get { return 0; }
            }
        }

        class NullDecoderFallbackBuffer : DecoderFallbackBuffer
        {
            NullDecoderFallback parent;
            public bool UsedFallback
            {
                get;
                private set;
            }

            public NullDecoderFallbackBuffer(NullDecoderFallback parent)
            {
                this.parent = parent;
                this.UsedFallback = false;
            }

            public override bool Fallback(byte[] bytesUnknown, int index)
            {
                this.UsedFallback = true;
                return false;
            }

            public override char GetNextChar()
            {
                return '\0';
            }

            public override bool MovePrevious()
            {
                return false;
            }

            public override int Remaining
            {
                get
                {
                    return 0;
                }
            }

            public override void Reset()
            {
                base.Reset();
                this.UsedFallback = false;
            }
        }
    }
}
