# API: ApiStatus Status(string version, string networkid)
/* get status */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "Status",
  "id": 1,
  "params": [
    "2.2",
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
    "version": "2.1.0.18",
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
      "stack": "   at Lyra.Node.JsonRpcServer.Status(String version, String networkid) in C:\\Users\\Wizard\\source\\repos\\LyraNetwork\\Core\\Lyra.Node2\\Services\\JsonRpcServer.cs:line 95",
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
    "LMCDi9rJ5xMWZb8vgW68e9zvwrkWqBZw9JUcDCPA8JxotLZNxfGUPHHeDGmjUPg1nkVmiX4VfXKcctBdmH5avXs4ohWKe3"
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
    "LMCDi9rJ5xMWZb8vgW68e9zvwrkWqBZw9JUcDCPA8JxotLZNxfGUPHHeDGmjUPg1nkVmiX4VfXKcctBdmH5avXs4ohWKe3"
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
    "LMCDi9rJ5xMWZb8vgW68e9zvwrkWqBZw9JUcDCPA8JxotLZNxfGUPHHeDGmjUPg1nkVmiX4VfXKcctBdmH5avXs4ohWKe3",
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
    "9tm36U6qwvHZweTGwDny7RxSxLXYb7uzzNiWsLDtmV1Y"
  ]
}
```

## Signing message: 9tm36U6qwvHZweTGwDny7RxSxLXYb7uzzNiWsLDtmV1Y

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": [
    "p1393",
    "4YPfKhaugsMhyoGY73X6VYKUnLRkboAtGfohf1oKpqWZov5dNpmBkwqRgdhx7CnCmauUEKNgcAzoR911qQEL5gfX"
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
        "sendHash": "9tm36U6qwvHZweTGwDny7RxSxLXYb7uzzNiWsLDtmV1Y",
        "funds": {
          "LYR": 13000.0
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Receiving","content":{"from":"LUTG2E1mdpGk5Qtq9BUgwZDWhUeZc14Xfw2pAvAdKoacvgRBU3atwtrQeoY3evm5C7TXRz3Q5nwPEUHj9p7CBDE6kQTQMy","sendHash":"9tm36U6qwvHZweTGwDny7RxSxLXYb7uzzNiWsLDtmV1Y","funds":{"LYR":13000.0}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "balance": {
      "LYR": 124677.46319139,
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
    "LMCDi9rJ5xMWZb8vgW68e9zvwrkWqBZw9JUcDCPA8JxotLZNxfGUPHHeDGmjUPg1nkVmiX4VfXKcctBdmH5avXs4ohWKe3"
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
    "LMCDi9rJ5xMWZb8vgW68e9zvwrkWqBZw9JUcDCPA8JxotLZNxfGUPHHeDGmjUPg1nkVmiX4VfXKcctBdmH5avXs4ohWKe3"
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
    "12KPxpakmRG7WH49iGDdzXbXjfBSmYmheG66v5SGb2mk"
  ]
}
```

## Signing message: 12KPxpakmRG7WH49iGDdzXbXjfBSmYmheG66v5SGb2mk

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": [
    "p1393",
    "4Hc5KmZjGZNnGpxFy4t58G2XKD17ZWJa32eTi6aG1j4W8cSt6QU5ArQNopRk2pt3RzasZ1KUwZn1Z8jBC1Tup96x"
  ]
}
```

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
    "LMCDi9rJ5xMWZb8vgW68e9zvwrkWqBZw9JUcDCPA8JxotLZNxfGUPHHeDGmjUPg1nkVmiX4VfXKcctBdmH5avXs4ohWKe3",
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
    "Hz4k1JBoHCpqAf82qKNaUrdu28yj1Ne5UDwATn8eVLqf"
  ]
}
```

