using System;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;

namespace Lyra.Core.Cryptography
{
    /*
     * From: https://gist.github.com/CodesInChaos/3175971
     */
     /*
    public static class Base58Encoding
    {
        public const int CheckSumSizeInBytes = 4;

        public static byte[] AddCheckSum(byte[] data)
        {
            byte[] checkSum = GetCheckSum(data);
            byte[] dataWithCheckSum = ArrayHelpers.ConcatArrays(data, checkSum);
            return dataWithCheckSum;
        }

        //Returns null if the checksum is invalid
        public static byte[] VerifyAndRemoveCheckSum(byte[] data)
        {
            byte[] result = ArrayHelpers.SubArray(data, 0, data.Length - CheckSumSizeInBytes);
            byte[] givenCheckSum = ArrayHelpers.SubArray(data, data.Length - CheckSumSizeInBytes);
            byte[] correctCheckSum = GetCheckSum(result);
            if (givenCheckSum.SequenceEqual(correctCheckSum))
                return result;
            else
                return null;
        }

        private const string Digits = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

        public static string Encode(byte[] data)
        {
            // Decode byte[] to BigInteger
            BigInteger intData = 0;
            for (int i = 0; i < data.Length; i++)
            {
                intData = intData * 256 + data[i];
            }

            // Encode BigInteger to Base58 string
            string result = "";
            while (intData > 0)
            {
                int remainder = (int)(intData % 58);
                intData /= 58;
                result = Digits[remainder] + result;
            }

            // Append `1` for each leading 0 byte
            for (int i = 0; i < data.Length && data[i] == 0; i++)
            {
                result = '1' + result;
            }
            return result;
        }

        private static string EncodeWithCheckSum(byte[] data)
        {

            return Encode(AddCheckSum(data));
        }

        public static byte[] Decode(string s)
        {
            // Decode Base58 string to BigInteger 
            BigInteger intData = 0;
            for (int i = 0; i < s.Length; i++)
            {
                int digit = Digits.IndexOf(s[i]); //Slow
                if (digit < 0)
                    throw new FormatException(string.Format("Invalid Base58 character `{0}` at position {1}", s[i], i));
                intData = intData * 58 + digit;
            }

            // Encode BigInteger to byte[]
            // Leading zero bytes get encoded as leading `1` characters
            int leadingZeroCount = s.TakeWhile(c => c == '1').Count();
            var leadingZeros = Enumerable.Repeat((byte)0, leadingZeroCount);
            var bytesWithoutLeadingZeros =
                intData.ToByteArray()
                .Reverse()// to big endian
                .SkipWhile(b => b == 0);//strip sign byte
            var result = leadingZeros.Concat(bytesWithoutLeadingZeros).ToArray();
            return result;
        }

        public static string EncodePrivateKey(byte[] private_key_data)
        {

            return EncodeWithCheckSum(private_key_data);
        }

        public static string EncodePublicKey(byte[] public_key_data)
        {
            return EncodeWithCheckSum(public_key_data);
        }

        public static string EncodeAccountId(byte[] public_key_data)
        {
            ///// *** Add "L" prefix
            //byte[] L_encoded = new byte[public_key_data.Length + 1];
            //L_encoded[0] = (byte)'L';
            //Buffer.BlockCopy(public_key_data, 0, L_encoded, 1, public_key_data.Length);
            ///// ***

            return "L" + EncodeWithCheckSum(public_key_data);
        }

        public static byte[] DecodePrivateKey(string privateKey)
        {
            return DecodeWithCheckSum(privateKey);
        }

        public static byte[] DecodePublicKey(string publicKey)
        {

            return DecodeWithCheckSum(publicKey);
        }

        public static byte[] DecodeAccountId(string accountId)
        {
            string without_prefix = accountId.Substring(1, accountId.Length - 1);
            byte[] decoded = DecodeWithCheckSum(without_prefix);
            //byte[] L_decoded = DecodeWithCheckSum(accountId);
            //byte[] decoded = new byte[L_decoded.Length - 1];
            //Buffer.BlockCopy(L_decoded, 1, decoded, 0, decoded.Length);
            return decoded;
        }

        // Throws `FormatException` if s is not a valid Base58 string, or the checksum is invalid
        private static byte[] DecodeWithCheckSum(string s)
        {
            var dataWithCheckSum = Decode(s);
            var dataWithoutCheckSum = VerifyAndRemoveCheckSum(dataWithCheckSum);
            if (dataWithoutCheckSum == null)
                throw new FormatException("Base58 checksum is invalid");
            return dataWithoutCheckSum;
        }

        private static byte[] GetCheckSum(byte[] data)
        {            
            SHA256 sha256 = new SHA256Managed();
            byte[] hash1 = sha256.ComputeHash(data);
            byte[] hash2 = sha256.ComputeHash(hash1);

            var result = new byte[CheckSumSizeInBytes];
            Buffer.BlockCopy(hash2, 0, result, 0, result.Length);

            return result;
        }
    }

    public class ArrayHelpers
    {
        public static T[] ConcatArrays<T>(params T[][] arrays)
        {   
            var result = new T[arrays.Sum(arr => arr.Length)];
            int offset = 0;
            for (int i = 0; i < arrays.Length; i++)
            {
                var arr = arrays[i];
                Buffer.BlockCopy(arr, 0, result, offset, arr.Length);
                offset += arr.Length;
            }
            return result;
        }

        public static T[] ConcatArrays<T>(T[] arr1, T[] arr2)
        {   
            var result = new T[arr1.Length + arr2.Length];
            Buffer.BlockCopy(arr1, 0, result, 0, arr1.Length);
            Buffer.BlockCopy(arr2, 0, result, arr1.Length, arr2.Length);
            return result;
        }

        public static T[] SubArray<T>(T[] arr, int start, int length)
        {
            var result = new T[length];
            Buffer.BlockCopy(arr, start, result, 0, length);
            return result;
        }

        public static T[] SubArray<T>(T[] arr, int start)
        {   
            return SubArray(arr, start, arr.Length - start);
        }
    }*/
}
