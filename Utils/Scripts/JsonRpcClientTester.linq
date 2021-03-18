<Query Kind="Program">
  <NuGetReference>Newtonsoft.Json</NuGetReference>
  <NuGetReference>Portable.BouncyCastle</NuGetReference>
  <NuGetReference>WebSocket4Net</NuGetReference>
  <Namespace>Newtonsoft.Json</Namespace>
  <Namespace>Newtonsoft.Json.Linq</Namespace>
  <Namespace>Org.BouncyCastle.Asn1</Namespace>
  <Namespace>Org.BouncyCastle.Asn1.Sec</Namespace>
  <Namespace>Org.BouncyCastle.Crypto</Namespace>
  <Namespace>Org.BouncyCastle.Crypto.Parameters</Namespace>
  <Namespace>Org.BouncyCastle.Math</Namespace>
  <Namespace>Org.BouncyCastle.Security</Namespace>
  <Namespace>System.Security.Authentication</Namespace>
  <Namespace>System.Security.Cryptography</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>WebSocket4Net</Namespace>
</Query>

async static Task Main(string[] args)
{
	//var networkId = "testnet";
	//var url = "wss://testnet.lyra.live/api/v1/socket";
	
	var networkId = "devnet";
	//var url = "wss://api.devnet:4504/api/v1/socket";
	var url = "wss://localhost:4504/api/v1/socket";
	//var url = "wss://localhost:4504/api/v2/socket";

	var cancel = new CancellationTokenSource();

	var w1 = new JsWallet("dkrwRdqNjEEshpLuEPPqc6zM1HM3nzGjsYts39zzA1iUypcpj", url, networkId, cancel);

	(var pvt, var pub) = PortableSignatures.GenerateWallet();
	// if need to insepct.
	File.AppendAllText($"{Environment.ExpandEnvironmentVariables("%TEMP%")}\\testerkeys.txt", $"Tester {DateTime.Now} {pvt} {pub}");

	var wtest = new JsWallet(pvt, url, networkId, cancel);
	
	_ = Task.Run(async () => await Tester.TestProc(wtest, w1, networkId, cancel) );

	WaitHandle.WaitAny(new[] { cancel.Token.WaitHandle });
}

