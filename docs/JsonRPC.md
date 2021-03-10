
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
    "testnet"
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
    "networkid": "testnet",
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
    "testnet"
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
      "stack": "   at Lyra.Node.JsonRpcServer.Status(String version, String networkid) in C:\\Users\\Wizard\\source\\repos\\LyraNetwork\\Core\\Lyra.Node2\\Services\\JsonRpcServer.cs:line 103",
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
    "LELjYgcrNjC9C1bw1shU8iy7gUmL43N5v5EQMc8coQKFsSGNa9oMiXtXMPZ8sHqP5FV86ui6yrfxUvXLZmm74CNmN3fAe1"
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
    "LELjYgcrNjC9C1bw1shU8iy7gUmL43N5v5EQMc8coQKFsSGNa9oMiXtXMPZ8sHqP5FV86ui6yrfxUvXLZmm74CNmN3fAe1"
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

## Signing message: Avtvc7Ey57t18UP2SoGjxSTouL64ESZrrRXtsyzCLeBx

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": [
    "p1393",
    "Ffx7nph4FLe23eeU3hbpkBfZiWqStWrXKb9a9qX5XefpDNpVPENLnvoRdrYT47YnTwohYgJrSfbuzQmuPggtwAq"
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
        "sendHash": "Avtvc7Ey57t18UP2SoGjxSTouL64ESZrrRXtsyzCLeBx",
        "funds": {
          "LYR": 13000.0
        }
      }
    }
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "balance": {
      "LYR": 4985273.97171312,
      "unittest/trans": 49999991914.67101
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
    "LELjYgcrNjC9C1bw1shU8iy7gUmL43N5v5EQMc8coQKFsSGNa9oMiXtXMPZ8sHqP5FV86ui6yrfxUvXLZmm74CNmN3fAe1"
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
    "LELjYgcrNjC9C1bw1shU8iy7gUmL43N5v5EQMc8coQKFsSGNa9oMiXtXMPZ8sHqP5FV86ui6yrfxUvXLZmm74CNmN3fAe1"
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
    "3Rq1X62A8Jz5DAzp4ougE16A5sBmCEfF4h3S4i532ibW"
  ]
}
```

## Signing message: 3Rq1X62A8Jz5DAzp4ougE16A5sBmCEfF4h3S4i532ibW

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": [
    "p1393",
    "Bj24ewG6eMhRqhBXSMbUhDuSQV3UPcuQNdpSRXqXpUGxfuVsKa143s2mk4whdwB4BFnU6BveHtqu3pFVsxM6MAH"
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
    "LELjYgcrNjC9C1bw1shU8iy7gUmL43N5v5EQMc8coQKFsSGNa9oMiXtXMPZ8sHqP5FV86ui6yrfxUvXLZmm74CNmN3fAe1",
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
    "DuQ6oPPc8cCrCoGUBGCpSjmLKCnY2rFR16MYwPQD7z6Y"
  ]
}
```

