using Org.BouncyCastle.Crypto.Signers;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Data.Crypto
{
    public class CryptoUtils
    {
        public const int PAYMENTID_LENGTH = 12;
        private const string Digits = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

        static Random rands;

        public CryptoUtils()
        {
            rands = new Random();
        }

        public static string GeneratePaymentID()
        {
            char[] payId = new char[PAYMENTID_LENGTH];
            
            for(int i = 0; i < PAYMENTID_LENGTH; i++)
            {
                payId[i] = Digits[rands.Next(0, Digits.Length - 1)];
            }

            return payId.ToString();
        }
    }
}
