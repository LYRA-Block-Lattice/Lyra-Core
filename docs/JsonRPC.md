# API: ApiStatus Status(string version, string networkid)
/* get status */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "Status",
  "id": 1,
  "params": [
    "2.2.0.0",
    "devnet"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "version": "2.2.0.1",
    "networkid": "devnet",
    "synced": true
  }
}
```


# API: ApiStatus Status(string version, string networkid) with error
/* get status with error */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "Status",
  "id": 2,
  "params": [
    "2.0",
    "devnet"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 2,
  "error": {
    "data": {
      "type": "System.Exception",
      "message": "Client version too low. Need upgrade.",
      "stack": "   at Lyra.Node.JsonRpcServer.Status(String version, String networkid) in C:\\Users\\Wizard\\source\\repos\\LyraNetwork\\Core\\Lyra.Node2\\Services\\JsonRpcServer.cs:line 81",
      "code": -2146233088,
      "inner": null
    },
    "code": -32000,
    "message": "Client version too low. Need upgrade."
  }
}
```


# API: BalanceResult Balance(string accountId)
/* wallet get balance */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "Balance",
  "id": 3,
  "params": [
    "L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": {
    "unreceived": false
  }
}
```


# API: void Monitor(string accountId)
/* monitor receiving */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "Monitor",
  "id": 4,
  "params": [
    "L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 4,
  "result": null
}
```



> rich guy is sending you token...

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "Send",
  "id": 1,
  "params": [
    "LUTG2E1mdpGk5Qtq9BUgwZDWhUeZc14Xfw2pAvAdKoacvgRBU3atwtrQeoY3evm5C7TXRz3Q5nwPEUHj9p7CBDE6kQTQMy",
    13000.0,
    "L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu",
    "LYR"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "Sign",
  "params": [
    "hash",
    "8gaibuRvkLk2ND5yoZEaWyAvFGsVPVA6S85Cer852koT"
  ]
}
```

## Signing message: 8gaibuRvkLk2ND5yoZEaWyAvFGsVPVA6S85Cer852koT

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": [
    "p1393",
    "4jBkv6QHvJ4su1xdUXpUQfcEgWJ1wjMrf18oiE1zNjdF18TCtjsY34LLhjzh9JTorK8ktRNSq97syn3otFdDR6Kz"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "method": "Notify",
  "params": [
    {
      "catalog": "Receiving",
      "content": {
        "from": "LUTG2E1mdpGk5Qtq9BUgwZDWhUeZc14Xfw2pAvAdKoacvgRBU3atwtrQeoY3evm5C7TXRz3Q5nwPEUHj9p7CBDE6kQTQMy",
        "to": "L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu",
        "sendHash": "8gaibuRvkLk2ND5yoZEaWyAvFGsVPVA6S85Cer852koT",
        "funds": {
          "LYR": 13000.0
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Receiving","content":{"from":"LUTG2E1mdpGk5Qtq9BUgwZDWhUeZc14Xfw2pAvAdKoacvgRBU3atwtrQeoY3evm5C7TXRz3Q5nwPEUHj9p7CBDE6kQTQMy","to":"L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu","sendHash":"8gaibuRvkLk2ND5yoZEaWyAvFGsVPVA6S85Cer852koT","funds":{"LYR":13000.0}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "balance": {
      "LYR": 33670.46319139,
      "testit/json-5963500": 3000000.0,
      "unittest/trans": 49999991619.64198
    },
    "unreceived": true
  }
}
```


# API: BalanceResult Balance(string accountId)
/* balance shows unreceived */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "Balance",
  "id": 5,
  "params": [
    "L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 5,
  "result": {
    "unreceived": true
  }
}
```


# API: BalanceResult Receive(string accountId)
/* receive it */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "Receive",
  "id": 6,
  "params": [
    "L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "Sign",
  "params": [
    "hash",
    "3trpKu99XyAwekLJKRcB9cLTZZyR4MNADsPyZc92aLJD"
  ]
}
```