## Signing message: DuQ6oPPc8cCrCoGUBGCpSjmLKCnY2rFR16MYwPQD7z6Y

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": [
    "p1393",
    "4CNfXARdEo9youJL4MjSgy48rKba6wa548dsVq2wAdHGtBsPqJsXkpsXBF9yoWWSw1kDqqjyr29JpegRnMdrBsbr"
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
    "LELjYgcrNjC9C1bw1shU8iy7gUmL43N5v5EQMc8coQKFsSGNa9oMiXtXMPZ8sHqP5FV86ui6yrfxUvXLZmm74CNmN3fAe1",
    "json-7827429",
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
    "cu11QyGi6SeXQvCUKeiJQuQC4PprJQXej1tSNudy3p9"
  ]
}
```

## Signing message: cu11QyGi6SeXQvCUKeiJQuQC4PprJQXej1tSNudy3p9

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 4,
  "result": [
    "p1393",
    "2Dwcm3p7QSsxSkoDhw9MSqrk99gc3J4Bqzr1uXxudDUMrhagKMnJZFPgse5bPbFCEKsLvTMQiPw8jXiDcNe8wSvS"
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
      "testit/json-7827429": 10000000.0,
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
    "LELjYgcrNjC9C1bw1shU8iy7gUmL43N5v5EQMc8coQKFsSGNa9oMiXtXMPZ8sHqP5FV86ui6yrfxUvXLZmm74CNmN3fAe1",
    "0",
    "637509924365598712",
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
      "TimeStamp": "2021-03-10T17:00:30.955182Z",
      "SendAccountId": "LUTG2E1mdpGk5Qtq9BUgwZDWhUeZc14Xfw2pAvAdKoacvgRBU3atwtrQeoY3evm5C7TXRz3Q5nwPEUHj9p7CBDE6kQTQMy",
      "SendHash": "Avtvc7Ey57t18UP2SoGjxSTouL64ESZrrRXtsyzCLeBx",
      "RecvAccountId": "LELjYgcrNjC9C1bw1shU8iy7gUmL43N5v5EQMc8coQKFsSGNa9oMiXtXMPZ8sHqP5FV86ui6yrfxUvXLZmm74CNmN3fAe1",
      "RecvHash": "3Rq1X62A8Jz5DAzp4ougE16A5sBmCEfF4h3S4i532ibW",
      "Changes": {
        "LYR": 1300000000000
      },
      "Balances": {
        "LYR": 1300000000000
      }
    },
    {
      "Height": 2,
      "IsReceive": false,
      "TimeStamp": "2021-03-10T17:00:32.4411241Z",
      "SendAccountId": "LELjYgcrNjC9C1bw1shU8iy7gUmL43N5v5EQMc8coQKFsSGNa9oMiXtXMPZ8sHqP5FV86ui6yrfxUvXLZmm74CNmN3fAe1",
      "SendHash": "DuQ6oPPc8cCrCoGUBGCpSjmLKCnY2rFR16MYwPQD7z6Y",
      "RecvAccountId": "LUTG2E1mdpGk5Qtq9BUgwZDWhUeZc14Xfw2pAvAdKoacvgRBU3atwtrQeoY3evm5C7TXRz3Q5nwPEUHj9p7CBDE6kQTQMy",
      "Changes": {
        "LYR": -1100000000
      },
      "Balances": {
        "LYR": 1298900000000
      }
    },
    {
      "Height": 3,
      "IsReceive": true,
      "TimeStamp": "2021-03-10T17:00:35.6386147Z",
      "RecvAccountId": "LELjYgcrNjC9C1bw1shU8iy7gUmL43N5v5EQMc8coQKFsSGNa9oMiXtXMPZ8sHqP5FV86ui6yrfxUvXLZmm74CNmN3fAe1",
      "RecvHash": "cu11QyGi6SeXQvCUKeiJQuQC4PprJQXej1tSNudy3p9",
      "Changes": {
        "testit/json-7827429": 1000000000000000,
        "LYR": -1000000000000
      },
      "Balances": {
        "testit/json-7827429": 1000000000000000,
        "LYR": 298900000000
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
    "testit/json-7827429"
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
      "stack": "   at Lyra.Node.JsonRpcServer.Pool(String token0, String token1) in C:\\Users\\Wizard\\source\\repos\\LyraNetwork\\Core\\Lyra.Node2\\Services\\JsonRpcServer.cs:line 217",
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
    "LELjYgcrNjC9C1bw1shU8iy7gUmL43N5v5EQMc8coQKFsSGNa9oMiXtXMPZ8sHqP5FV86ui6yrfxUvXLZmm74CNmN3fAe1",
    "LYR",
    "testit/json-7827429"
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
    "EDwDrGd9EiYaxUVRAvkEscN8ZCiDNQRmHJGVZvNqAC61"
  ]
}
```

