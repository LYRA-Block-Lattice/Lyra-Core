<Query Kind="Program">
  <NuGetReference>Newtonsoft.Json</NuGetReference>
  <NuGetReference>WebSocket4Net</NuGetReference>
  <NuGetReference>Lyra.Data</NuGetReference>
  <Namespace>Lyra.Core.Accounts</Namespace>
  <Namespace>Lyra.Core.API</Namespace>
  <Namespace>Lyra.Data.Crypto</Namespace>
  <Namespace>Newtonsoft.Json</Namespace>
  <Namespace>Newtonsoft.Json.Linq</Namespace>
  <Namespace>System.Security.Authentication</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>WebSocket4Net</Namespace>
</Query>

async static Task Main(string[] args)
{
	//var networkId = "testnet";
	//var url = "wss://testnet.lyra.live/api/v1/socket";
	var networkId = "devnet";
	var url = "wss://api.devnet:4504/api/v1/socket";
	//var url = "wss://localhost:4504/api/v1/socket";

	var cancel = new CancellationTokenSource();
	// note: reconnection handling needed.
	var websocket = new WebSocket(url, sslProtocols: SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls);
	websocket.Security.AllowUnstrustedCertificate = true;
	websocket.Security.AllowCertificateChainErrors = true;
	websocket.Security.AllowNameMismatchCertificate = true;
	Tester t = new Tester(websocket, networkId, cancel);
	websocket.Opened += (sender, e) =>
	{
		Task.Run(async () => { await t.TestProc(); });
	};
	websocket.MessageReceived += (sender, e) =>
	{
		Console.WriteLine($"<--: ");
		t.WriteFancy(e.Message);
		dynamic data = JObject.Parse(e.Message);
		if (data.method == "Sign")
		{
			var msg = data.@params[0].Value;
			Console.WriteLine($"  Signing message: {msg}");
			var sign = t.Sign(msg);
			t.Send(new {
				jsonrpc = "2.0",
				id = data.id,
				result = sign
			}, false);
		}
		else if(data.result != null || data.error != null)
		{
			t.Done(data);
		}
	};

	websocket.Open();
	WaitHandle.WaitAny(new[] { cancel.Token.WaitHandle });
}

public class Tester
{
	WebSocket _socket;
	string _networkId;
	CancellationTokenSource _cancel;
	string _richPrivateKey = "dkrwRdqNjEEshpLuEPPqc6zM1HM3nzGjsYts39zzA1iUypcpj";
	string _privateKey;
	string _accountId;
	int _id;
	ManualResetEvent _done;
	dynamic _data;
	public Tester(WebSocket socket, string networkId, CancellationTokenSource cancel)
	{
		_socket = socket;
		_networkId = networkId;
		_cancel = cancel;
		
		(_privateKey, _accountId) = Signatures.GenerateWallet();
		_id = 1;
		_done = new ManualResetEvent(false);

		// if need to insepct.
		File.AppendAllText($"{Environment.ExpandEnvironmentVariables("%TEMP%")}\\testerkeys.txt", $"Tester {DateTime.Now} {_privateKey} {_accountId}");
	}
	
	public void Done(dynamic data)
	{
		_data = data;
		_done.Set();
	}
	
	public void WaitReply()
	{
		_done.WaitOne();
		_done.Reset();
	}
	
	public string Sign(string msg)
	{
		return Signatures.GetSignature(_privateKey, msg, _accountId);
	}
	
	public void Send(object o, bool wait = true)
	{
		var json = JsonConvert.SerializeObject(o);
		SendJson(json, wait);
	}
	
	public void SendJson(string json, bool wait = true)
	{
		Console.WriteLine($"-->: ");
		WriteFancy(json);
		_socket.Send(json);
		if(wait)
			WaitReply();
	}

	public void WriteFancy(string json)
	{
		dynamic parsedJson = JsonConvert.DeserializeObject(json);
		var s = JsonConvert.SerializeObject(parsedJson, Newtonsoft.Json.Formatting.Indented);
		Console.WriteLine(s);
	}

	public void CallRPC(string desc, string apiName, string api, object args, bool wait = true)
	{
		Console.WriteLine($"* {desc}. API: {apiName}");
		Send(new
		{
			jsonrpc = "2.0",
			method = api,
			id = _id++,
			@params = args
		}, wait);		
		Console.WriteLine();
	}
	
