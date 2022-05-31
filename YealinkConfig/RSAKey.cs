using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace YealinkConfig
{
    public class RSAKey
    {
        public byte[] Encrypt(string text, string m, string e)
        {
            var modulus = CreateBigInteger(m);
            var exponent = CreateBigInteger(e);

            var ml = modulus;
            var bitLength = 0;
            do
            {
                bitLength++;
                ml /= 2;
            } while (ml != 0);

            var encryptedNumber = Pkcs1Pad2(text, (bitLength + 7) >> 3);
            Debug.Print(encryptedNumber.ToString());
            encryptedNumber = BigInteger.ModPow(encryptedNumber, exponent, modulus);
            return encryptedNumber.ToByteArray().Take(bitLength/8).Reverse().ToArray();
        }

        private static BigInteger Pkcs1Pad2(string data, int keySize)
        {
            if (keySize < data.Length + 11)
                return new BigInteger();

            var buffer = new byte[keySize];
            var i = data.Length - 1;

            var a = data.ToCharArray();
            Debug.Print(data);
            while (i >= 0 && keySize > 0)
            {
                char c = a[i--];
                if (c < 128)
                {
                    buffer[--keySize] = (byte)c;
                }
                else if (c > 127 && c < 2048)
                {
                    buffer[--keySize] = (byte)((c & 63) | 128);
                    buffer[--keySize] = (byte)((c >> 6) | 192);
                }
                else
                {
                    buffer[--keySize] = (byte)((c & 63) | 128);
                    buffer[--keySize] = (byte)(((c >> 6) & 63) | 128);
                    buffer[--keySize] = (byte)((c >> 12) | 224);
                }
            }

            var random = new Random();
            buffer[--keySize] = 0;
            while (keySize > 2)
            {
                buffer[--keySize] = (byte)random.Next(1, 256);
            }

            buffer[--keySize] = 2;
            buffer[--keySize] = 0;

            Array.Reverse(buffer);

            return new BigInteger(buffer);
        }

        public static BigInteger CreateBigInteger(string hex)
        {
            return BigInteger.Parse("00" + hex, NumberStyles.AllowHexSpecifier);
        }
    }
}
