using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace AinDecompiler
{
    public static class StreamExtensions
    {
        public static void WriteToStream(this Stream streamToReadFrom, Stream streamToWriteTo)
        {
            WriteToStream(streamToReadFrom, streamToWriteTo, streamToReadFrom.Length - streamToReadFrom.Position);
        }

        public static void WriteToStream(this Stream streamToReadFrom, Stream streamToWriteTo, long byteCount)
        {
            int bufferSize = 65536;
            byte[] buffer = new byte[bufferSize];

            long bytesRemaining = byteCount;
            while (bytesRemaining > 0)
            {
                int bytesToRead = bufferSize;
                if (bytesToRead > bytesRemaining)
                {
                    bytesToRead = (int)bytesRemaining;
                }
                int bytesRead = streamToReadFrom.Read(buffer, 0, bytesToRead);
                if (bytesRead != bytesToRead)
                {
                    //???
                    if (bytesRead == 0)
                    {
                        throw new IOException("Failed to read from the stream");
                    }
                }
                streamToWriteTo.Write(buffer, 0, bytesRead);
                bytesRemaining -= bytesRead;
            }

        }

        public static void WriteFromStream(this Stream streamToWriteTo, Stream streamToReadFrom)
        {
            WriteToStream(streamToReadFrom, streamToWriteTo, streamToReadFrom.Length - streamToReadFrom.Position);
        }

        public static void WriteFromStream(this Stream streamToWriteTo, Stream streamToReadFrom, long byteCount)
        {
            WriteToStream(streamToReadFrom, streamToWriteTo, byteCount);
        }

        public static int PeekByte(this Stream stream)
        {
            long position = stream.Position;
            int b = stream.ReadByte();
            stream.Position = position;
            return b;
        }


    }
}