	private async Task<Wallet> GetRichWallet()
	{
		var store = new AccountInMemoryStorage();
		var wallet = Wallet.Create(store, "rich", "", _networkId, _richPrivateKey);
		wallet.NoConsole = true;
		var client = LyraRestClient.Create(_networkId, "Windows", "UnitTest", "1.0");
		await wallet.Sync(client);
		return wallet;
	}

	public async Task TestProc()
	{
		//var json = "{\"jsonrpc\":\"2.0\",\"method\":\"PoolCalculate\",\"id\": 123,\"params\":[\"LHEozCoPdfVHj2eohVUnivjEK3M5Z1vfzq6wR427mEg6kLTLLoTCMnbiyBT9CPMDst9fUYZaapHScXxYtHv9tWe5U2eoiR\", \"LYR\", 1000, 0.001]}";
		//SendJson(json);

		CallRPC("get status", "ApiStatus Status(string version, string networkid)", "Status", new string[] { "2.2", _networkId});		
		CallRPC("get status with error", "ApiStatus Status(string version, string networkid) with error", "Status", new string[] { "2.0", _networkId});	
		
		CallRPC("wallet get balance", "BalanceResult Balance(string accountId)", "Balance", new string[] { _accountId });	
		CallRPC("monitor receiving", "void Monitor(string accountId)", "Monitor", new string[] { _accountId }, false);	
		
		// someone send to this wallet
		var richGuy = await GetRichWallet();		
		await richGuy.Send(13000, _accountId);
		
		CallRPC("balance shows unreceived", "BalanceResult Balance(string accountId)", "Balance", new string[] { _accountId });
		CallRPC("receive it", "BalanceResult Receive(string accountId)", "Receive", new string[] { _accountId });
		
		CallRPC("send token", "BalanceResult Send(string accountId, decimal amount, string destAccount, string ticker)", "Send", new string[] { _accountId, "10", richGuy.AccountId, "LYR"});

		var r = new Random(); var tokenName = $"json-{r.Next(10000, 10000000)}";
		CallRPC("create a token", "BalanceResult Token(string accountId, string name, string domain, decimal supply)", "Token", new string[] { _accountId, tokenName, "testit", "10000000" });
		CallRPC("show all transaction history", "List<TransactionDescription> History(string accountId, long startTime, long endTime, int count)", "History", new string[] { _accountId, "0", DateTime.UtcNow.Ticks.ToString(), "100" });

		// doing pool stuff
		var ticker = $"testit/{tokenName}";
		CallRPC("get pool info", "PoolInfo Pool(string token0, string token1)", "Pool", new string[] {"LYR", ticker});
		CallRPC("create a pool", "PoolInfo CreatePool(string accountId, string token0, string token1)", "CreatePool", new string[] {_accountId, "LYR", ticker});
		CallRPC("add liquidaty to pool", "BalanceResult AddLiquidaty(string accountId, string token0, decimal token0Amount, string token1, decimal token1Amount)", "AddLiquidaty", new string[] {_accountId, "LYR", "1000", ticker, "5000000"});
		CallRPC("calculate price for pool", "SwapCalculator PoolCalculate(string poolId, string swapFrom, decimal amount, decimal slippage)", "PoolCalculate", new string[] { _data.result.poolId, "LYR", "100", "0.0001"});
		CallRPC("swap from lyr", "BalanceResult Swap(string accountId, string token0, string token1, string tokenToSwap, decimal amountToSwap, decimal amountToGet)", "Swap", new [] {_accountId, "LYR", ticker, "LYR", 100, _data.result.MinimumReceived});
		CallRPC("remove liquidaty from pool", "BalanceResult RemoveLiquidaty(string accountId, string token0, string token1)", "RemoveLiquidaty", new string[] {_accountId, "LYR", ticker});
		CallRPC("pool should be empty", "PoolInfo Pool(string token0, string token1)", "Pool", new string[] { "LYR", ticker });
		CallRPC("add liquidaty to pool again", "BalanceResult AddLiquidaty(string accountId, string token0, decimal token0Amount, string token1, decimal token1Amount)", "AddLiquidaty", new string[] { _accountId, "LYR", "300", ticker, "7000000" });
		CallRPC("the new pool", "PoolInfo Pool(string token0, string token1)", "Pool", new string[] { "LYR", ticker });
		
		await Task.Delay(1000);	// maybe some notify
		_cancel.Cancel();
	}
}