using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Math;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Lyra.Core.Cryptography
{
    public class SignatureHelper
    {
        public static byte[] ConvertDerToP1393(byte[] bcSignature)
        {
            var asn1Stream = new Asn1InputStream(bcSignature);

            var bcDerSequence = ((DerSequence)asn1Stream.ReadObject());
            var bcR = ((DerInteger)bcDerSequence[0]).PositiveValue.ToByteArrayUnsigned();
            var bcS = ((DerInteger)bcDerSequence[1]).PositiveValue.ToByteArrayUnsigned();

            var buff = new byte[bcR.Length + bcS.Length];
            Array.Copy(bcR, 0, buff, 0, bcR.Length);
            Array.Copy(bcS, 0, buff, bcR.Length, bcS.Length);
            return buff;
        }

        public static byte[] derSign(byte[] signature)
        {
            byte[] r = signature.Take(signature.Length / 2).ToArray();
            byte[] s = signature.Skip(signature.Length / 2).ToArray();

            MemoryStream stream = new MemoryStream();
            DerOutputStream der = new DerOutputStream(stream);

            Asn1EncodableVector v = new Asn1EncodableVector();
            v.Add(new DerInteger(new BigInteger(1, r)));
            v.Add(new DerInteger(new BigInteger(1, s)));
            der.WriteObject(new DerSequence(v));

            return stream.ToArray();
        }
    }
}