public class Tester
{
	public static async Task TestProc(JsWallet tester, JsWallet richGuy, string networkId, CancellationTokenSource cancel)
	{
		//var json = "{\"jsonrpc\":\"2.0\",\"method\":\"PoolCalculate\",\"id\": 123,\"params\":[\"LHEozCoPdfVHj2eohVUnivjEK3M5Z1vfzq6wR427mEg6kLTLLoTCMnbiyBT9CPMDst9fUYZaapHScXxYtHv9tWe5U2eoiR\", \"LYR\", 1000, 0.001]}";
		//SendJson(json);
		while(!tester.IsReady)
			await Task.Delay(1000);

		tester.CallRPC("get status", "ApiStatus Status(string version, string networkid)", "Status", new string[] { "2.2.0.0", networkId });
		tester.CallRPC("get status with error", "ApiStatus Status(string version, string networkid) with error", "Status", new string[] { "2.0", networkId });

		tester.CallRPC("wallet get balance", "BalanceResult Balance(string accountId)", "Balance", new string[] { tester.AccountId });
		tester.CallRPC("monitor receiving", "void Monitor(string accountId)", "Monitor", new string[] { tester.AccountId });

		// someone send to this wallet
		await Task.Delay(2000);
		Console.WriteLine("\n> rich guy is sending you token...\n");
		richGuy.Send(tester.AccountId, "LYR", 13000);
		await Task.Delay(2000);

		tester.CallRPC("balance shows unreceived", "BalanceResult Balance(string accountId)", "Balance", new string[] { tester.AccountId });
		tester.CallRPC("receive it", "BalanceResult Receive(string accountId)", "Receive", new string[] { tester.AccountId });		
		tester.CallRPC("send token", "BalanceResult Send(string accountId, decimal amount, string destAccount, string ticker)", "Send", new string[] { tester.AccountId, "10", richGuy.AccountId, "LYR" });

		var r = new Random(); var tokenName = $"json-{r.Next(10000, 10000000)}";
		tester.CallRPC("create a token", "BalanceResult Token(string accountId, string name, string domain, decimal supply)", "Token", new string[] { tester.AccountId, tokenName, "testit", "10000000" });
		var jsonTime = (long) Math.Round(DateTime.UtcNow
			   .Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))
			   .TotalMilliseconds);
		tester.CallRPC("show all transaction history", "List<TransactionDescription> History(string accountId, long startTime, long endTime, int count)", "History", new string[] { tester.AccountId, "0", jsonTime.ToString(), "100" });

		// doing pool stuff
		var ticker = $"testit/{tokenName}";
		tester.CallRPC("get pool info", "PoolInfo Pool(string token0, string token1)", "Pool", new string[] { "LYR", ticker });
		(dynamic reply, _) = tester.CallRPC("create a pool", "PoolInfo CreatePool(string accountId, string token0, string token1)", "CreatePool", new string[] { tester.AccountId, "LYR", ticker });
		tester.CallRPC("add liquidaty to pool", "BalanceResult AddLiquidaty(string accountId, string token0, decimal token0Amount, string token1, decimal token1Amount)", "AddLiquidaty", new string[] { tester.AccountId, "LYR", "1000", ticker, "5000000" });
		(dynamic reply2, _) = tester.CallRPC("calculate price for pool", "SwapCalculator PoolCalculate(string poolId, string swapFrom, decimal amount, decimal slippage)", "PoolCalculate", new string[] { reply.result.poolId, "LYR", "100", "0.0001" });
		tester.CallRPC("swap from lyr", "BalanceResult Swap(string accountId, string token0, string token1, string tokenToSwap, decimal amountToSwap, decimal amountToGet)", "Swap", new[] { tester.AccountId, "LYR", ticker, "LYR", 100, reply2.result.MinimumReceived });
		tester.CallRPC("remove liquidaty from pool", "BalanceResult RemoveLiquidaty(string accountId, string token0, string token1)", "RemoveLiquidaty", new string[] { tester.AccountId, "LYR", ticker });
		tester.CallRPC("pool should be empty", "PoolInfo Pool(string token0, string token1)", "Pool", new string[] { "LYR", ticker });
		tester.CallRPC("add liquidaty to pool again", "BalanceResult AddLiquidaty(string accountId, string token0, decimal token0Amount, string token1, decimal token1Amount)", "AddLiquidaty", new string[] { tester.AccountId, "LYR", "300", ticker, "7000000" });
		tester.CallRPC("the new pool", "PoolInfo Pool(string token0, string token1)", "Pool", new string[] { "LYR", ticker });

		await Task.Delay(1000); // maybe some notify
		cancel.Cancel();
	}

}

public class JsWallet : LyraJsonRPCClient
{
	string _privateKey;
	string _accountId;
	public override string AccountId => _accountId;

	public JsWallet(string privateKey, string url, string networkId, CancellationTokenSource cancel) : base(url, networkId, cancel)
	{
		_privateKey = privateKey;
		_accountId = PortableSignatures.GetAccountIdFromPrivateKey(_privateKey);
	}

	public override string Sign(string msg)
	{
		return PortableSignatures.GetSignature(_privateKey, msg, _accountId);
	}

	public Dictionary<string, decimal> Balance
	{
		get
		{
			(var result, var error) = CallRPC("Balance", new string[] { AccountId });
			return result["result"]["balance"].ToObject<Dictionary<string, decimal>>();
		}		
	}
	
	public bool Send(string dstAccountId, string tokenName, decimal amount)
	{
		(var result, var error) = CallRPC("Send", new object[] { AccountId, amount, dstAccountId, tokenName });
		return result != null;
	}
	
	public bool Receive()
	{
		(var result, var error) = CallRPC("Receive", new string[] { AccountId });
		return result != null;
	}
	
	public bool CreateToken(string name, string domain, decimal supply)
	{
		(var result, var error) = CallRPC("Token", new object [] { AccountId, name, domain, supply });
		return result != null;
	}
	