## Signing message: Hz4k1JBoHCpqAf82qKNaUrdu28yj1Ne5UDwATn8eVLqf

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": [
    "p1393",
    "Wzi4rdaRK1KnfXmYENiLwz1HC1144WUt4jqbFy8zXeSp6THNbG8w2XUfD7zgvfF72ikqCAy359WFboAAJ7PMD8F"
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
    "LMCDi9rJ5xMWZb8vgW68e9zvwrkWqBZw9JUcDCPA8JxotLZNxfGUPHHeDGmjUPg1nkVmiX4VfXKcctBdmH5avXs4ohWKe3",
    "json-1031181",
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
    "6UMgMd8Sm6T2YveniiQPE3eY9NWf25VBD6TBAoQQboyc"
  ]
}
```

## Signing message: 6UMgMd8Sm6T2YveniiQPE3eY9NWf25VBD6TBAoQQboyc

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 4,
  "result": [
    "p1393",
    "R2VFtg9pBntEnn8er71mGmJXC1SR2Jd8P98aPwpD281mke5t7EyhJttQ7itYcmP4Punjs4rh7Qk84nP8xsQMi8k"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 8,
  "result": {
    "balance": {
      "testit/json-1031181": 10000000.0,
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
    "LMCDi9rJ5xMWZb8vgW68e9zvwrkWqBZw9JUcDCPA8JxotLZNxfGUPHHeDGmjUPg1nkVmiX4VfXKcctBdmH5avXs4ohWKe3",
    "0",
    "1615833727282",
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
      "TimeStamp": 1615833726585,
      "SendAccountId": "LUTG2E1mdpGk5Qtq9BUgwZDWhUeZc14Xfw2pAvAdKoacvgRBU3atwtrQeoY3evm5C7TXRz3Q5nwPEUHj9p7CBDE6kQTQMy",
      "SendHash": "9tm36U6qwvHZweTGwDny7RxSxLXYb7uzzNiWsLDtmV1Y",
      "RecvAccountId": "LMCDi9rJ5xMWZb8vgW68e9zvwrkWqBZw9JUcDCPA8JxotLZNxfGUPHHeDGmjUPg1nkVmiX4VfXKcctBdmH5avXs4ohWKe3",
      "RecvHash": "12KPxpakmRG7WH49iGDdzXbXjfBSmYmheG66v5SGb2mk",
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
      "TimeStamp": 1615833726847,
      "SendAccountId": "LMCDi9rJ5xMWZb8vgW68e9zvwrkWqBZw9JUcDCPA8JxotLZNxfGUPHHeDGmjUPg1nkVmiX4VfXKcctBdmH5avXs4ohWKe3",
      "SendHash": "Hz4k1JBoHCpqAf82qKNaUrdu28yj1Ne5UDwATn8eVLqf",
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
      "TimeStamp": 1615833727057,
      "RecvAccountId": "LMCDi9rJ5xMWZb8vgW68e9zvwrkWqBZw9JUcDCPA8JxotLZNxfGUPHHeDGmjUPg1nkVmiX4VfXKcctBdmH5avXs4ohWKe3",
      "RecvHash": "6UMgMd8Sm6T2YveniiQPE3eY9NWf25VBD6TBAoQQboyc",
      "Changes": {
        "testit/json-1031181": "10000000",
        "LYR": "-10000"
      },
      "Balances": {
        "testit/json-1031181": "10000000",
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
    "testit/json-1031181"
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
      "stack": "   at Lyra.Node.JsonRpcServer.Pool(String token0, String token1) in C:\\Users\\Wizard\\source\\repos\\LyraNetwork\\Core\\Lyra.Node2\\Services\\JsonRpcServer.cs:line 220",
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
    "LMCDi9rJ5xMWZb8vgW68e9zvwrkWqBZw9JUcDCPA8JxotLZNxfGUPHHeDGmjUPg1nkVmiX4VfXKcctBdmH5avXs4ohWKe3",
    "LYR",
    "testit/json-1031181"
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
    "owMmZcfnQS1LDT16FNtBdpAA8KWghg5j4DjCJjrTgqw"
  ]
}
```

