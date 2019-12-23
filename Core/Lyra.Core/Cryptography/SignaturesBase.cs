using System;
using System.Text;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Math;

namespace Lyra.Core.Cryptography
{
    //
    // Parts of this code are from https://github.com/sander-/working-with-digital-signatures
    //
    public class SignaturesBase
    {
        public static bool ValidateAccountId(string AccountId)
        {
            try
            {
                if (AccountId[0] != 'L')
                    return false;

                Base58Encoding.DecodeAccountId(AccountId);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // It can validate either public or private key - thanks to the checksum
        public static bool ValidatePublicKey(string PublicKey)
        {
            try
            {
                Base58Encoding.DecodePublicKey(PublicKey);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool ValidatePrivateKey(string PrivateKey)
        {
            try
            {
                Base58Encoding.DecodePrivateKey(PrivateKey);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool VerifyAccountSignature(string message, string accountId, string signature)
        {
            if (string.IsNullOrWhiteSpace(message) || !ValidateAccountId(accountId) || string.IsNullOrWhiteSpace(signature))
                return false;
            var publicKeyBytes = Base58Encoding.DecodeAccountId(accountId);
            return VerifySignature(message, publicKeyBytes, signature);
        }

        public static bool VerifyAuthorizerSignature(string message, string publicKey, string signature)
        {
            if (string.IsNullOrWhiteSpace(message) || !ValidatePublicKey(publicKey) || string.IsNullOrWhiteSpace(signature))
                return false;
            var publicKeyBytes = Base58Encoding.DecodePublicKey(publicKey);
            return VerifySignature(message, publicKeyBytes, signature);
        }

        private static bool VerifySignature(string message, byte[] public_key_bytes, string signature)
        {
            var curve = SecNamedCurves.GetByName("secp256r1");
            var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H, curve.GetSeed());

            //var publicKeyBytes = Base58Encoding.Decode(publicKey);
            //var publicKeyBytes = Base58Encoding.DecodeWithCheckSum(publicKey);
            //var publicKeyBytes = Base58Encoding.DecodePublicKey(publicKey);

            var q = curve.Curve.DecodePoint(public_key_bytes);

            var keyParameters = new
                    Org.BouncyCastle.Crypto.Parameters.ECPublicKeyParameters(q,
                    domain);

            ISigner signer = SignerUtilities.GetSigner("SHA-256withECDSA");

            signer.Init(false, keyParameters);
            signer.BlockUpdate(Encoding.ASCII.GetBytes(message), 0, message.Length);

            var signatureBytes = Base58Encoding.Decode(signature);

            var result = signer.VerifySignature(signatureBytes);
            return result;
        }

        public static string GetSignature(string privateKey, string message)
        {
            var curve = SecNamedCurves.GetByName("secp256r1");
            var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H, curve.GetSeed());

            //byte[] pkbytes = Base58Encoding.Decode(privateKey);
            //byte[] pkbytes = Base58Encoding.DecodeWithCheckSum(privateKey);
            byte[] pkbytes = Base58Encoding.DecodePrivateKey(privateKey);

            var keyParameters = new
                    ECPrivateKeyParameters(new Org.BouncyCastle.Math.BigInteger(1, pkbytes),
                    domain);

            ISigner signer = SignerUtilities.GetSigner("SHA-256withECDSA");

            signer.Init(true, keyParameters);
            signer.BlockUpdate(Encoding.ASCII.GetBytes(message), 0, message.Length);
            var signature = signer.GenerateSignature();
            return Base58Encoding.Encode(signature);
        }

        private static byte[] DerivePublicKeyBytes(string privateKey)
        {
            var curve = SecNamedCurves.GetByName("secp256r1");
            var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H, curve.GetSeed());

            byte[] pkbytes = Base58Encoding.DecodePrivateKey(privateKey);
            var d = new BigInteger(pkbytes);
            var q = domain.G.Multiply(d);

            var publicKey = new ECPublicKeyParameters(q, domain);

            return publicKey.Q.GetEncoded();
        }

        public static string GetAccountIdFromPrivateKey(string privateKey)
        {
            byte[] public_key_bytes = DerivePublicKeyBytes(privateKey);
            return Base58Encoding.EncodeAccountId(public_key_bytes);
        }

        public static string GetPublicKeyFromPrivateKey(string privateKey)
        {
            byte[] public_key_bytes = DerivePublicKeyBytes(privateKey);
            return Base58Encoding.EncodePublicKey(public_key_bytes);
        }

        //public void GenarateKeyPair()
        //{
        //    var curve = ECNamedCurveTable.GetByName("secp256k1");
        //    var domainParams = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H, curve.GetSeed());
        //    var secureRandom = new SecureRandom();
        //    var keyParams = new ECKeyGenerationParameters(domainParams, secureRandom);
        //    var generator = new ECKeyPairGenerator("ECDSA");
        //    generator.Init(keyParams);
        //    var keyPair = generator.GenerateKeyPair();
        //    PrivateKey = keyPair.Private as ECPrivateKeyParameters;
        //    PublicKey = keyPair.Public as ECPublicKeyParameters;
        //}

        public static (string privateKey, string publicKey) GenerateWallet()
        {
            while(true)
            {
                var kp = GenerateKeys(256);
                ECPrivateKeyParameters prvk = kp.Private as ECPrivateKeyParameters;
                ECPublicKeyParameters pubk = kp.Public as ECPublicKeyParameters;

                // ugly bug: Exception in SyncServiceChain(): Scalar is not in the interval [1, n - 1] (Parameter 'd')
                if (prvk.D.CompareTo(BigInteger.One) < 0 || prvk.D.CompareTo(prvk.Parameters.N) >= 0)
                    continue;

                var privBuff = prvk.D.ToByteArrayUnsigned();
                var pubBuff = pubk.Q.GetEncoded();

                var result = (Base58Encoding.EncodePrivateKey(privBuff), Base58Encoding.EncodePublicKey(pubBuff));
                return result;
            }
        }
        private static AsymmetricCipherKeyPair GenerateKeys(int keySize)
        {
            //using ECDSA algorithm for the key generation
            var gen = new Org.BouncyCastle.Crypto.Generators.ECKeyPairGenerator("ECDSA");

            //Creating Random
            var secureRandom = new SecureRandom();

            //Parameters creation using the random and keysize
            var keyGenParam = new KeyGenerationParameters(secureRandom, keySize);

            //Initializing generation algorithm with the Parameters--This method Init i modified
            gen.Init(keyGenParam);

            //Generation of Key Pair
            return gen.GenerateKeyPair();
        }

        // is this a good key generating?
        //public string GeneratePrivateKey()
        //{
        //    var privateKey = new byte[32];
        //    var rnd = System.Security.Cryptography.RandomNumberGenerator.Create();
        //    rnd.GetBytes(privateKey);
        //    //return Base58Encoding.Encode(privateKey);
        //    //return Base58Encoding.EncodeWithCheckSum(privateKey);
        //    return Base58Encoding.EncodePrivateKey(privateKey);
        //}


        //private string GetPublicKeyFromPrivateKey(string privateKey)
        //{
        //    var p = BigInteger.Parse("0FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEFFFFFC2F", NumberStyles.HexNumber);
        //    var b = (BigInteger)7;
        //    var a = BigInteger.Zero;
        //    var Gx = BigInteger.Parse("79BE667EF9DCBBAC55A06295CE870B07029BFCDB2DCE28D959F2815B16F81798", NumberStyles.HexNumber);
        //    var Gy = BigInteger.Parse("483ADA7726A3C4655DA4FBFC0E1108A8FD17B448A68554199C47D08FFB10D4B8", NumberStyles.HexNumber);

        //    CurveFp curve256 = new CurveFp(p, a, b);
        //    Point generator256 = new Point(curve256, Gx, Gy);

        //    var secret = BigInteger.Parse(privateKey, NumberStyles.HexNumber);
        //    var pubkeyPoint = generator256 * secret;
        //    return pubkeyPoint.X.ToString("X") + pubkeyPoint.Y.ToString("X");
        //}

        // test sign/verify functions
        public string SignData(string msg, ECPrivateKeyParameters privKey)
        {
            try
            {
                byte[] msgBytes = Encoding.UTF8.GetBytes(msg);

                ISigner signer = SignerUtilities.GetSigner("SHA-256withECDSA");
                signer.Init(true, privKey);
                signer.BlockUpdate(msgBytes, 0, msgBytes.Length);
                byte[] sigBytes = signer.GenerateSignature();

                return Convert.ToBase64String(sigBytes);
            }
            catch (Exception exc)
            {
                Console.WriteLine("Signing Failed: " + exc.ToString());
                return null;
            }
        }

        public bool VerifySignature(ECPublicKeyParameters pubKey, string signature, string msg)
        {
            try
            {
                byte[] msgBytes = Encoding.UTF8.GetBytes(msg);
                byte[] sigBytes = Convert.FromBase64String(signature);

                ISigner signer = SignerUtilities.GetSigner("SHA-256withECDSA");
                signer.Init(false, pubKey);
                signer.BlockUpdate(msgBytes, 0, msgBytes.Length);
                return signer.VerifySignature(sigBytes);
            }
            catch (Exception exc)
            {
                Console.WriteLine("Verification failed with the error: " + exc.ToString());
                return false;
            }
        }
    }


    //class Transaction
    //{
    //    public string FromPublicKey { get; internal set; }
    //    public string ToPublicKey { get; internal set; }
    //    public int Amount { get; internal set; }
    //    public string Signature { get; internal set; }

    //    public override string ToString()
    //    {
    //        return $"{this.Amount}:{this.FromPublicKey}:{this.ToPublicKey}";
    //    }
    //}

    /*
     * Original Source: https://bitcoin.stackexchange.com/a/25039
     */
    //class Point
    //{
    //    public readonly Point INFINITY = new Point(null, default(BigInteger), default(BigInteger));
    //    public CurveFp Curve { get; private set; }
    //    public BigInteger X { get; private set; }
    //    public BigInteger Y { get; private set; }

    //    public Point(CurveFp curve, BigInteger x, BigInteger y)
    //    {
    //        this.Curve = curve;
    //        this.X = x;
    //        this.Y = y;
    //    }
    //    public Point Double()
    //    {
    //        if (this == INFINITY)
    //            return INFINITY;

    //        BigInteger p = this.Curve.p;
    //        BigInteger a = this.Curve.a;
    //        BigInteger l = ((3 * this.X * this.X + a) * InverseMod(2 * this.Y, p)) % p;
    //        BigInteger x3 = (l * l - 2 * this.X) % p;
    //        BigInteger y3 = (l * (this.X - x3) - this.Y) % p;
    //        return new Point(this.Curve, x3, y3);
    //    }
    //    public override string ToString()
    //    {
    //        if (this == INFINITY)
    //            return "infinity";
    //        return string.Format("({0},{1})", this.X, this.Y);
    //    }
    //    public Point operator +(Point left, Point right)
    //    {
    //        if (right == INFINITY)
    //            return left;
    //        if (left == INFINITY)
    //            return right;
    //        if (left.X == right.X)
    //        {
    //            if ((left.Y + right.Y) % left.Curve.p == 0)
    //                return INFINITY;
    //            else
    //                return left.Double();
    //        }

    //        var p = left.Curve.p;
    //        var l = ((right.Y - left.Y) * InverseMod(right.X - left.X, p)) % p;
    //        var x3 = (l * l - left.X - right.X) % p;
    //        var y3 = (l * (left.X - x3) - left.Y) % p;
    //        return new Point(left.Curve, x3, y3);
    //    }
    //    public Point operator *(Point left, BigInteger right)
    //    {
    //        var e = right;
    //        if (e == 0 || left == INFINITY)
    //            return INFINITY;
    //        var e3 = 3 * e;
    //        var negativeLeft = new Point(left.Curve, left.X, -left.Y);
    //        var i = LeftmostBit(e3) / 2;
    //        var result = left;
    //        while (i > 1)
    //        {
    //            result = result.Double();
    //            if ((e3 & i) != 0 && (e & i) == 0)
    //                result += left;
    //            if ((e3 & i) == 0 && (e & i) != 0)
    //                result += negativeLeft;
    //            i /= 2;
    //        }
    //        return result;
    //    }

    //    private BigInteger LeftmostBit(BigInteger x)
    //    {
    //        BigInteger result = 1;
    //        while (result <= x)
    //            result = 2 * result;
    //        return result / 2;
    //    }
    //    private BigInteger InverseMod(BigInteger a, BigInteger m)
    //    {
    //        while (a < 0) a += m;
    //        if (a < 0 || m <= a)
    //            a = a % m;
    //        BigInteger c = a;
    //        BigInteger d = m;

    //        BigInteger uc = 1;
    //        BigInteger vc = 0;
    //        BigInteger ud = 0;
    //        BigInteger vd = 1;

    //        while (c != 0)
    //        {
    //            BigInteger r;
    //            //q, c, d = divmod( d, c ) + ( c, );
    //            var q = BigInteger.DivRem(d, c, out r);
    //            d = c;
    //            c = r;

    //            //uc, vc, ud, vd = ud - q*uc, vd - q*vc, uc, vc;
    //            var uct = uc;
    //            var vct = vc;
    //            var udt = ud;
    //            var vdt = vd;
    //            uc = udt - q * uct;
    //            vc = vdt - q * vct;
    //            ud = uct;
    //            vd = vct;
    //        }
    //        if (ud > 0) return ud;
    //        else return ud + m;
    //    }
    //}

    //class CurveFp
    //{
    //    public BigInteger p { get; private set; }
    //    public BigInteger a { get; private set; }
    //    public BigInteger b { get; private set; }
    //    public CurveFp(BigInteger p, BigInteger a, BigInteger b)
    //    {
    //        this.p = p;
    //        this.a = a;
    //        this.b = b;
    //    }
    //}
}