## Signing message: EDwDrGd9EiYaxUVRAvkEscN8ZCiDNQRmHJGVZvNqAC61

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 5,
  "result": [
    "p1393",
    "C49Fu6h4HY1QyK6NmNQsGemY9uv9u3riUpwEbXdfDeCPYNRai9jjRL4rD2h3NrNJRdH8tjv9oHqkWyxSJwmuqab"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 11,
  "result": {
    "poolId": "LEAdNJTwzXdp88yQZpgVP1CrySSjpyT9qMpo4PMdJ2M2G1wmBNzUEenYGefkj4rYucsq4hhaoNhqsdPCs3FGyEzUM4CFcR",
    "height": 1,
    "token0": "LYR",
    "token1": "testit/json-7827429",
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
    "LELjYgcrNjC9C1bw1shU8iy7gUmL43N5v5EQMc8coQKFsSGNa9oMiXtXMPZ8sHqP5FV86ui6yrfxUvXLZmm74CNmN3fAe1",
    "LYR",
    "1000",
    "testit/json-7827429",
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
    "9d1Yxrg9pTigRthA8ooUTG6FSbqnbTPYV7aNjKTFR7Hw"
  ]
}
```

## Signing message: 9d1Yxrg9pTigRthA8ooUTG6FSbqnbTPYV7aNjKTFR7Hw

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 6,
  "result": [
    "p1393",
    "4rG7iMYFAmjbVCerRFcXoC5zhSJNdBRoMag2zyMVor6QkCQV9PpjNc7xcp8FdwDbLidyj74v9w5HqTwidWbnkDjt"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 12,
  "result": {
    "poolId": "LEAdNJTwzXdp88yQZpgVP1CrySSjpyT9qMpo4PMdJ2M2G1wmBNzUEenYGefkj4rYucsq4hhaoNhqsdPCs3FGyEzUM4CFcR",
    "height": 2,
    "token0": "LYR",
    "token1": "testit/json-7827429",
    "balance": {
      "testit/json-7827429": 5000000.0,
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
    "LEAdNJTwzXdp88yQZpgVP1CrySSjpyT9qMpo4PMdJ2M2G1wmBNzUEenYGefkj4rYucsq4hhaoNhqsdPCs3FGyEzUM4CFcR",
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
    "SwapOutToken": "testit/json-7827429",
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
    "LELjYgcrNjC9C1bw1shU8iy7gUmL43N5v5EQMc8coQKFsSGNa9oMiXtXMPZ8sHqP5FV86ui6yrfxUvXLZmm74CNmN3fAe1",
    "LYR",
    "testit/json-7827429",
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
    "35ub33jpkoAR1KS1V8SRvtb3HfmkXji12Kg1rrm1ZHsC"
  ]
}
```

## Signing message: 35ub33jpkoAR1KS1V8SRvtb3HfmkXji12Kg1rrm1ZHsC

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 7,
  "result": [
    "p1393",
    "5XrT7D5u4wgVaUSH6K6FDXSczUwyegBuFQrnmLQ5BYChM7q3knstF7GzavhcTAnaRdS11z1wxLiB4ngrPsxdeU1v"
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
      "testit/json-7827429": 5000000.0,
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
    "LELjYgcrNjC9C1bw1shU8iy7gUmL43N5v5EQMc8coQKFsSGNa9oMiXtXMPZ8sHqP5FV86ui6yrfxUvXLZmm74CNmN3fAe1",
    "LYR",
    "testit/json-7827429"
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
        "from": "LEAdNJTwzXdp88yQZpgVP1CrySSjpyT9qMpo4PMdJ2M2G1wmBNzUEenYGefkj4rYucsq4hhaoNhqsdPCs3FGyEzUM4CFcR",
        "sendHash": "2TYhy39nD52ziefLvmN8gNffQsEhz1G7hfidKatq6XpQ",
        "funds": {
          "testit/json-7827429": 452891.96071299
        }
      }
    }
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 8,
  "method": "Sign",
  "params": [
    "hash",
    "Bh5r9Uco1EWabLf4i4s3znmMB3PJgSLXuWZSzLwcUX5q"
  ]
}
```

## Signing message: Bh5r9Uco1EWabLf4i4s3znmMB3PJgSLXuWZSzLwcUX5q

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 8,
  "result": [
    "p1393",
    "RCWYg3sk7s9Ph17NgDuFJQk7wk3czFhnPqPyWygxYYS8CHdbxy8VKqnXcLmQ6q5tdTU5DJLuECbbgpMkQzQrGxi"
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
        "from": "LEAdNJTwzXdp88yQZpgVP1CrySSjpyT9qMpo4PMdJ2M2G1wmBNzUEenYGefkj4rYucsq4hhaoNhqsdPCs3FGyEzUM4CFcR",
        "sendHash": "ALUCiEqDUPjiPUjCpjnsJNuaBeFqBYKGdyDhTmhqd4iT",
        "funds": {
          "testit/json-7827429": 4547108.03928701,
          "LYR": 1099.9
        }
      }
    }
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
    "9rLJLamfR3EZzANBjoiGGzKSUR2KHXSGySiqhTcs9EjE"
  ]
}
```

