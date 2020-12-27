using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace AinDecompiler
{
    public static class DecodeAin
    {
        public static void Decode(byte[] bytes)
        {
            var twister = new Twister();
            twister.Decrypt(bytes, 0x5D3E3);
        }

        public static byte[] Decode2(byte[] bytes)
        {
            var bytes2 = (byte[])bytes.Clone();
            Decode(bytes2);
            return bytes2;
        }

        class Twister
        {
            uint[] state = new uint[0x270];
            private void Init(uint key)
            {
                unchecked
                {
                    uint key32 = (uint)key;
                    for (int i = 0; i < state.Length; i++)
                    {
                        state[i] = key32;
                        key32 *= (uint)0x10dcd;
                    }
                }
            }

            private void Update()
            {
                unchecked
                {
                    int i;
                    int ahead = 0x18d;
                    int write = 0;
                    int read = 2;
                    uint current = state[1];
                    uint last = state[0];
                    uint tmp;

                    for (i = 0; i < 0x270; i++)
                    {
                        tmp = (((current ^ last) & 0x7ffffffe) ^ last) >> 1;
                        tmp = ((current & 1) != 0) ? (tmp ^ 0x9908b0df) : tmp;
                        state[write] = tmp ^ state[ahead];
                        last = current;
                        current = state[read];
                        write++;
                        read++;
                        ahead++;
                        while (read >= 0x270)
                            read -= 0x270;
                        while (ahead >= 0x270)
                            ahead -= 0x270;
                    }
                }

            }

            private static uint Temper(uint input)
            {
                unchecked
                {
                    input ^= (input >> 11);
                    input ^= (input << 7) & 0x9d2c5680;
                    input ^= (input << 15) & 0xefc60000;
                    input ^= (input >> 18);
                    return input;
                }
            }

            public void Decrypt(byte[] bytes, uint key)
            {
                Init(key);
                Update();
                int offset = 0;

                for (int index = 0; index < bytes.Length; index++)
                {
                    if (offset >= 0x270)
                    {
                        Update();
                        offset = 0;
                    }
                    byte xorKey = (byte)(Temper(state[offset]) & 0xFF);
                    bytes[index] ^= xorKey;
                    offset++;
                }
            }
        }

    }
}