## Signing message: owMmZcfnQS1LDT16FNtBdpAA8KWghg5j4DjCJjrTgqw

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 5,
  "result": [
    "p1393",
    "u99F9fTvx5yaynPqXSZ1WEsQD3AUtBpsawFEyQ2TqYvA7RWmUj6Jph8Dy7nURTeM2j5u9P3b9oaCyuYFRA5MMbi"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 11,
  "result": {
    "poolId": "LDFj4M5QwzQMg7rsHQuLFcgnMXgiatsBCu7wvhJWBnhWnxQA3F3r1PY8vromAGxLvKwQPtszAcWPMp4KCnPhBjRzDMkyhT",
    "height": 1,
    "token0": "LYR",
    "token1": "testit/json-1031181",
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
    "LMCDi9rJ5xMWZb8vgW68e9zvwrkWqBZw9JUcDCPA8JxotLZNxfGUPHHeDGmjUPg1nkVmiX4VfXKcctBdmH5avXs4ohWKe3",
    "LYR",
    "1000",
    "testit/json-1031181",
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
    "BR7mLz9sWL9L54yuxWxpyEcJCVgSWazsHpSMYegBEwbo"
  ]
}
```

## Signing message: BR7mLz9sWL9L54yuxWxpyEcJCVgSWazsHpSMYegBEwbo

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 6,
  "result": [
    "p1393",
    "46QTv6Kgg4XypvuGaT1k2amtpSbinNNXsswtWGQbxBCZbxCRMh9ztj1Fm16QjvvT2yP1mG2pGatSJoMa4GF4PwLc"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 12,
  "result": {
    "poolId": "LDFj4M5QwzQMg7rsHQuLFcgnMXgiatsBCu7wvhJWBnhWnxQA3F3r1PY8vromAGxLvKwQPtszAcWPMp4KCnPhBjRzDMkyhT",
    "height": 2,
    "token0": "LYR",
    "token1": "testit/json-1031181",
    "balance": {
      "testit/json-1031181": 5000000.0,
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
    "LDFj4M5QwzQMg7rsHQuLFcgnMXgiatsBCu7wvhJWBnhWnxQA3F3r1PY8vromAGxLvKwQPtszAcWPMp4KCnPhBjRzDMkyhT",
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
    "SwapOutToken": "testit/json-1031181",
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
    "LMCDi9rJ5xMWZb8vgW68e9zvwrkWqBZw9JUcDCPA8JxotLZNxfGUPHHeDGmjUPg1nkVmiX4VfXKcctBdmH5avXs4ohWKe3",
    "LYR",
    "testit/json-1031181",
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
    "H1ybjtKhCk4rd33wfA53HH5SbtEkEUdJSqRTiEsqbHtZ"
  ]
}
```