	public void GetPool(string token0, string token1)
	{
		(var result, var error) = CallRPC("Pool", new string[] { token0, token1 });		
	}
}

public abstract class LyraJsonRPCClient
{
	string _url;
	WebSocket _socket;
	string _networkId;
	CancellationTokenSource _cancel;
	string _richPrivateKey = "dkrwRdqNjEEshpLuEPPqc6zM1HM3nzGjsYts39zzA1iUypcpj";	
	int _id;
	ManualResetEvent _done;
	JObject _data;
	JObject _error;
	
	public virtual string AccountId => throw new NotImplementedException();
	public bool IsReady => _socket?.State == WebSocketState.Open;
	
	public LyraJsonRPCClient(string url, string networkId, CancellationTokenSource cancel)
	{
		_url = url;
		_networkId = networkId;
		_cancel = cancel;
		
		_id = 1;
		_done = new ManualResetEvent(false);		
		Setup();
	}
	
	public void Done(JObject data, JObject error)
	{
		_data = data;
		_error = error;
		_done.Set();
	}
	
	public void WaitReply()
	{
		_done.WaitOne();
		_done.Reset();
	}
	
	public virtual string Sign(string msg)
	{
		throw new NotImplementedException();
	}
	
	public void CallRPC(object o, bool wait = true)
	{
		var json = JsonConvert.SerializeObject(o);
		WriteJson(json, wait);
	}
	
	public void WriteJson(string json, bool wait = true)
	{
		Console.WriteLine($"Client send:\n```");
		WriteFancy(json);
		_socket.Send(json);
		Console.WriteLine("```\n");
		if(wait)
			WaitReply();
	}

	public void WriteFancy(string json)
	{
		dynamic parsedJson = JsonConvert.DeserializeObject(json);
		var s = JsonConvert.SerializeObject(parsedJson, Newtonsoft.Json.Formatting.Indented);
		Console.WriteLine(s);
	}

	public (JObject result, JObject error) CallRPC(string api, object args, bool wait = true)
	{
		return CallRPC(null, null, api, args, wait);
	}
	public (JObject result, JObject error) CallRPC(string desc, string apiName, string api, object args, bool wait = true)
	{
		_data = null;
		_error = null;
		
		if(desc != null)
			Console.WriteLine($"# API: {apiName}\n/* {desc} */\n");
		CallRPC(new
		{
			jsonrpc = "2.0",
			method = api,
			id = _id++,
			@params = args
		}, wait);		
		Console.WriteLine();
		
		return (_data, _error);
	}
	
	private void Setup()
	{
		// note: reconnection handling needed.
		_socket = new WebSocket(_url, sslProtocols: SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls);
		_socket.Security.AllowUnstrustedCertificate = true;
		_socket.Security.AllowCertificateChainErrors = true;
		_socket.Security.AllowNameMismatchCertificate = true;
	
		_socket.Opened += (sender, e) =>
		{
			
		};
		_socket.MessageReceived += (sender, e) =>
		{
			Console.WriteLine($"Server reply:\n```");
			WriteFancy(e.Message);
			Console.WriteLine("```\n");
			dynamic data = JObject.Parse(e.Message);
			if (data.method == "Sign")
			{
				// string[] Sign (string type, string message)
				// for now type is always 'hash'.
				// in future maybe raw msg and need to hash it locally.
				var msg = data.@params[1].Value;
				Console.WriteLine($"## Signing message: {msg}\n");
				var sign = new[] { "p1393", Sign(msg) };
				CallRPC(new {
					jsonrpc = "2.0",
					id = data.id,
					result = sign
				}, false);
			}
			else if(data.method == "Notify")
			{
				Console.WriteLine("> Notify from server: " + e.Message);
			}
			else if (data.error != null)
			{
				Done(null, JObject.Parse(e.Message));
			}
			else
			{
				try
				{
					var result = data.result;
					Done(JObject.Parse(e.Message), null);
				}
				catch(Exception)
				{
					Console.WriteLine("Unsupported: " + e.Message);
				}
			}
		};

		_socket.Open();
	}
}