## Signing message: 9rLJLamfR3EZzANBjoiGGzKSUR2KHXSGySiqhTcs9EjE

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 9,
  "result": [
    "p1393",
    "5irbYP1FVxdmDt8XmEGJB4L7rjnfFHugxSj7bdUFBAyXhsNkZEsf1wiGZmwKwSLVo1h6XajVxvjw3mB4SsQVgiup"
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
    "3NV4x49S9PvgdnkZM7AHGpgDWJxGZaYRAzPqQNJ7y72a"
  ]
}
```

## Signing message: 3NV4x49S9PvgdnkZM7AHGpgDWJxGZaYRAzPqQNJ7y72a

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 10,
  "result": [
    "p1393",
    "4ZazXsYAX1APTCtxaP4i616VaacAsvwB9njMD1uUib15FcgmrzsKyhSgqH9RtdvvnvuT1minXsiLgXafxAWq9X9c"
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
      "testit/json-7827429": 10000000.0,
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
    "testit/json-7827429"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 16,
  "result": {
    "poolId": "LEAdNJTwzXdp88yQZpgVP1CrySSjpyT9qMpo4PMdJ2M2G1wmBNzUEenYGefkj4rYucsq4hhaoNhqsdPCs3FGyEzUM4CFcR",
    "height": 5,
    "token0": "LYR",
    "token1": "testit/json-7827429",
    "balance": {
      "testit/json-7827429": 0.0,
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
    "LELjYgcrNjC9C1bw1shU8iy7gUmL43N5v5EQMc8coQKFsSGNa9oMiXtXMPZ8sHqP5FV86ui6yrfxUvXLZmm74CNmN3fAe1",
    "LYR",
    "300",
    "testit/json-7827429",
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
    "6Jc7nWiawNwm2jJXX6JKUqz6ezACUim9BXzRT4XY7G2g"
  ]
}
```

## Signing message: 6Jc7nWiawNwm2jJXX6JKUqz6ezACUim9BXzRT4XY7G2g

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 11,
  "result": [
    "p1393",
    "2bwaqbRcRp852iniLY2255ay923wj38SEuptS2i8S3xETwU86xMNYXnDahCq6rp5f6L7P9pL3RuQSQ8iECMpoiyp"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 17,
  "result": {
    "poolId": "LEAdNJTwzXdp88yQZpgVP1CrySSjpyT9qMpo4PMdJ2M2G1wmBNzUEenYGefkj4rYucsq4hhaoNhqsdPCs3FGyEzUM4CFcR",
    "height": 6,
    "token0": "LYR",
    "token1": "testit/json-7827429",
    "balance": {
      "testit/json-7827429": 7000000.0,
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
    "testit/json-7827429"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 18,
  "result": {
    "poolId": "LEAdNJTwzXdp88yQZpgVP1CrySSjpyT9qMpo4PMdJ2M2G1wmBNzUEenYGefkj4rYucsq4hhaoNhqsdPCs3FGyEzUM4CFcR",
    "height": 6,
    "token0": "LYR",
    "token1": "testit/json-7827429",
    "balance": {
      "testit/json-7827429": 7000000.0,
      "LYR": 300.0
    }
  }
}
```