## Signing message: 3trpKu99XyAwekLJKRcB9cLTZZyR4MNADsPyZc92aLJD

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": [
    "p1393",
    "486oyravCzquoAgdQQBGhu5183dRQaoZTFDeVa4aF6dxUXAa3asMm3svwiVpoKkHqhjGHB94xYnyghfQrWTMYVYR"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "method": "Notify",
  "params": [
    {
      "catalog": "Settlement",
      "content": {
        "recvHash": "3trpKu99XyAwekLJKRcB9cLTZZyR4MNADsPyZc92aLJD",
        "to": "L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu",
        "sendHash": "8gaibuRvkLk2ND5yoZEaWyAvFGsVPVA6S85Cer852koT",
        "funds": {
          "LYR": 13000.0
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Settlement","content":{"recvHash":"3trpKu99XyAwekLJKRcB9cLTZZyR4MNADsPyZc92aLJD","to":"L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu","sendHash":"8gaibuRvkLk2ND5yoZEaWyAvFGsVPVA6S85Cer852koT","funds":{"LYR":13000.0}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 6,
  "result": {
    "balance": {
      "LYR": 13000.0
    },
    "unreceived": false
  }
}
```


# API: BalanceResult Send(string accountId, decimal amount, string destAccount, string ticker)
/* send token */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "Send",
  "id": 7,
  "params": [
    "L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu",
    "10",
    "LUTG2E1mdpGk5Qtq9BUgwZDWhUeZc14Xfw2pAvAdKoacvgRBU3atwtrQeoY3evm5C7TXRz3Q5nwPEUHj9p7CBDE6kQTQMy",
    "LYR"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "Sign",
  "params": [
    "hash",
    "FjaiYQkpS3JXd6FcN5Fk5WpYR39ahbSAd3AzzNMBSudG"
  ]
}
```

## Signing message: FjaiYQkpS3JXd6FcN5Fk5WpYR39ahbSAd3AzzNMBSudG

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": [
    "p1393",
    "44UmYx2Bc6jvbC2TicVoPX2Cx2Aab5niogPfsmWPyyFAoqSbaHzftvjUYXkNH72L4pACQJqbpfLM7FtfWLZJWhXd"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 7,
  "result": {
    "balance": {
      "LYR": 12989.0
    },
    "unreceived": false
  }
}
```


# API: BalanceResult Token(string accountId, string name, string domain, decimal supply)
/* create a token */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "Token",
  "id": 8,
  "params": [
    "L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu",
    "json-160771",
    "testit",
    "10000000"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 4,
  "method": "Sign",
  "params": [
    "hash",
    "3KGb4x1S4svKtctc4UchXZwi6j3rT6HXrwKg86xuMTuN"
  ]
}
```

## Signing message: 3KGb4x1S4svKtctc4UchXZwi6j3rT6HXrwKg86xuMTuN

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 4,
  "result": [
    "p1393",
    "Qqo9wbHPLANdRWyEcK4GWtru3VVFn5zoProvW8cMjgrDhcs5NqmtsatEdEYFLyYTFuffxqu3G17LY8WyLoApoKz"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "method": "Notify",
  "params": [
    {
      "catalog": "Settlement",
      "content": {
        "recvHash": "3KGb4x1S4svKtctc4UchXZwi6j3rT6HXrwKg86xuMTuN",
        "to": "L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu",
        "funds": {
          "testit/json-160771": 10000000.0,
          "LYR": -10000.0
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Settlement","content":{"recvHash":"3KGb4x1S4svKtctc4UchXZwi6j3rT6HXrwKg86xuMTuN","to":"L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu","funds":{"testit/json-160771":10000000.0,"LYR":-10000.0}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 8,
  "result": {
    "balance": {
      "testit/json-160771": 10000000.0,
      "LYR": 2989.0
    },
    "unreceived": false
  }
}
```


# API: List<TransactionDescription> History(string accountId, long startTime, long endTime, int count)
/* show all transaction history */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "History",
  "id": 9,
  "params": [
    "L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu",
    "0",
    "1616041996735",
    "100"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 9,
  "result": [
    {
      "Height": 1,
      "IsReceive": true,
      "TimeStamp": 1616041996137,
      "SendAccountId": "LUTG2E1mdpGk5Qtq9BUgwZDWhUeZc14Xfw2pAvAdKoacvgRBU3atwtrQeoY3evm5C7TXRz3Q5nwPEUHj9p7CBDE6kQTQMy",
      "SendHash": "8gaibuRvkLk2ND5yoZEaWyAvFGsVPVA6S85Cer852koT",
      "RecvAccountId": "L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu",
      "RecvHash": "3trpKu99XyAwekLJKRcB9cLTZZyR4MNADsPyZc92aLJD",
      "Changes": {
        "LYR": "13000"
      },
      "Balances": {
        "LYR": "13000"
      }
    },
    {
      "Height": 2,
      "IsReceive": false,
      "TimeStamp": 1616041996344,
      "SendAccountId": "L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu",
      "SendHash": "FjaiYQkpS3JXd6FcN5Fk5WpYR39ahbSAd3AzzNMBSudG",
      "RecvAccountId": "LUTG2E1mdpGk5Qtq9BUgwZDWhUeZc14Xfw2pAvAdKoacvgRBU3atwtrQeoY3evm5C7TXRz3Q5nwPEUHj9p7CBDE6kQTQMy",
      "Changes": {
        "LYR": "-11"
      },
      "Balances": {
        "LYR": "12989"
      }
    },
    {
      "Height": 3,
      "IsReceive": true,
      "TimeStamp": 1616041996523,
      "RecvAccountId": "L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu",
      "RecvHash": "3KGb4x1S4svKtctc4UchXZwi6j3rT6HXrwKg86xuMTuN",
      "Changes": {
        "testit/json-160771": "10000000",
        "LYR": "-10000"
      },
      "Balances": {
        "testit/json-160771": "10000000",
        "LYR": "2989"
      }
    }
  ]
}
```


# API: PoolInfo Pool(string token0, string token1)
/* get pool info */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "Pool",
  "id": 10,
  "params": [
    "LYR",
    "testit/json-160771"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 10,
  "error": {
    "data": {
      "type": "System.Exception",
      "message": "Failed to get pool",
      "stack": "   at Lyra.Node.JsonRpcServer.Pool(String token0, String token1) in C:\\Users\\Wizard\\source\\repos\\LyraNetwork\\Core\\Lyra.Node2\\Services\\JsonRpcServer.cs:line 218",
      "code": -2146233088,
      "inner": null
    },
    "code": -32000,
    "message": "Failed to get pool"
  }
}
```


# API: PoolInfo CreatePool(string accountId, string token0, string token1)
/* create a pool */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "CreatePool",
  "id": 11,
  "params": [
    "L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu",
    "LYR",
    "testit/json-160771"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 5,
  "method": "Sign",
  "params": [
    "hash",
    "A1AbjLuNY3jmiQXVpcW2BdQXcftbJ5MEv972obdonRXY"
  ]
}
```

## Signing message: A1AbjLuNY3jmiQXVpcW2BdQXcftbJ5MEv972obdonRXY

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 5,
  "result": [
    "p1393",
    "2pcMUNPPDJBSejFbjwm7jPSxF77GBS9z3v66GA5ZvEAft55zZt6viBLABGXnGVyH1EoL3PJDg9ete5264YFz974C"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 11,
  "result": {
    "poolId": "LSChMwMB4e69JmJDmSnP3gfLnibsoZi2tMTJnmkeM5r492hpaN8mXnS6K97ePi8w4d6TAt2GDnmbE6DiENCskn44r18Ch7",
    "height": 1,
    "token0": "LYR",
    "token1": "testit/json-160771",
    "balance": {}
  }
}
```


# API: BalanceResult AddLiquidaty(string accountId, string token0, decimal token0Amount, string token1, decimal token1Amount)
/* add liquidaty to pool */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "AddLiquidaty",
  "id": 12,
  "params": [
    "L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu",
    "LYR",
    "1000",
    "testit/json-160771",
    "5000000"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 6,
  "method": "Sign",
  "params": [
    "hash",
    "9omr3RaRiTnJ4yhDbCJXiQFgwjHM1igDFSepmqPXFKf6"
  ]
}
```

## Signing message: 9omr3RaRiTnJ4yhDbCJXiQFgwjHM1igDFSepmqPXFKf6

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 6,
  "result": [
    "p1393",
    "QDMkmN7quRaCWSEgofsDA2qxuG2mnW6Mwu5vZnu8Dsvb6CMYJV8hdhAAaHipKdmNH2MPjAAwtY1s9VtF64rJbnL"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 12,
  "result": {
    "poolId": "LSChMwMB4e69JmJDmSnP3gfLnibsoZi2tMTJnmkeM5r492hpaN8mXnS6K97ePi8w4d6TAt2GDnmbE6DiENCskn44r18Ch7",
    "height": 2,
    "token0": "LYR",
    "token1": "testit/json-160771",
    "balance": {
      "testit/json-160771": 5000000.0,
      "LYR": 1000.0
    }
  }
}
```


# API: SwapCalculator PoolCalculate(string poolId, string swapFrom, decimal amount, decimal slippage)
/* calculate price for pool */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "PoolCalculate",
  "id": 13,
  "params": [
    "LSChMwMB4e69JmJDmSnP3gfLnibsoZi2tMTJnmkeM5r492hpaN8mXnS6K97ePi8w4d6TAt2GDnmbE6DiENCskn44r18Ch7",
    "LYR",
    "100",
    "0.0001"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 13,
  "result": {
    "ProviderFee": 0.003,
    "ProtocolFee": 0.001,
    "SwapInToken": "LYR",
    "SwapInAmount": 100.0,
    "SwapOutToken": "testit/json-160771",
    "SwapOutAmount": 452891.96071299,
    "Price": 0.0002208032128514,
    "PriceImpact": 0.09057839,
    "MinimumReceived": 452846.67151692,
    "PayToProvider": 0.3,
    "PayToAuthorizer": 0.1
  }
}
```


# API: BalanceResult Swap(string accountId, string token0, string token1, string tokenToSwap, decimal amountToSwap, decimal amountToGet)
/* swap from lyr */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "Swap",
  "id": 14,
  "params": [
    "L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu",
    "LYR",
    "testit/json-160771",
    "LYR",
    100,
    452846.67151692
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 7,
  "method": "Sign",
  "params": [
    "hash",
    "38ZoTBDWGf5AneXSxo4eELFetbWn8jL2SDsN4uQGvZPM"
  ]
}
```

## Signing message: 38ZoTBDWGf5AneXSxo4eELFetbWn8jL2SDsN4uQGvZPM

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 7,
  "result": [
    "p1393",
    "23ASxuDF1WUSSyP2HGfU3DzrQBWUivnsfRqW248G8kkMtESzM15jDR8NXdGE1b3GjXmk5GXcZMdxoDAmw6jkztge"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "method": "Notify",
  "params": [
    {
      "catalog": "Receiving",
      "content": {
        "from": "LSChMwMB4e69JmJDmSnP3gfLnibsoZi2tMTJnmkeM5r492hpaN8mXnS6K97ePi8w4d6TAt2GDnmbE6DiENCskn44r18Ch7",
        "to": "L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu",
        "sendHash": "HgKZbf2HEjzPepd9Sysk41yZ9dsB5MXdo4gBwjJhwa56",
        "funds": {
          "testit/json-160771": 452891.96071299
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Receiving","content":{"from":"LSChMwMB4e69JmJDmSnP3gfLnibsoZi2tMTJnmkeM5r492hpaN8mXnS6K97ePi8w4d6TAt2GDnmbE6DiENCskn44r18Ch7","to":"L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu","sendHash":"HgKZbf2HEjzPepd9Sysk41yZ9dsB5MXdo4gBwjJhwa56","funds":{"testit/json-160771":452891.96071299}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 8,
  "method": "Sign",
  "params": [
    "hash",
    "9raZRYkj9QEnNrD9zbRipTPurmgG63jWoqPfrZvypGz8"
  ]
}
```

## Signing message: 9raZRYkj9QEnNrD9zbRipTPurmgG63jWoqPfrZvypGz8

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 8,
  "result": [
    "p1393",
    "3EFzueGMSmNGEEfCmPvRLGks5cMTN7E7mamui83D23AFsMwq4VHfQ9JWTfxKCbAgLH7wDfwbdqDwEqHMNwafZz47"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "method": "Notify",
  "params": [
    {
      "catalog": "Settlement",
      "content": {
        "recvHash": "9raZRYkj9QEnNrD9zbRipTPurmgG63jWoqPfrZvypGz8",
        "to": "L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu",
        "sendHash": "HgKZbf2HEjzPepd9Sysk41yZ9dsB5MXdo4gBwjJhwa56",
        "funds": {
          "testit/json-160771": 452891.96071299
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Settlement","content":{"recvHash":"9raZRYkj9QEnNrD9zbRipTPurmgG63jWoqPfrZvypGz8","to":"L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu","sendHash":"HgKZbf2HEjzPepd9Sysk41yZ9dsB5MXdo4gBwjJhwa56","funds":{"testit/json-160771":452891.96071299}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 14,
  "result": {
    "balance": {
      "testit/json-160771": 5452891.96071299,
      "LYR": 886.0
    },
    "unreceived": false
  }
}
```


# API: BalanceResult RemoveLiquidaty(string accountId, string token0, string token1)
/* remove liquidaty from pool */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "RemoveLiquidaty",
  "id": 15,
  "params": [
    "L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu",
    "LYR",
    "testit/json-160771"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 9,
  "method": "Sign",
  "params": [
    "hash",
    "AZNxro1sJFAwLKRCr3986iZScDaE1mNQnCLxG9VSmHxv"
  ]
}
```

## Signing message: AZNxro1sJFAwLKRCr3986iZScDaE1mNQnCLxG9VSmHxv

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 9,
  "result": [
    "p1393",
    "Jm9ZQpLkQRZPjWQ3Z4JC2fP7LquyeAcdmTAAvaJvvQo4y9THukfeCze4tn3bMfLvqoXEVzb9VBtaHowCCSC6bbE"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "method": "Notify",
  "params": [
    {
      "catalog": "Receiving",
      "content": {
        "from": "LSChMwMB4e69JmJDmSnP3gfLnibsoZi2tMTJnmkeM5r492hpaN8mXnS6K97ePi8w4d6TAt2GDnmbE6DiENCskn44r18Ch7",
        "to": "L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu",
        "sendHash": "5S1PtARc4R9Aqt21dD3sJzfqhU3AT5rD2tTA4qQtETpZ",
        "funds": {
          "testit/json-160771": 4547108.03928701,
          "LYR": 1099.9
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Receiving","content":{"from":"LSChMwMB4e69JmJDmSnP3gfLnibsoZi2tMTJnmkeM5r492hpaN8mXnS6K97ePi8w4d6TAt2GDnmbE6DiENCskn44r18Ch7","to":"L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu","sendHash":"5S1PtARc4R9Aqt21dD3sJzfqhU3AT5rD2tTA4qQtETpZ","funds":{"testit/json-160771":4547108.03928701,"LYR":1099.9}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 10,
  "method": "Sign",
  "params": [
    "hash",
    "5J8bndRnRTwrLVtPkSQ6W7EzuFkYnBgqrNtqoxMrds9d"
  ]
}
```

## Signing message: 5J8bndRnRTwrLVtPkSQ6W7EzuFkYnBgqrNtqoxMrds9d

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 10,
  "result": [
    "p1393",
    "5ahg3petjYxmC6yXJsTBrkb4G6HnTcqYuMVHLbHseAKgWNL4wbKzPVm1bFGQZcEUdzV7btAkZ6A73D9EouS4EKXB"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "method": "Notify",
  "params": [
    {
      "catalog": "Settlement",
      "content": {
        "recvHash": "5J8bndRnRTwrLVtPkSQ6W7EzuFkYnBgqrNtqoxMrds9d",
        "to": "L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu",
        "sendHash": "5S1PtARc4R9Aqt21dD3sJzfqhU3AT5rD2tTA4qQtETpZ",
        "funds": {
          "testit/json-160771": 4547108.03928701,
          "LYR": 1099.9
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Settlement","content":{"recvHash":"5J8bndRnRTwrLVtPkSQ6W7EzuFkYnBgqrNtqoxMrds9d","to":"L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu","sendHash":"5S1PtARc4R9Aqt21dD3sJzfqhU3AT5rD2tTA4qQtETpZ","funds":{"testit/json-160771":4547108.03928701,"LYR":1099.9}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 15,
  "result": {
    "balance": {
      "testit/json-160771": 10000000.0,
      "LYR": 1983.9
    },
    "unreceived": false
  }
}
```


# API: PoolInfo Pool(string token0, string token1)
/* pool should be empty */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "Pool",
  "id": 16,
  "params": [
    "LYR",
    "testit/json-160771"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 16,
  "result": {
    "poolId": "LSChMwMB4e69JmJDmSnP3gfLnibsoZi2tMTJnmkeM5r492hpaN8mXnS6K97ePi8w4d6TAt2GDnmbE6DiENCskn44r18Ch7",
    "height": 5,
    "token0": "LYR",
    "token1": "testit/json-160771",
    "balance": {
      "testit/json-160771": 0.0,
      "LYR": 0.0
    }
  }
}
```


# API: BalanceResult AddLiquidaty(string accountId, string token0, decimal token0Amount, string token1, decimal token1Amount)
/* add liquidaty to pool again */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "AddLiquidaty",
  "id": 17,
  "params": [
    "L8prCJqotXE6ZGQjTRaiqC1UP6MtQBqhjk6P1mpqh1VaLyfCBtKZ3E1fHpgaCevYs5Zi3DorMGFJDSPByJJ7XXTJQEjFmu",
    "LYR",
    "300",
    "testit/json-160771",
    "7000000"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 11,
  "method": "Sign",
  "params": [
    "hash",
    "5qAj1W5wTaVXaUiGtWXU8tNQHAixk3XpdoR2zXnHuMBM"
  ]
}
```

## Signing message: 5qAj1W5wTaVXaUiGtWXU8tNQHAixk3XpdoR2zXnHuMBM

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 11,
  "result": [
    "p1393",
    "42YjmE35SVksAsWcJL3Zby6LNKqHnSrUWubBy8vEu3AM1p2UkoKDeWp8VDwxKo1hHKvZAWPQ1pRNjrS6Q28aLCkB"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 17,
  "result": {
    "poolId": "LSChMwMB4e69JmJDmSnP3gfLnibsoZi2tMTJnmkeM5r492hpaN8mXnS6K97ePi8w4d6TAt2GDnmbE6DiENCskn44r18Ch7",
    "height": 6,
    "token0": "LYR",
    "token1": "testit/json-160771",
    "balance": {
      "testit/json-160771": 7000000.0,
      "LYR": 300.0
    }
  }
}
```


# API: PoolInfo Pool(string token0, string token1)
/* the new pool */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "Pool",
  "id": 18,
  "params": [
    "LYR",
    "testit/json-160771"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 18,
  "result": {
    "poolId": "LSChMwMB4e69JmJDmSnP3gfLnibsoZi2tMTJnmkeM5r492hpaN8mXnS6K97ePi8w4d6TAt2GDnmbE6DiENCskn44r18Ch7",
    "height": 6,
    "token0": "LYR",
    "token1": "testit/json-160771",
    "balance": {
      "testit/json-160771": 7000000.0,
      "LYR": 300.0
    }
  }
}
```