public static class PortableSignatures
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
		var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);

		//var publicKeyBytes = Base58Encoding.Decode(publicKey);
		//var publicKeyBytes = Base58Encoding.DecodeWithCheckSum(publicKey);
		//var publicKeyBytes = Base58Encoding.DecodePublicKey(publicKey);

		var byte0 = new byte[public_key_bytes.Length + 1];
		byte0[0] = 4;
		Array.Copy(public_key_bytes, 0, byte0, 1, public_key_bytes.Length);
		var q = curve.Curve.DecodePoint(byte0);

		var keyParameters = new
				Org.BouncyCastle.Crypto.Parameters.ECPublicKeyParameters(q,
				domain);

		ISigner signer = SignerUtilities.GetSigner("SHA-256withECDSA");

		signer.Init(false, keyParameters);
		signer.BlockUpdate(Encoding.UTF8.GetBytes(message), 0, message.Length);

		var signatureBytes = Base58Encoding.Decode(signature);
		var derSign = SignatureHelper.derSign(signatureBytes);
		return signer.VerifySignature(derSign);
	}

	public static string GetSignature(string privateKey, string message, string accountId)
	{
		return GetSignature(privateKey, message);
	}
	public static string GetSignature(string privateKey, string message)
	{
		var curve = SecNamedCurves.GetByName("secp256r1");
		var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);

		//byte[] pkbytes = Base58Encoding.Decode(privateKey);
		//byte[] pkbytes = Base58Encoding.DecodeWithCheckSum(privateKey);
		byte[] pkbytes = Base58Encoding.DecodePrivateKey(privateKey);

		var keyParameters = new
				ECPrivateKeyParameters(new BigInteger(1, pkbytes),
				domain);

		ISigner signer = SignerUtilities.GetSigner("SHA-256withECDSA");

		signer.Init(true, keyParameters);
		signer.BlockUpdate(Encoding.UTF8.GetBytes(message), 0, message.Length);
		var signature = signer.GenerateSignature();
		var netformat = SignatureHelper.ConvertDerToP1393(signature);
		return Base58Encoding.Encode(netformat);
	}

	private static byte[] DerivePublicKeyBytes(string privateKey)
	{
		var curve = SecNamedCurves.GetByName("secp256r1");
		var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);

		byte[] pkbytes = Base58Encoding.DecodePrivateKey(privateKey);
		var d = new BigInteger(1, pkbytes);
		var q = domain.G.Multiply(d);

		var publicKey = new ECPublicKeyParameters(q, domain);

		return publicKey.Q.GetEncoded(false);
	}

	public static string GetAccountIdFromPrivateKey(string privateKey)
	{
		byte[] public_key_bytes = DerivePublicKeyBytes(privateKey);
		return Base58Encoding.EncodeAccountId(public_key_bytes.Skip(1).ToArray());   // skip first byte which indicate compress or not.
	}

	public static string GetPublicKeyFromPrivateKey(string privateKey)
	{
		byte[] public_key_bytes = DerivePublicKeyBytes(privateKey);
		return Base58Encoding.EncodePublicKey(public_key_bytes);
	}

	public static string GeneratePrivateKey()
	{
		var privateKey = new byte[32];
		var rnd = System.Security.Cryptography.RandomNumberGenerator.Create();
		rnd.GetBytes(privateKey);
		return Base58Encoding.EncodePrivateKey(privateKey);
	}

	public static (string privateKey, string AccountId) GenerateWallet(byte[] keyData)
	{
		var pvtKeyStr = Base58Encoding.EncodePrivateKey(keyData);

		var pubKey = GetAccountIdFromPrivateKey(pvtKeyStr);
		return (pvtKeyStr, pubKey);
	}

	public static (string privateKey, string AccountId) GenerateWallet()
	{
		byte[] keyData = new byte[32];
		using (var rnd = System.Security.Cryptography.RandomNumberGenerator.Create())
		{
			rnd.GetBytes(keyData);
		}
		return GenerateWallet(keyData);
	}
}

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
		System.Numerics.BigInteger intData = 0;
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
		System.Numerics.BigInteger intData = 0;
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
}

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