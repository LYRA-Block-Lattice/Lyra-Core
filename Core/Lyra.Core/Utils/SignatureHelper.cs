using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Lyra.Core.Cryptography
{
    public class SignatureHelper
    {
        public static byte[] derSign(byte[] signature)
        {
            byte[] rb = new byte[signature.Length / 2];
            byte[] sb = new byte[signature.Length / 2];

            Array.Copy(signature, 0, rb, 0, signature.Length / 2);
            Array.Copy(signature, signature.Length / 2, rb, 0, signature.Length / 2);

            int off = (2 + 2) + rb.Length;
            int tot = off + (2 - 2) + sb.Length;
            byte[] der = new byte[tot + 2];
            der[0] = 0x30;
            der[1] = (byte)(tot & 0xff);
            der[2 + 0] = 0x02;
            der[2 + 1] = (byte)(rb.Length & 0xff);
            Array.Copy(rb, 0, der, 2 + 2, rb.Length);
            der[off + 0] = 0x02;
            der[off + 1] = (byte)(sb.Length & 0xff);
            Array.Copy(sb, 0, der, off + 2, sb.Length);
            return der;
        }

        public static byte[] Der2Net(byte[] signature)
        {
            int startR = (signature[1] & 0x80) != 0 ? 3 : 2;
            int lengthR = signature[startR + 1];
            int startS = startR + 2 + lengthR;
            int lengthS = signature[startS + 1];

            var buff = new byte[lengthR + lengthS];
            Array.Copy(signature, startR + 2, buff, 0, lengthR);
            Array.Copy(signature, startS + 2, buff, lengthR, lengthS);
            return buff;
        }
        public static byte[] extractR(byte[] signature)
        {
            int startR = (signature[1] & 0x80) != 0 ? 3 : 2;
            int lengthR = signature[startR + 1];
            var buff = new byte[lengthR];
            Array.Copy(signature, startR + 2, buff, 0, lengthR);
            return buff;
            //return new BigInteger(buff);
        }

        public static byte[] extractS(byte[] signature)
        {
            int startR = (signature[1] & 0x80) != 0 ? 3 : 2;
            int lengthR = signature[startR + 1];
            int startS = startR + 2 + lengthR;
            int lengthS = signature[startS + 1];
            var buff = new byte[lengthS];
            Array.Copy(signature, startS + 2, buff, 0, lengthS);
            return buff;
            //return new BigInteger(buff);
        }
    }

    public class IllegalSignatureFormatException : Exception
    {
        public IllegalSignatureFormatException(string message) : base(message)
        { }
    }

    class ConvertECDSASignature
    {
        private static int BYTE_SIZE_BITS = 8;
        private static byte ASN1_SEQUENCE = 0x30;
        private static byte ASN1_INTEGER = 0x02;

        public static byte[] lightweightConvertSignatureFromX9_62ToISO7816_8(int orderInBits, byte[] x9_62)
        {
            int offset = 0;
            if (x9_62[offset++] != ASN1_SEQUENCE)
            {
                throw new IllegalSignatureFormatException("Input is not a SEQUENCE");
            }

            int sequenceSize = parseLength(x9_62, offset, out offset);
            int sequenceValueOffset = offset;

            int nBytes = (orderInBits + BYTE_SIZE_BITS - 1) / BYTE_SIZE_BITS;
            byte[] iso7816_8 = new byte[2 * nBytes];

            // retrieve and copy r

            if (x9_62[offset++] != ASN1_INTEGER)
            {
                throw new IllegalSignatureFormatException("Input is not an INTEGER");
            }

            int rSize = parseLength(x9_62, offset, out offset);
            copyToStatic(x9_62, offset, rSize, iso7816_8, 0, nBytes);

            offset += rSize;

            // --- retrieve and copy s

            if (x9_62[offset++] != ASN1_INTEGER)
            {
                throw new IllegalSignatureFormatException("Input is not an INTEGER");
            }

            int sSize = parseLength(x9_62, offset, out offset);
            copyToStatic(x9_62, offset, sSize, iso7816_8, nBytes, nBytes);

            offset += sSize;

            if (offset != sequenceValueOffset + sequenceSize)
            {
                throw new IllegalSignatureFormatException("SEQUENCE is either too small or too large for the encoding of r and s");
            }

            return iso7816_8;
        }

        /**
         * Copies an variable sized, signed, big endian number to an array as static sized, unsigned, big endian number.
         * Assumes that the iso7816_8 buffer is zeroized from the iso7816_8Offset for nBytes.
         */
        private static void copyToStatic(byte[] sint, int sintOffset, int sintSize, byte[] iso7816_8, int iso7816_8Offset, int nBytes)
        {
            // if the integer starts with zero, then skip it
            if (sint[sintOffset] == 0x00)
            {
                sintOffset++;
                sintSize--;
            }

            // after skipping the zero byte then the integer must fit
            if (sintSize > nBytes)
            {
                throw new IllegalSignatureFormatException("Number format of r or s too large");
            }

            // copy it into the right place
            Array.Copy(sint, sintOffset, iso7816_8, iso7816_8Offset + nBytes - sintSize, sintSize);
        }

        /*
         * Standalone BER decoding of length value, up to 2^31 -1.
         */
        private static int parseLength(byte[] input, int startOffset, out int offset)
        {
            offset = startOffset;
            byte l1 = input[offset++];
            // --- return value of single byte length encoding
            if (l1 < 0x80)
            {
                return l1;
            }

            // otherwise the first byte of the length specifies the number of encoding bytes that follows
            int end = offset + l1 & 0x7F;

            uint result = 0;

            // --- skip leftmost zero bytes (for BER)
            while (offset < end)
            {
                if (input[offset] != 0x00)
                {
                    break;
                }
                offset++;
            }

            // --- test against maximum value
            if (end - offset > sizeof(uint))
            {
                throw new IllegalSignatureFormatException("Length of TLV is too large");
            }

            // --- parse multi byte length encoding
            while (offset < end)
            {
                result = (result << BYTE_SIZE_BITS) ^ input[offset++];
            }

            // --- make sure that the uint isn't larger than an int can handle
            if (result > Int32.MaxValue)
            {
                throw new IllegalSignatureFormatException("Length of TLV is too large");
            }

            // --- return multi byte length encoding
            return (int)result;
        }
    }
}