## Signing message: H1ybjtKhCk4rd33wfA53HH5SbtEkEUdJSqRTiEsqbHtZ

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 7,
  "result": [
    "p1393",
    "JSkteh5nK49rydkj65TCmFmQBdkb8wqJBc1cuLn4V4zLJSyyGcYiXcpdLfGJFD5HRo4UMYYQcce8hHJRP1WnNyx"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 14,
  "result": {
    "balance": {
      "testit/json-1031181": 5000000.0,
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
    "LMCDi9rJ5xMWZb8vgW68e9zvwrkWqBZw9JUcDCPA8JxotLZNxfGUPHHeDGmjUPg1nkVmiX4VfXKcctBdmH5avXs4ohWKe3",
    "LYR",
    "testit/json-1031181"
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
        "from": "LDFj4M5QwzQMg7rsHQuLFcgnMXgiatsBCu7wvhJWBnhWnxQA3F3r1PY8vromAGxLvKwQPtszAcWPMp4KCnPhBjRzDMkyhT",
        "sendHash": "GsRV5gLjjCGXcj8DrQrLrEDPtrCbpDEukNwE9xFVeySz",
        "funds": {
          "testit/json-1031181": 452891.96071299
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Receiving","content":{"from":"LDFj4M5QwzQMg7rsHQuLFcgnMXgiatsBCu7wvhJWBnhWnxQA3F3r1PY8vromAGxLvKwQPtszAcWPMp4KCnPhBjRzDMkyhT","sendHash":"GsRV5gLjjCGXcj8DrQrLrEDPtrCbpDEukNwE9xFVeySz","funds":{"testit/json-1031181":452891.96071299}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 8,
  "method": "Sign",
  "params": [
    "hash",
    "DThnDSZ1nN5vXFMK5uoXhZue8VB3vi9RXdCHkKTWYC4L"
  ]
}
```

## Signing message: DThnDSZ1nN5vXFMK5uoXhZue8VB3vi9RXdCHkKTWYC4L

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 8,
  "result": [
    "p1393",
    "5Ctwmh6ZCZmz5cfX1BTwrBuhpafWzMymgte52x1ZKV4Jp5HH5zFndEX64vdN3ykrn1F2ZxQtKciaexGzTStnQt1m"
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
        "from": "LDFj4M5QwzQMg7rsHQuLFcgnMXgiatsBCu7wvhJWBnhWnxQA3F3r1PY8vromAGxLvKwQPtszAcWPMp4KCnPhBjRzDMkyhT",
        "sendHash": "Lh95pkeCHAq3X4utRzXA4fmhothExoWDQCeGcmUW5Wy",
        "funds": {
          "testit/json-1031181": 4547108.03928701,
          "LYR": 1099.9
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Receiving","content":{"from":"LDFj4M5QwzQMg7rsHQuLFcgnMXgiatsBCu7wvhJWBnhWnxQA3F3r1PY8vromAGxLvKwQPtszAcWPMp4KCnPhBjRzDMkyhT","sendHash":"Lh95pkeCHAq3X4utRzXA4fmhothExoWDQCeGcmUW5Wy","funds":{"testit/json-1031181":4547108.03928701,"LYR":1099.9}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 9,
  "method": "Sign",
  "params": [
    "hash",
    "9ANTSQnYAsJyuXW5zQMPy5DkjAnq6sPbtwCwK9Tc9a4Q"
  ]
}
```

## Signing message: 9ANTSQnYAsJyuXW5zQMPy5DkjAnq6sPbtwCwK9Tc9a4Q

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 9,
  "result": [
    "p1393",
    "5FsVS2LLZ8QXXmYdLKAPR6j8wTNNd4jaAASAf11frzrRig6iX1JwtArxSDefw4JsuGpj5Znai4GALsxj3m9YfvAJ"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 10,
  "method": "Sign",
  "params": [
    "hash",
    "FvhmvxJs7FA2GvHLk6vfwe8ygxkEXnftsch9aki9xsGE"
  ]
}
```

## Signing message: FvhmvxJs7FA2GvHLk6vfwe8ygxkEXnftsch9aki9xsGE

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 10,
  "result": [
    "p1393",
    "4MkMqGNptjcB5FVRCdBajGFweHoR3LVewtH2JiDjEEf8AkFriuETHi9JdmA2euPLhC9BrLbGwsxeHHJJPg2nCiDv"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 15,
  "result": {
    "balance": {
      "testit/json-1031181": 10000000.0,
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
    "testit/json-1031181"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 16,
  "result": {
    "poolId": "LDFj4M5QwzQMg7rsHQuLFcgnMXgiatsBCu7wvhJWBnhWnxQA3F3r1PY8vromAGxLvKwQPtszAcWPMp4KCnPhBjRzDMkyhT",
    "height": 5,
    "token0": "LYR",
    "token1": "testit/json-1031181",
    "balance": {
      "testit/json-1031181": 0.0,
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
    "LMCDi9rJ5xMWZb8vgW68e9zvwrkWqBZw9JUcDCPA8JxotLZNxfGUPHHeDGmjUPg1nkVmiX4VfXKcctBdmH5avXs4ohWKe3",
    "LYR",
    "300",
    "testit/json-1031181",
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
    "o9EWLDx2MUG9zwuUvxcKoyiCJ9MwP63VjVnHfXsugd2"
  ]
}
```

## Signing message: o9EWLDx2MUG9zwuUvxcKoyiCJ9MwP63VjVnHfXsugd2

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 11,
  "result": [
    "p1393",
    "3SdMfGzFWU3vUopKV9DbUxDQ6p81f7jEDLhzWvZkDXCniRv2jT9DEyqQF9eN4hxz3TJgbfknkaxiYQQUmBMzRhzU"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 17,
  "result": {
    "poolId": "LDFj4M5QwzQMg7rsHQuLFcgnMXgiatsBCu7wvhJWBnhWnxQA3F3r1PY8vromAGxLvKwQPtszAcWPMp4KCnPhBjRzDMkyhT",
    "height": 6,
    "token0": "LYR",
    "token1": "testit/json-1031181",
    "balance": {
      "testit/json-1031181": 7000000.0,
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
    "testit/json-1031181"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 18,
  "result": {
    "poolId": "LDFj4M5QwzQMg7rsHQuLFcgnMXgiatsBCu7wvhJWBnhWnxQA3F3r1PY8vromAGxLvKwQPtszAcWPMp4KCnPhBjRzDMkyhT",
    "height": 6,
    "token0": "LYR",
    "token1": "testit/json-1031181",
    "balance": {
      "testit/json-1031181": 7000000.0,
      "LYR": 300.0
    }
  }
}
```



