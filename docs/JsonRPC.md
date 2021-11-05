# API: BalanceResult Balance(string accountId)
/* wallet get balance */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "Balance",
  "id": 1,
  "params": [
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF"
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
      "LYR": 5299.8,
      "testit/json-1556709": 10000000.0,
      "testit/json-4555140": 10000000.0,
      "testit/json-1979343": 10000000.0,
      "testit/json-2737471": 10000000.0,
      "testit/json-3855456": 10000000.0,
      "testit/json-6351728": 3000000.0,
      "testit/json-2332708": 3000000.0
    },
    "height": 35,
    "unreceived": true
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
  "id": 2,
  "params": [
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 2,
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
    23000.0,
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
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
    "6qGfu7z9hVzgZe5oQ5EKA71GWmbwXJa5m2Wfjc3WF13Q",
    "LUTG2E1mdpGk5Qtq9BUgwZDWhUeZc14Xfw2pAvAdKoacvgRBU3atwtrQeoY3evm5C7TXRz3Q5nwPEUHj9p7CBDE6kQTQMy"
  ]
}
```

## Signing message: 6qGfu7z9hVzgZe5oQ5EKA71GWmbwXJa5m2Wfjc3WF13Q

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": [
    "p1393",
    "3R8WgscJmwsp1PiqvwcCNXB5Sa84RpsxE2eETDEoJgRpmPn3g3GeGgqbacsbT2aoDAwEjuPiDN1wHBbHsZgVnbkU"
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
        "to": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
        "sendHash": "6qGfu7z9hVzgZe5oQ5EKA71GWmbwXJa5m2Wfjc3WF13Q",
        "funds": {
          "LYR": 23000.0
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
      "LYR": 483863.48260583,
      "unittest/trans": 49999981238.24132
    },
    "height": 209,
    "unreceived": true,
    "txHash": "6qGfu7z9hVzgZe5oQ5EKA71GWmbwXJa5m2Wfjc3WF13Q"
  }
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Receiving","content":{"from":"LUTG2E1mdpGk5Qtq9BUgwZDWhUeZc14Xfw2pAvAdKoacvgRBU3atwtrQeoY3evm5C7TXRz3Q5nwPEUHj9p7CBDE6kQTQMy","to":"LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF","sendHash":"6qGfu7z9hVzgZe5oQ5EKA71GWmbwXJa5m2Wfjc3WF13Q","funds":{"LYR":23000.0}}}]}

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
        "to": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
        "sendHash": "6qGfu7z9hVzgZe5oQ5EKA71GWmbwXJa5m2Wfjc3WF13Q",
        "funds": {
          "LYR": 23000.0
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Receiving","content":{"from":"LUTG2E1mdpGk5Qtq9BUgwZDWhUeZc14Xfw2pAvAdKoacvgRBU3atwtrQeoY3evm5C7TXRz3Q5nwPEUHj9p7CBDE6kQTQMy","to":"LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF","sendHash":"6qGfu7z9hVzgZe5oQ5EKA71GWmbwXJa5m2Wfjc3WF13Q","funds":{"LYR":23000.0}}}]}
# API: BalanceResult Balance(string accountId)
/* balance shows unreceived */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "Balance",
  "id": 3,
  "params": [
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": {
    "balance": {
      "LYR": 5299.8,
      "testit/json-1556709": 10000000.0,
      "testit/json-4555140": 10000000.0,
      "testit/json-1979343": 10000000.0,
      "testit/json-2737471": 10000000.0,
      "testit/json-3855456": 10000000.0,
      "testit/json-6351728": 3000000.0,
      "testit/json-2332708": 3000000.0
    },
    "height": 35,
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
  "id": 4,
  "params": [
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF"
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
    "8HBkAeJxj1jga1GMaBEE2WgaAUaaKeeKTZTyiHSEGgJ3",
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF"
  ]
}
```

## Signing message: 8HBkAeJxj1jga1GMaBEE2WgaAUaaKeeKTZTyiHSEGgJ3

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": [
    "p1393",
    "5T3DAeZge66UxxowviUKazPBWHrgnwzVdeecy4yqSLTfqNSnYtMCDRbXtgq8vkAwBe1mnYdBSWiHvaAZYtuKoqwL"
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
        "recvHash": "8HBkAeJxj1jga1GMaBEE2WgaAUaaKeeKTZTyiHSEGgJ3",
        "to": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
        "sendHash": "5ymsfC5QDXGzYKwJnj5c7R6wqrgCtSJ8mHh38JbptKyn",
        "funds": {
          "LYR": 13000.0
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Settlement","content":{"recvHash":"8HBkAeJxj1jga1GMaBEE2WgaAUaaKeeKTZTyiHSEGgJ3","to":"LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF","sendHash":"5ymsfC5QDXGzYKwJnj5c7R6wqrgCtSJ8mHh38JbptKyn","funds":{"LYR":13000.0}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "method": "Notify",
  "params": [
    {
      "catalog": "Settlement",
      "content": {
        "recvHash": "8HBkAeJxj1jga1GMaBEE2WgaAUaaKeeKTZTyiHSEGgJ3",
        "to": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
        "sendHash": "5ymsfC5QDXGzYKwJnj5c7R6wqrgCtSJ8mHh38JbptKyn",
        "funds": {
          "LYR": 13000.0
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Settlement","content":{"recvHash":"8HBkAeJxj1jga1GMaBEE2WgaAUaaKeeKTZTyiHSEGgJ3","to":"LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF","sendHash":"5ymsfC5QDXGzYKwJnj5c7R6wqrgCtSJ8mHh38JbptKyn","funds":{"LYR":13000.0}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "Sign",
  "params": [
    "hash",
    "9BwbtKiHdUhTobQVwegXY6a3AsWbGahhbN7xaHxSrec3",
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF"
  ]
}
```

## Signing message: 9BwbtKiHdUhTobQVwegXY6a3AsWbGahhbN7xaHxSrec3

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": [
    "p1393",
    "AwdtBU1YCSm3HPhKBP9tznwjwT6jVDTK6CN7iVqDTArEGujaokZdEMEkWPYoz1r76mYRVK9ex4fcsaaGRVLYXza"
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
        "recvHash": "9BwbtKiHdUhTobQVwegXY6a3AsWbGahhbN7xaHxSrec3",
        "to": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
        "sendHash": "4LUJQ7FMV6HJbg2seRUAyZABKhHHvcrQ5tPD5sMBjoQA",
        "funds": {
          "LYR": 13000.0
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Settlement","content":{"recvHash":"9BwbtKiHdUhTobQVwegXY6a3AsWbGahhbN7xaHxSrec3","to":"LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF","sendHash":"4LUJQ7FMV6HJbg2seRUAyZABKhHHvcrQ5tPD5sMBjoQA","funds":{"LYR":13000.0}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "method": "Notify",
  "params": [
    {
      "catalog": "Settlement",
      "content": {
        "recvHash": "9BwbtKiHdUhTobQVwegXY6a3AsWbGahhbN7xaHxSrec3",
        "to": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
        "sendHash": "4LUJQ7FMV6HJbg2seRUAyZABKhHHvcrQ5tPD5sMBjoQA",
        "funds": {
          "LYR": 13000.0
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Settlement","content":{"recvHash":"9BwbtKiHdUhTobQVwegXY6a3AsWbGahhbN7xaHxSrec3","to":"LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF","sendHash":"4LUJQ7FMV6HJbg2seRUAyZABKhHHvcrQ5tPD5sMBjoQA","funds":{"LYR":13000.0}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 4,
  "method": "Sign",
  "params": [
    "hash",
    "7aGMhBMT5JHQEoL5BmnEoLFAFqbNzLXnByHyfs8rkQ5D",
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF"
  ]
}
```

## Signing message: 7aGMhBMT5JHQEoL5BmnEoLFAFqbNzLXnByHyfs8rkQ5D

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 4,
  "result": [
    "p1393",
    "55LWgEyjWmNCWBFkC5LtcKVweKxmzNidwc9Ag8BxAh661hUyo5TWFbTxiN4fUedwR1bBiRdVHBgTVFeRsvc4TnKh"
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
        "recvHash": "7aGMhBMT5JHQEoL5BmnEoLFAFqbNzLXnByHyfs8rkQ5D",
        "to": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
        "sendHash": "61aRJdsGK3PbYQdKW9fNngJs3WiDQyh59eLUmTXhXsy8",
        "funds": {
          "LYR": 13000.0
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Settlement","content":{"recvHash":"7aGMhBMT5JHQEoL5BmnEoLFAFqbNzLXnByHyfs8rkQ5D","to":"LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF","sendHash":"61aRJdsGK3PbYQdKW9fNngJs3WiDQyh59eLUmTXhXsy8","funds":{"LYR":13000.0}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "method": "Notify",
  "params": [
    {
      "catalog": "Settlement",
      "content": {
        "recvHash": "7aGMhBMT5JHQEoL5BmnEoLFAFqbNzLXnByHyfs8rkQ5D",
        "to": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
        "sendHash": "61aRJdsGK3PbYQdKW9fNngJs3WiDQyh59eLUmTXhXsy8",
        "funds": {
          "LYR": 13000.0
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Settlement","content":{"recvHash":"7aGMhBMT5JHQEoL5BmnEoLFAFqbNzLXnByHyfs8rkQ5D","to":"LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF","sendHash":"61aRJdsGK3PbYQdKW9fNngJs3WiDQyh59eLUmTXhXsy8","funds":{"LYR":13000.0}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 5,
  "method": "Sign",
  "params": [
    "hash",
    "6YZEJUPut9pQv4bYJvesneNdvfitsWfvc7sKHc8VnuK7",
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF"
  ]
}
```

## Signing message: 6YZEJUPut9pQv4bYJvesneNdvfitsWfvc7sKHc8VnuK7

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 5,
  "result": [
    "p1393",
    "97Vzh9Apf3ggpKbgr4FA2H6996GdF58ZKKySRacbRGpTHaYcdPyrk5tBaz2H5Lcqk67J9VmpBQyWAJgJTKWUdsm"
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
        "recvHash": "6YZEJUPut9pQv4bYJvesneNdvfitsWfvc7sKHc8VnuK7",
        "to": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
        "sendHash": "BbDPK5LdespLqLPCgno3Z8cqUVKJSVxLdQzEoUBdyB5N",
        "funds": {
          "LYR": 23000.0
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Settlement","content":{"recvHash":"6YZEJUPut9pQv4bYJvesneNdvfitsWfvc7sKHc8VnuK7","to":"LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF","sendHash":"BbDPK5LdespLqLPCgno3Z8cqUVKJSVxLdQzEoUBdyB5N","funds":{"LYR":23000.0}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "method": "Notify",
  "params": [
    {
      "catalog": "Settlement",
      "content": {
        "recvHash": "6YZEJUPut9pQv4bYJvesneNdvfitsWfvc7sKHc8VnuK7",
        "to": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
        "sendHash": "BbDPK5LdespLqLPCgno3Z8cqUVKJSVxLdQzEoUBdyB5N",
        "funds": {
          "LYR": 23000.0
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Settlement","content":{"recvHash":"6YZEJUPut9pQv4bYJvesneNdvfitsWfvc7sKHc8VnuK7","to":"LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF","sendHash":"BbDPK5LdespLqLPCgno3Z8cqUVKJSVxLdQzEoUBdyB5N","funds":{"LYR":23000.0}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 6,
  "method": "Sign",
  "params": [
    "hash",
    "5UV6vGdoGuLmk4gEdAbSS7TQN6UompDb9GwDi8yF7gdS",
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF"
  ]
}
```

## Signing message: 5UV6vGdoGuLmk4gEdAbSS7TQN6UompDb9GwDi8yF7gdS

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 6,
  "result": [
    "p1393",
    "27CgqJBgM6XFA513aBENYKmrpWwZ5kLCFhLQpPiUnhHaTe1gsUKHDo5cdUXYSLnK1LZV5a3eyrVX9upZX4DfuUFA"
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
        "recvHash": "5UV6vGdoGuLmk4gEdAbSS7TQN6UompDb9GwDi8yF7gdS",
        "to": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
        "sendHash": "AkGEPtZyKRW3rHMrTkwFpeoCSJrcPPK4G2frJWqYm4DL",
        "funds": {
          "LYR": 23000.0
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Settlement","content":{"recvHash":"5UV6vGdoGuLmk4gEdAbSS7TQN6UompDb9GwDi8yF7gdS","to":"LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF","sendHash":"AkGEPtZyKRW3rHMrTkwFpeoCSJrcPPK4G2frJWqYm4DL","funds":{"LYR":23000.0}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "method": "Notify",
  "params": [
    {
      "catalog": "Settlement",
      "content": {
        "recvHash": "5UV6vGdoGuLmk4gEdAbSS7TQN6UompDb9GwDi8yF7gdS",
        "to": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
        "sendHash": "AkGEPtZyKRW3rHMrTkwFpeoCSJrcPPK4G2frJWqYm4DL",
        "funds": {
          "LYR": 23000.0
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Settlement","content":{"recvHash":"5UV6vGdoGuLmk4gEdAbSS7TQN6UompDb9GwDi8yF7gdS","to":"LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF","sendHash":"AkGEPtZyKRW3rHMrTkwFpeoCSJrcPPK4G2frJWqYm4DL","funds":{"LYR":23000.0}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 7,
  "method": "Sign",
  "params": [
    "hash",
    "653ab2Sxghosn2XzsDqUpYeAGsFqDHAA12r9N4i5t1hm",
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF"
  ]
}
```

## Signing message: 653ab2Sxghosn2XzsDqUpYeAGsFqDHAA12r9N4i5t1hm

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 7,
  "result": [
    "p1393",
    "3Qg3NA8kyQjUEmSWiSSRaYD19X8ozpBnkG2yXg7bxWGFv87DZgHEyn6zsnyUwjWBBgJ4VmbP52HE5TZhsU31ooZf"
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
        "recvHash": "653ab2Sxghosn2XzsDqUpYeAGsFqDHAA12r9N4i5t1hm",
        "to": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
        "sendHash": "BagpKfBJ7vYgTmdXv1TJYRCzDtbpDTm2FMqFgLDH65TU",
        "funds": {
          "LYR": 23000.0
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Settlement","content":{"recvHash":"653ab2Sxghosn2XzsDqUpYeAGsFqDHAA12r9N4i5t1hm","to":"LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF","sendHash":"BagpKfBJ7vYgTmdXv1TJYRCzDtbpDTm2FMqFgLDH65TU","funds":{"LYR":23000.0}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "method": "Notify",
  "params": [
    {
      "catalog": "Settlement",
      "content": {
        "recvHash": "653ab2Sxghosn2XzsDqUpYeAGsFqDHAA12r9N4i5t1hm",
        "to": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
        "sendHash": "BagpKfBJ7vYgTmdXv1TJYRCzDtbpDTm2FMqFgLDH65TU",
        "funds": {
          "LYR": 23000.0
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Settlement","content":{"recvHash":"653ab2Sxghosn2XzsDqUpYeAGsFqDHAA12r9N4i5t1hm","to":"LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF","sendHash":"BagpKfBJ7vYgTmdXv1TJYRCzDtbpDTm2FMqFgLDH65TU","funds":{"LYR":23000.0}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 8,
  "method": "Sign",
  "params": [
    "hash",
    "JEEX7KhqpL2M3hWBqEzJoZJ5rzWFfJAuFwan4vJgPgxt",
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF"
  ]
}
```

## Signing message: JEEX7KhqpL2M3hWBqEzJoZJ5rzWFfJAuFwan4vJgPgxt

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 8,
  "result": [
    "p1393",
    "BYQ4iGxBRRMdMGvsPe6Rap2DakKaFcfV26xJk1UdJ4fDMrxKLN9V4uWEpSv4V2tfA3w9kJpCbbMb1YJqSygHwo4"
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
        "recvHash": "JEEX7KhqpL2M3hWBqEzJoZJ5rzWFfJAuFwan4vJgPgxt",
        "to": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
        "sendHash": "6qGfu7z9hVzgZe5oQ5EKA71GWmbwXJa5m2Wfjc3WF13Q",
        "funds": {
          "LYR": 23000.0
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Settlement","content":{"recvHash":"JEEX7KhqpL2M3hWBqEzJoZJ5rzWFfJAuFwan4vJgPgxt","to":"LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF","sendHash":"6qGfu7z9hVzgZe5oQ5EKA71GWmbwXJa5m2Wfjc3WF13Q","funds":{"LYR":23000.0}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "method": "Notify",
  "params": [
    {
      "catalog": "Settlement",
      "content": {
        "recvHash": "JEEX7KhqpL2M3hWBqEzJoZJ5rzWFfJAuFwan4vJgPgxt",
        "to": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
        "sendHash": "6qGfu7z9hVzgZe5oQ5EKA71GWmbwXJa5m2Wfjc3WF13Q",
        "funds": {
          "LYR": 23000.0
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Settlement","content":{"recvHash":"JEEX7KhqpL2M3hWBqEzJoZJ5rzWFfJAuFwan4vJgPgxt","to":"LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF","sendHash":"6qGfu7z9hVzgZe5oQ5EKA71GWmbwXJa5m2Wfjc3WF13Q","funds":{"LYR":23000.0}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 4,
  "result": {
    "balance": {
      "LYR": 136299.8,
      "testit/json-1556709": 10000000.0,
      "testit/json-4555140": 10000000.0,
      "testit/json-1979343": 10000000.0,
      "testit/json-2737471": 10000000.0,
      "testit/json-3855456": 10000000.0,
      "testit/json-6351728": 3000000.0,
      "testit/json-2332708": 3000000.0
    },
    "height": 42,
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
  "id": 5,
  "params": [
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
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
  "id": 9,
  "method": "Sign",
  "params": [
    "hash",
    "ABweN15eoBYppSwBMPbFNBmisoVGdkuR2J1VeuCNrmCm",
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF"
  ]
}
```

## Signing message: ABweN15eoBYppSwBMPbFNBmisoVGdkuR2J1VeuCNrmCm

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 9,
  "result": [
    "p1393",
    "2FRjNtbgzBny9HWWUrF3M8rYVag6JKfSgfEJagfHD4pHQ69AJYT5bbLugcHbN8s3pzzhjwzrstzR8hkYNbtLnmfq"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 5,
  "result": {
    "balance": {
      "LYR": 136288.8,
      "testit/json-1556709": 10000000.0,
      "testit/json-4555140": 10000000.0,
      "testit/json-1979343": 10000000.0,
      "testit/json-2737471": 10000000.0,
      "testit/json-3855456": 10000000.0,
      "testit/json-6351728": 3000000.0,
      "testit/json-2332708": 3000000.0
    },
    "height": 43,
    "unreceived": false,
    "txHash": "ABweN15eoBYppSwBMPbFNBmisoVGdkuR2J1VeuCNrmCm"
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
  "id": 6,
  "params": [
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
    "json-361478",
    "testit",
    "10000000"
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
    "HcqR6z1k67msNYtiBicHFsccDcphLt3d5ez6kMA9ZT4D",
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF"
  ]
}
```

## Signing message: HcqR6z1k67msNYtiBicHFsccDcphLt3d5ez6kMA9ZT4D

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 10,
  "result": [
    "p1393",
    "3MLeZFg737knhcEWQ6skvqwyp1atAArnvKCyoDGA3pv1kzBtN2LgyCjNdBkhp3DtLoe1yfFfAL7jS9DtLimJ72TP"
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
        "recvHash": "HcqR6z1k67msNYtiBicHFsccDcphLt3d5ez6kMA9ZT4D",
        "to": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
        "funds": {
          "testit/json-361478": 10000000.0,
          "LYR": -10000.0
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Settlement","content":{"recvHash":"HcqR6z1k67msNYtiBicHFsccDcphLt3d5ez6kMA9ZT4D","to":"LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF","funds":{"testit/json-361478":10000000.0,"LYR":-10000.0}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "method": "Notify",
  "params": [
    {
      "catalog": "Settlement",
      "content": {
        "recvHash": "HcqR6z1k67msNYtiBicHFsccDcphLt3d5ez6kMA9ZT4D",
        "to": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
        "funds": {
          "testit/json-361478": 10000000.0,
          "LYR": -10000.0
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Settlement","content":{"recvHash":"HcqR6z1k67msNYtiBicHFsccDcphLt3d5ez6kMA9ZT4D","to":"LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF","funds":{"testit/json-361478":10000000.0,"LYR":-10000.0}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 6,
  "result": {
    "balance": {
      "testit/json-361478": 10000000.0,
      "LYR": 126288.8,
      "testit/json-1556709": 10000000.0,
      "testit/json-4555140": 10000000.0,
      "testit/json-1979343": 10000000.0,
      "testit/json-2737471": 10000000.0,
      "testit/json-3855456": 10000000.0,
      "testit/json-6351728": 3000000.0,
      "testit/json-2332708": 3000000.0
    },
    "height": 44,
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
  "id": 7,
  "params": [
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
    "0",
    "1636102737102",
    "5"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 7,
  "result": [
    {
      "Height": 1,
      "IsReceive": true,
      "TimeStamp": 1636041893538,
      "SendAccountId": "LUTG2E1mdpGk5Qtq9BUgwZDWhUeZc14Xfw2pAvAdKoacvgRBU3atwtrQeoY3evm5C7TXRz3Q5nwPEUHj9p7CBDE6kQTQMy",
      "SendHash": "3oPByDbv4xpiVYDn6kMuBe4YgvQuLPjYUiGJ7nh855L7",
      "RecvAccountId": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
      "RecvHash": "34k89o4zhDHRhGKKD5Ddz98JsT53yWSdyKveU7Zz3aP6",
      "Changes": {
        "LYR": "13000"
      },
      "Balances": {
        "LYR": "13000"
      }
    },
    {
      "Height": 2,
      "IsReceive": true,
      "TimeStamp": 1636041893752,
      "SendAccountId": "LUTG2E1mdpGk5Qtq9BUgwZDWhUeZc14Xfw2pAvAdKoacvgRBU3atwtrQeoY3evm5C7TXRz3Q5nwPEUHj9p7CBDE6kQTQMy",
      "SendHash": "EqPD1QAiLfnFpnZezqGhk8YCFGjdKDFkdLAUURBzy9fM",
      "RecvAccountId": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
      "RecvHash": "6TPWgp3My2jenhUftnrPy15xWWrZX16SKZeycCxAhN6N",
      "Changes": {
        "LYR": "13000"
      },
      "Balances": {
        "LYR": "26000"
      }
    },
    {
      "Height": 3,
      "IsReceive": false,
      "TimeStamp": 1636041930000,
      "SendAccountId": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
      "SendHash": "7Mj3XXWHHpDXNKCxxYEqznDJ39Cp9XDTAyRktMepVx6E",
      "RecvAccountId": "LUTG2E1mdpGk5Qtq9BUgwZDWhUeZc14Xfw2pAvAdKoacvgRBU3atwtrQeoY3evm5C7TXRz3Q5nwPEUHj9p7CBDE6kQTQMy",
      "Changes": {
        "LYR": "-11"
      },
      "Balances": {
        "LYR": "25989"
      }
    },
    {
      "Height": 4,
      "IsReceive": true,
      "TimeStamp": 1636041944206,
      "RecvAccountId": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
      "RecvHash": "9Uyo5n6ras4qVk6k4zekKm9WxACTBNBdUUZonq5ZW7FA",
      "Changes": {
        "testit/json-2332708": "10000000",
        "LYR": "-10000"
      },
      "Balances": {
        "testit/json-2332708": "10000000",
        "LYR": "15989"
      }
    },
    {
      "Height": 5,
      "IsReceive": false,
      "TimeStamp": 1636041975271,
      "SendAccountId": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
      "SendHash": "5Zg2CU7WknRErBcm5iztyno16mYSk4YtMcWw2eoNpKs4",
      "RecvAccountId": "LPFA82ZDTo4cyoeY3EGozTpbWWzUXAtHCm33cMDcXyPzuV2HQf1X2Z9xVAins9kGJdBY12iGAzBPuMZvvW6x4ktLXa1MKQ",
      "RecvHash": "9zHsCVMzRvdXUh2txGXojkBP4NwHaHMLr36iEdNEbFKg",
      "Changes": {
        "LYR": "-1001"
      },
      "Balances": {
        "testit/json-2332708": "10000000",
        "LYR": "14988"
      }
    }
  ]
}
```


Client send:
```
{
  "jsonrpc": "2.0",
  "method": "Send",
  "id": 2,
  "params": [
    "LUTG2E1mdpGk5Qtq9BUgwZDWhUeZc14Xfw2pAvAdKoacvgRBU3atwtrQeoY3evm5C7TXRz3Q5nwPEUHj9p7CBDE6kQTQMy",
    2000.0,
    "LU2BiJQtsMsUVw4Y6AbhveUPFc66iKWES3LZtNtXJNaomhKCSX1PErCzExTwim8Yv1fziSSuGuB49Emj44vjoEDpUFdK43",
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
    "3vy6UjNn4bM2wrjfS8VAdXfBPx6N2pkCNVU7yChST2qq",
    "LUTG2E1mdpGk5Qtq9BUgwZDWhUeZc14Xfw2pAvAdKoacvgRBU3atwtrQeoY3evm5C7TXRz3Q5nwPEUHj9p7CBDE6kQTQMy"
  ]
}
```

## Signing message: 3vy6UjNn4bM2wrjfS8VAdXfBPx6N2pkCNVU7yChST2qq

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": [
    "p1393",
    "wHzzLNjgr9FVL4A4G4fEhaEdyFzhRLKq4CmcJNvkHQ1od1CabdcJEHpeAPa2p1kQt5rSc3Ckz1dNJZonY1Nq7dM"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": {
    "balance": {
      "LYR": 481862.48260583,
      "unittest/trans": 49999981238.24132
    },
    "height": 210,
    "unreceived": true,
    "txHash": "3vy6UjNn4bM2wrjfS8VAdXfBPx6N2pkCNVU7yChST2qq"
  }
}
```


Client send:
```
{
  "jsonrpc": "2.0",
  "method": "Send",
  "id": 3,
  "params": [
    "LUTG2E1mdpGk5Qtq9BUgwZDWhUeZc14Xfw2pAvAdKoacvgRBU3atwtrQeoY3evm5C7TXRz3Q5nwPEUHj9p7CBDE6kQTQMy",
    2000.0,
    "LSLfaSr9pvWCHbFZ35YvzwCU7uXSMjHbDERthhEvUZ1zPs5LC9FnB5gCSLAQd537Qk9aXdjdcFjMgaPx7Gg3Aa4ugSM9eU",
    "LYR"
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
    "EXXE4dwMLnozTd3yzvWRe8ru5JLK7UVmDJpBYr9UV8VG",
    "LUTG2E1mdpGk5Qtq9BUgwZDWhUeZc14Xfw2pAvAdKoacvgRBU3atwtrQeoY3evm5C7TXRz3Q5nwPEUHj9p7CBDE6kQTQMy"
  ]
}
```

## Signing message: EXXE4dwMLnozTd3yzvWRe8ru5JLK7UVmDJpBYr9UV8VG

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 4,
  "result": [
    "p1393",
    "5LejH8sZhNoJRRqu3qhT7V81N2BT9XtwA9dDAicLPw7vHYEj1RnWrzV9srutd9k8T3BvMa1WqhuJoz2xofiYKj87"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": {
    "balance": {
      "LYR": 479861.48260583,
      "unittest/trans": 49999981238.24132
    },
    "height": 211,
    "unreceived": true,
    "txHash": "EXXE4dwMLnozTd3yzvWRe8ru5JLK7UVmDJpBYr9UV8VG"
  }
}
```


Client send:
```
{
  "jsonrpc": "2.0",
  "method": "Receive",
  "id": 1,
  "params": [
    "LU2BiJQtsMsUVw4Y6AbhveUPFc66iKWES3LZtNtXJNaomhKCSX1PErCzExTwim8Yv1fziSSuGuB49Emj44vjoEDpUFdK43"
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
    "8MQ4vd7fLm5wnMqsz7GSSFTTQrQYqvucm3nc7oCYmjZQ",
    "LU2BiJQtsMsUVw4Y6AbhveUPFc66iKWES3LZtNtXJNaomhKCSX1PErCzExTwim8Yv1fziSSuGuB49Emj44vjoEDpUFdK43"
  ]
}
```

## Signing message: 8MQ4vd7fLm5wnMqsz7GSSFTTQrQYqvucm3nc7oCYmjZQ

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": [
    "p1393",
    "5f6VeEgZBz6mW8KfTgNyaMRHKfsEhQYSpHzzgd2Z9Kj5VxZtr5VAXUWCb7sgHAsCaEWVWTBnFadSGLMUL4rBLm5h"
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
      "LYR": 2000.0
    },
    "height": 1,
    "unreceived": false
  }
}
```


Client send:
```
{
  "jsonrpc": "2.0",
  "method": "Receive",
  "id": 1,
  "params": [
    "LSLfaSr9pvWCHbFZ35YvzwCU7uXSMjHbDERthhEvUZ1zPs5LC9FnB5gCSLAQd537Qk9aXdjdcFjMgaPx7Gg3Aa4ugSM9eU"
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
    "FkuqbYSkdSxLpMsWdtczvdDCJhbeR6MvQZdvrNBLWfy1",
    "LSLfaSr9pvWCHbFZ35YvzwCU7uXSMjHbDERthhEvUZ1zPs5LC9FnB5gCSLAQd537Qk9aXdjdcFjMgaPx7Gg3Aa4ugSM9eU"
  ]
}
```

## Signing message: FkuqbYSkdSxLpMsWdtczvdDCJhbeR6MvQZdvrNBLWfy1

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": [
    "p1393",
    "XbWgf6bM3fnWKa8EwgPrfJZnZsaYptxVjvbYWuWQ957s9p2UjeFLByWEkhR2hq6e9e1bFKKeknPLBqBjmXR9G3x"
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
      "LYR": 2000.0
    },
    "height": 1,
    "unreceived": false
  }
}
```


# API: ProfitInfo CreateProfitingAccount(string accountId, string Name, ProfitingType ptype, decimal shareRito, int maxVoter)
/* Create a profiting account */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "CreateProfitingAccount",
  "id": 2,
  "params": [
    "LU2BiJQtsMsUVw4Y6AbhveUPFc66iKWES3LZtNtXJNaomhKCSX1PErCzExTwim8Yv1fziSSuGuB49Emj44vjoEDpUFdK43",
    "Pft1",
    "Node",
    "0.8",
    "100"
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
    "4pA6nMcMKmVTPBUWaVwXuJ76X6S1GX5XMEzo5mPTT4rT",
    "LU2BiJQtsMsUVw4Y6AbhveUPFc66iKWES3LZtNtXJNaomhKCSX1PErCzExTwim8Yv1fziSSuGuB49Emj44vjoEDpUFdK43"
  ]
}
```

## Signing message: 4pA6nMcMKmVTPBUWaVwXuJ76X6S1GX5XMEzo5mPTT4rT

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": [
    "p1393",
    "kpYaAZc5oPkkd61Q2g39BaRbqVPERaaBHv1QhbR1ydq4uE65AktAAAsvCdCsafo2JMuRcXHYFt7cLXkBv1zprw1"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": {
    "name": "Pft1",
    "type": "Node",
    "shareratio": 0.8,
    "seats": 100,
    "pftid": "La1VMyZ8c57CiuFXuEqrTK7uWXK4DF7uaL3FH4SJs1DG9FDbNPYDNQPNLC9aF8F8X8CPafQZxq6EPQHeSJDzAQs4SQmGch",
    "owner": "LU2BiJQtsMsUVw4Y6AbhveUPFc66iKWES3LZtNtXJNaomhKCSX1PErCzExTwim8Yv1fziSSuGuB49Emj44vjoEDpUFdK43"
  }
}
```


# API: StakingInfo CreateStakingAccount(string accountId, string Name, string voteFor, int daysToStake)
/* Staking to a profiting account */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "CreateStakingAccount",
  "id": 2,
  "params": [
    "LSLfaSr9pvWCHbFZ35YvzwCU7uXSMjHbDERthhEvUZ1zPs5LC9FnB5gCSLAQd537Qk9aXdjdcFjMgaPx7Gg3Aa4ugSM9eU",
    "Stk1",
    "La1VMyZ8c57CiuFXuEqrTK7uWXK4DF7uaL3FH4SJs1DG9FDbNPYDNQPNLC9aF8F8X8CPafQZxq6EPQHeSJDzAQs4SQmGch",
    "1000"
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
    "BqxdHnCb675YWrNJC6gBiCoFqcevENy6PfA99UHdhQEi",
    "LSLfaSr9pvWCHbFZ35YvzwCU7uXSMjHbDERthhEvUZ1zPs5LC9FnB5gCSLAQd537Qk9aXdjdcFjMgaPx7Gg3Aa4ugSM9eU"
  ]
}
```

## Signing message: BqxdHnCb675YWrNJC6gBiCoFqcevENy6PfA99UHdhQEi

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": [
    "p1393",
    "52i2mAn6fg6xkFhW7yfD8pqw54XBQu7wadPaEjb1zSXzVbvjSHQSERTSvwN9LSpe1Gv2mgqL2CCNnDSax64xkNwS"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": {
    "name": "Stk1",
    "voting": "La1VMyZ8c57CiuFXuEqrTK7uWXK4DF7uaL3FH4SJs1DG9FDbNPYDNQPNLC9aF8F8X8CPafQZxq6EPQHeSJDzAQs4SQmGch",
    "owner": "LSLfaSr9pvWCHbFZ35YvzwCU7uXSMjHbDERthhEvUZ1zPs5LC9FnB5gCSLAQd537Qk9aXdjdcFjMgaPx7Gg3Aa4ugSM9eU",
    "stkid": "L5gBGRpW1q5fpBPMdC8VfFQNVDqhZRcwpUqdagdtVjfZ3gJ5fSXHSaEgd5mVRa5eTNcupKEirLd3mCRb6gVC5WDHkHQptU",
    "days": 1000,
    "start": "2021-11-05T08:59:15.1358464Z",
    "amount": 0.0
  }
}
```


# API: bool AddStaking(string accountId, string stakingAccountId, decimal amount)
/* Add staking funds */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "AddStaking",
  "id": 3,
  "params": [
    "LSLfaSr9pvWCHbFZ35YvzwCU7uXSMjHbDERthhEvUZ1zPs5LC9FnB5gCSLAQd537Qk9aXdjdcFjMgaPx7Gg3Aa4ugSM9eU",
    "L5gBGRpW1q5fpBPMdC8VfFQNVDqhZRcwpUqdagdtVjfZ3gJ5fSXHSaEgd5mVRa5eTNcupKEirLd3mCRb6gVC5WDHkHQptU",
    "200"
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
    "Bft7UHSuHobk5kttqBcU2CuwJ24SZz88hn1YEMZ3iXuf",
    "LSLfaSr9pvWCHbFZ35YvzwCU7uXSMjHbDERthhEvUZ1zPs5LC9FnB5gCSLAQd537Qk9aXdjdcFjMgaPx7Gg3Aa4ugSM9eU"
  ]
}
```

## Signing message: Bft7UHSuHobk5kttqBcU2CuwJ24SZz88hn1YEMZ3iXuf

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 4,
  "result": [
    "p1393",
    "2LDveL7RFg8aEcH1pZhEB3ADED14kBJh4uYBWksiJX4YXVpvoexSukCzp2zN8zuXCgutcAuBGJMRoxBKRJbQ7SKX"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": true
}
```


# API: bool CreateDividends(string accountId, string profitingAccountId)
/* Create dividends */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "CreateDividends",
  "id": 3,
  "params": [
    "LU2BiJQtsMsUVw4Y6AbhveUPFc66iKWES3LZtNtXJNaomhKCSX1PErCzExTwim8Yv1fziSSuGuB49Emj44vjoEDpUFdK43",
    "La1VMyZ8c57CiuFXuEqrTK7uWXK4DF7uaL3FH4SJs1DG9FDbNPYDNQPNLC9aF8F8X8CPafQZxq6EPQHeSJDzAQs4SQmGch"
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
    "9gx2Cc3rAzUs1cJxRo9nnjBQ3JZdKy4JMPgQtaRp2Mdw",
    "LU2BiJQtsMsUVw4Y6AbhveUPFc66iKWES3LZtNtXJNaomhKCSX1PErCzExTwim8Yv1fziSSuGuB49Emj44vjoEDpUFdK43"
  ]
}
```

## Signing message: 9gx2Cc3rAzUs1cJxRo9nnjBQ3JZdKy4JMPgQtaRp2Mdw

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 4,
  "result": [
    "p1393",
    "2WYYrXo1mHTPt5xqMCpaf2H3jo9o2weWwfczLvpZXzKfxFhKp55QeTiqnVPLWbXirVo3wrtQ5MjHkrn2iFhZ6n9r"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": true
}
```


# API: bool UnStaking(string accountId, string stakingAccountId)
/* Unstaking */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "UnStaking",
  "id": 4,
  "params": [
    "LSLfaSr9pvWCHbFZ35YvzwCU7uXSMjHbDERthhEvUZ1zPs5LC9FnB5gCSLAQd537Qk9aXdjdcFjMgaPx7Gg3Aa4ugSM9eU",
    "L5gBGRpW1q5fpBPMdC8VfFQNVDqhZRcwpUqdagdtVjfZ3gJ5fSXHSaEgd5mVRa5eTNcupKEirLd3mCRb6gVC5WDHkHQptU"
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
    "GMo22yKVQxEW52DsLR6ETt52QgwsriZbqRdzJqx7EoPn",
    "LSLfaSr9pvWCHbFZ35YvzwCU7uXSMjHbDERthhEvUZ1zPs5LC9FnB5gCSLAQd537Qk9aXdjdcFjMgaPx7Gg3Aa4ugSM9eU"
  ]
}
```

## Signing message: GMo22yKVQxEW52DsLR6ETt52QgwsriZbqRdzJqx7EoPn

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 5,
  "result": [
    "p1393",
    "5TQ6SuW6wRzb6Qmq1m73SZzxAP6MYTuheGuLbjj6yGGhCi524TfywpMzca92rprpuGDLS9cUTMz8ZQpC5Mbja7H"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 4,
  "result": true
}
```


# API: ProfitInfo CreateProfitingAccount(string accountId, string Name, ProfitingType ptype, decimal shareRito, int maxVoter)
/* Create a profiting account */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "CreateProfitingAccount",
  "id": 5,
  "params": [
    "LSLfaSr9pvWCHbFZ35YvzwCU7uXSMjHbDERthhEvUZ1zPs5LC9FnB5gCSLAQd537Qk9aXdjdcFjMgaPx7Gg3Aa4ugSM9eU",
    "Pft Test",
    "Node",
    "0.5",
    "42"
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
    "8m54HifXV9H1CVhGkMuUFtQemRjawNSFD6vTH8sL5Lpr",
    "LSLfaSr9pvWCHbFZ35YvzwCU7uXSMjHbDERthhEvUZ1zPs5LC9FnB5gCSLAQd537Qk9aXdjdcFjMgaPx7Gg3Aa4ugSM9eU"
  ]
}
```

## Signing message: 8m54HifXV9H1CVhGkMuUFtQemRjawNSFD6vTH8sL5Lpr

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 6,
  "result": [
    "p1393",
    "46Sic1bWuBPLxdTPCXoir14GwnnvWLsZNEZH5HtaEpjqMGvcm7zUK4tJPyPPbTbS89FWAkmJCrijkwFgb5V5BwtV"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 5,
  "result": {
    "name": "Pft Test",
    "type": "Node",
    "shareratio": 0.5,
    "seats": 42,
    "pftid": "LGNQyHdYRJ5NyqmDNNx9RoN3sRim4zeMfdoWommXpjNG5pXJazqg1gE9TuqegEDGZVWm9bCUCdycmJnXgeDfWmtxdtcBue",
    "owner": "LSLfaSr9pvWCHbFZ35YvzwCU7uXSMjHbDERthhEvUZ1zPs5LC9FnB5gCSLAQd537Qk9aXdjdcFjMgaPx7Gg3Aa4ugSM9eU"
  }
}
```


# API: BrokerAccountsInfo GetBrokerAccounts(string accountId)
/* Get all broker accounts */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "GetBrokerAccounts",
  "id": 6,
  "params": [
    "LSLfaSr9pvWCHbFZ35YvzwCU7uXSMjHbDERthhEvUZ1zPs5LC9FnB5gCSLAQd537Qk9aXdjdcFjMgaPx7Gg3Aa4ugSM9eU"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 6,
  "result": {
    "owner": "LSLfaSr9pvWCHbFZ35YvzwCU7uXSMjHbDERthhEvUZ1zPs5LC9FnB5gCSLAQd537Qk9aXdjdcFjMgaPx7Gg3Aa4ugSM9eU",
    "profits": [
      {
        "name": "Pft Test",
        "type": "Node",
        "shareratio": 0.5,
        "seats": 42,
        "pftid": "LGNQyHdYRJ5NyqmDNNx9RoN3sRim4zeMfdoWommXpjNG5pXJazqg1gE9TuqegEDGZVWm9bCUCdycmJnXgeDfWmtxdtcBue",
        "owner": "LSLfaSr9pvWCHbFZ35YvzwCU7uXSMjHbDERthhEvUZ1zPs5LC9FnB5gCSLAQd537Qk9aXdjdcFjMgaPx7Gg3Aa4ugSM9eU"
      }
    ],
    "stakings": [
      {
        "name": "Stk1",
        "voting": "La1VMyZ8c57CiuFXuEqrTK7uWXK4DF7uaL3FH4SJs1DG9FDbNPYDNQPNLC9aF8F8X8CPafQZxq6EPQHeSJDzAQs4SQmGch",
        "owner": "LSLfaSr9pvWCHbFZ35YvzwCU7uXSMjHbDERthhEvUZ1zPs5LC9FnB5gCSLAQd537Qk9aXdjdcFjMgaPx7Gg3Aa4ugSM9eU",
        "stkid": "L5gBGRpW1q5fpBPMdC8VfFQNVDqhZRcwpUqdagdtVjfZ3gJ5fSXHSaEgd5mVRa5eTNcupKEirLd3mCRb6gVC5WDHkHQptU",
        "days": 1000,
        "start": "2021-11-05T08:59:15.1358464Z",
        "amount": 0.0
      }
    ]
  }
}
```


# API: PoolInfo Pool(string token0, string token1)
/* get pool info */

Client send:
```
{
  "jsonrpc": "2.0",
  "method": "Pool",
  "id": 8,
  "params": [
    "LYR",
    "testit/json-361478"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 8,
  "error": {
    "data": {
      "type": "System.Exception",
      "message": "Failed to get pool",
      "stack": "   at Lyra.Node.JsonRpcServer.PoolAsync(String token0, String token1) in C:\\Users\\wizard\\Source\\Repos\\LyraNetwork\\Core\\Lyra.Node2\\Services\\JsonRpcServer.cs:line 257",
      "code": -2146233088
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
  "id": 9,
  "params": [
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
    "LYR",
    "testit/json-361478"
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
    "8w6BbHuoBbXZFuYCxJwcrYMzJ3XJbsBZmYe2ebvUwVz9",
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF"
  ]
}
```

## Signing message: 8w6BbHuoBbXZFuYCxJwcrYMzJ3XJbsBZmYe2ebvUwVz9

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 11,
  "result": [
    "p1393",
    "29UqzEXt5hhUqkghtzVMYQG58gKEt24zfxVLBXB6fjSaKWuJXubQzaHr1FEtGAh4XftXWAn5EAF4sxMJyHdTuiXC"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 9,
  "result": {
    "poolId": "L3WPJnV3cLbon1QRfvcR74HuYwV1LZ8feK5ZVmYMvJyGS7XZBVMNA7zQhUbsD2ah6GcLz9zEYPiGhLdo3hsb2Eqby6cYp3",
    "height": 1,
    "token0": "LYR",
    "token1": "testit/json-361478",
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
  "id": 10,
  "params": [
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
    "LYR",
    "1000",
    "testit/json-361478",
    "5000000"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 12,
  "method": "Sign",
  "params": [
    "hash",
    "7fXEnHrraPzCGbuE1W4uByhiUyU3YfakQ5VK4vtQQe9c",
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF"
  ]
}
```

## Signing message: 7fXEnHrraPzCGbuE1W4uByhiUyU3YfakQ5VK4vtQQe9c

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 12,
  "result": [
    "p1393",
    "2KJCd5WU1ai8tgrdFw3SZmQUuu2sMYyuA5Bja7MXciVoY11MuP1EZPWCpmRUqPjLyKTBHq6CjHSJiZuGBtiE4rBf"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 10,
  "result": {
    "poolId": "L3WPJnV3cLbon1QRfvcR74HuYwV1LZ8feK5ZVmYMvJyGS7XZBVMNA7zQhUbsD2ah6GcLz9zEYPiGhLdo3hsb2Eqby6cYp3",
    "height": 2,
    "token0": "LYR",
    "token1": "testit/json-361478",
    "balance": {
      "testit/json-361478": 5000000.0,
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
  "id": 11,
  "params": [
    "L3WPJnV3cLbon1QRfvcR74HuYwV1LZ8feK5ZVmYMvJyGS7XZBVMNA7zQhUbsD2ah6GcLz9zEYPiGhLdo3hsb2Eqby6cYp3",
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
  "id": 11,
  "result": {
    "ProviderFee": 0.003,
    "ProtocolFee": 0.001,
    "SwapInToken": "LYR",
    "SwapInAmount": 100.0,
    "SwapOutToken": "testit/json-361478",
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
  "id": 12,
  "params": [
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
    "LYR",
    "testit/json-361478",
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
  "id": 13,
  "method": "Sign",
  "params": [
    "hash",
    "B6eiVmN23hMCLM3s32Dm2tAmiu3B1FPEp8CRd9Nz2APK",
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF"
  ]
}
```

## Signing message: B6eiVmN23hMCLM3s32Dm2tAmiu3B1FPEp8CRd9Nz2APK

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 13,
  "result": [
    "p1393",
    "3ytTcbbP8SRZc26EmRT7kBHzBZeLQHb1Z9hzs48CxKzP6mdSnfYCREqxcLM8dRbWtvBZC9HMHX5m8BZ5u7bD5XGn"
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
        "from": "L3WPJnV3cLbon1QRfvcR74HuYwV1LZ8feK5ZVmYMvJyGS7XZBVMNA7zQhUbsD2ah6GcLz9zEYPiGhLdo3hsb2Eqby6cYp3",
        "to": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
        "sendHash": "HRmKAnHQWgSCbeGk8N7aYnoR5LEPRNdPr2encUL3hGzK",
        "funds": {
          "testit/json-361478": 452891.96071299
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Receiving","content":{"from":"L3WPJnV3cLbon1QRfvcR74HuYwV1LZ8feK5ZVmYMvJyGS7XZBVMNA7zQhUbsD2ah6GcLz9zEYPiGhLdo3hsb2Eqby6cYp3","to":"LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF","sendHash":"HRmKAnHQWgSCbeGk8N7aYnoR5LEPRNdPr2encUL3hGzK","funds":{"testit/json-361478":452891.96071299}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "method": "Notify",
  "params": [
    {
      "catalog": "Receiving",
      "content": {
        "from": "L3WPJnV3cLbon1QRfvcR74HuYwV1LZ8feK5ZVmYMvJyGS7XZBVMNA7zQhUbsD2ah6GcLz9zEYPiGhLdo3hsb2Eqby6cYp3",
        "to": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
        "sendHash": "HRmKAnHQWgSCbeGk8N7aYnoR5LEPRNdPr2encUL3hGzK",
        "funds": {
          "testit/json-361478": 452891.96071299
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Receiving","content":{"from":"L3WPJnV3cLbon1QRfvcR74HuYwV1LZ8feK5ZVmYMvJyGS7XZBVMNA7zQhUbsD2ah6GcLz9zEYPiGhLdo3hsb2Eqby6cYp3","to":"LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF","sendHash":"HRmKAnHQWgSCbeGk8N7aYnoR5LEPRNdPr2encUL3hGzK","funds":{"testit/json-361478":452891.96071299}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 14,
  "method": "Sign",
  "params": [
    "hash",
    "DLCp7JjgH7upxYw2QFtRcdw65ZuL13yrm1fi6HDSmPdM",
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF"
  ]
}
```

## Signing message: DLCp7JjgH7upxYw2QFtRcdw65ZuL13yrm1fi6HDSmPdM

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 14,
  "result": [
    "p1393",
    "3sf5VFtFq8xyFYYGMwsM9Q75hwAbyLVPrh7Ldpo6ZgvyXFp7Yy5oUhVAoJcFp9QS47BgMrz4DkymHWidJpNJ6DcF"
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
        "recvHash": "DLCp7JjgH7upxYw2QFtRcdw65ZuL13yrm1fi6HDSmPdM",
        "to": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
        "sendHash": "HRmKAnHQWgSCbeGk8N7aYnoR5LEPRNdPr2encUL3hGzK",
        "funds": {
          "testit/json-361478": 452891.96071299
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Settlement","content":{"recvHash":"DLCp7JjgH7upxYw2QFtRcdw65ZuL13yrm1fi6HDSmPdM","to":"LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF","sendHash":"HRmKAnHQWgSCbeGk8N7aYnoR5LEPRNdPr2encUL3hGzK","funds":{"testit/json-361478":452891.96071299}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "method": "Notify",
  "params": [
    {
      "catalog": "Settlement",
      "content": {
        "recvHash": "DLCp7JjgH7upxYw2QFtRcdw65ZuL13yrm1fi6HDSmPdM",
        "to": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
        "sendHash": "HRmKAnHQWgSCbeGk8N7aYnoR5LEPRNdPr2encUL3hGzK",
        "funds": {
          "testit/json-361478": 452891.96071299
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Settlement","content":{"recvHash":"DLCp7JjgH7upxYw2QFtRcdw65ZuL13yrm1fi6HDSmPdM","to":"LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF","sendHash":"HRmKAnHQWgSCbeGk8N7aYnoR5LEPRNdPr2encUL3hGzK","funds":{"testit/json-361478":452891.96071299}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 12,
  "result": {
    "balance": {
      "testit/json-361478": 5452891.96071299,
      "LYR": 124185.8,
      "testit/json-1556709": 10000000.0,
      "testit/json-4555140": 10000000.0,
      "testit/json-1979343": 10000000.0,
      "testit/json-2737471": 10000000.0,
      "testit/json-3855456": 10000000.0,
      "testit/json-6351728": 3000000.0,
      "testit/json-2332708": 3000000.0
    },
    "height": 48,
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
  "id": 13,
  "params": [
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
    "LYR",
    "testit/json-361478"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 15,
  "method": "Sign",
  "params": [
    "hash",
    "8EJjVkUganNXrwrKm8MUKdPNWzvvyMjt56mLDkujbteF",
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF"
  ]
}
```

## Signing message: 8EJjVkUganNXrwrKm8MUKdPNWzvvyMjt56mLDkujbteF

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 15,
  "result": [
    "p1393",
    "3t9GAgicYPY1wRH4L9zMdjAC5gns9BBf82QZA2wWAK48a6JUq5NxiUVh7FMt1UYW3J7hyAxTa8KDXMSeE7xd6YQm"
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
        "from": "L3WPJnV3cLbon1QRfvcR74HuYwV1LZ8feK5ZVmYMvJyGS7XZBVMNA7zQhUbsD2ah6GcLz9zEYPiGhLdo3hsb2Eqby6cYp3",
        "to": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
        "sendHash": "CqNeA8XkFJbDFK55sr1TrxoZYw3YYKgA5JWBbKipK2zj",
        "funds": {
          "testit/json-361478": 4547108.03928701,
          "LYR": 1099.9
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Receiving","content":{"from":"L3WPJnV3cLbon1QRfvcR74HuYwV1LZ8feK5ZVmYMvJyGS7XZBVMNA7zQhUbsD2ah6GcLz9zEYPiGhLdo3hsb2Eqby6cYp3","to":"LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF","sendHash":"CqNeA8XkFJbDFK55sr1TrxoZYw3YYKgA5JWBbKipK2zj","funds":{"testit/json-361478":4547108.03928701,"LYR":1099.9}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "method": "Notify",
  "params": [
    {
      "catalog": "Receiving",
      "content": {
        "from": "L3WPJnV3cLbon1QRfvcR74HuYwV1LZ8feK5ZVmYMvJyGS7XZBVMNA7zQhUbsD2ah6GcLz9zEYPiGhLdo3hsb2Eqby6cYp3",
        "to": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
        "sendHash": "CqNeA8XkFJbDFK55sr1TrxoZYw3YYKgA5JWBbKipK2zj",
        "funds": {
          "testit/json-361478": 4547108.03928701,
          "LYR": 1099.9
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Receiving","content":{"from":"L3WPJnV3cLbon1QRfvcR74HuYwV1LZ8feK5ZVmYMvJyGS7XZBVMNA7zQhUbsD2ah6GcLz9zEYPiGhLdo3hsb2Eqby6cYp3","to":"LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF","sendHash":"CqNeA8XkFJbDFK55sr1TrxoZYw3YYKgA5JWBbKipK2zj","funds":{"testit/json-361478":4547108.03928701,"LYR":1099.9}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 16,
  "method": "Sign",
  "params": [
    "hash",
    "9hpi9o8vXmCP9ehxu4hYpQpVD2ynwTKGuoRX6fDHpcov",
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF"
  ]
}
```

## Signing message: 9hpi9o8vXmCP9ehxu4hYpQpVD2ynwTKGuoRX6fDHpcov

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 16,
  "result": [
    "p1393",
    "5GtVSgxEnP5ERqnTjhYMgdVVe1uXPbCD524zouNyvWYbEABULy1z92zuxjnpVNXTsytRFNYkGDjHFE7HGx8idv1u"
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
        "recvHash": "9hpi9o8vXmCP9ehxu4hYpQpVD2ynwTKGuoRX6fDHpcov",
        "to": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
        "sendHash": "CqNeA8XkFJbDFK55sr1TrxoZYw3YYKgA5JWBbKipK2zj",
        "funds": {
          "testit/json-361478": 4547108.03928701,
          "LYR": 1099.9
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Settlement","content":{"recvHash":"9hpi9o8vXmCP9ehxu4hYpQpVD2ynwTKGuoRX6fDHpcov","to":"LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF","sendHash":"CqNeA8XkFJbDFK55sr1TrxoZYw3YYKgA5JWBbKipK2zj","funds":{"testit/json-361478":4547108.03928701,"LYR":1099.9}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "method": "Notify",
  "params": [
    {
      "catalog": "Settlement",
      "content": {
        "recvHash": "9hpi9o8vXmCP9ehxu4hYpQpVD2ynwTKGuoRX6fDHpcov",
        "to": "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
        "sendHash": "CqNeA8XkFJbDFK55sr1TrxoZYw3YYKgA5JWBbKipK2zj",
        "funds": {
          "testit/json-361478": 4547108.03928701,
          "LYR": 1099.9
        }
      }
    }
  ]
}
```

> Notify from server: {"jsonrpc":"2.0","method":"Notify","params":[{"catalog":"Settlement","content":{"recvHash":"9hpi9o8vXmCP9ehxu4hYpQpVD2ynwTKGuoRX6fDHpcov","to":"LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF","sendHash":"CqNeA8XkFJbDFK55sr1TrxoZYw3YYKgA5JWBbKipK2zj","funds":{"testit/json-361478":4547108.03928701,"LYR":1099.9}}}]}
Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 13,
  "result": {
    "balance": {
      "testit/json-361478": 10000000.0,
      "LYR": 125283.7,
      "testit/json-1556709": 10000000.0,
      "testit/json-4555140": 10000000.0,
      "testit/json-1979343": 10000000.0,
      "testit/json-2737471": 10000000.0,
      "testit/json-3855456": 10000000.0,
      "testit/json-6351728": 3000000.0,
      "testit/json-2332708": 3000000.0
    },
    "height": 50,
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
  "id": 14,
  "params": [
    "LYR",
    "testit/json-361478"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 14,
  "result": {
    "poolId": "L3WPJnV3cLbon1QRfvcR74HuYwV1LZ8feK5ZVmYMvJyGS7XZBVMNA7zQhUbsD2ah6GcLz9zEYPiGhLdo3hsb2Eqby6cYp3",
    "height": 5,
    "token0": "LYR",
    "token1": "testit/json-361478",
    "balance": {
      "testit/json-361478": 0.0,
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
  "id": 15,
  "params": [
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF",
    "LYR",
    "300",
    "testit/json-361478",
    "7000000"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 17,
  "method": "Sign",
  "params": [
    "hash",
    "Gdff9M9ZGmiYzNnVf4dehZmNCXVXcWVEabZxQXtuMFRo",
    "LT29bEJF2WafaGppVQBcXtCKc93XjK77mv4k5qrfGiSAWPNUEmBgCzBRMsh9wpg3FLM1er63cPZ8NCMeBBWuGrJ9d1xwcF"
  ]
}
```

## Signing message: Gdff9M9ZGmiYzNnVf4dehZmNCXVXcWVEabZxQXtuMFRo

Client send:
```
{
  "jsonrpc": "2.0",
  "id": 17,
  "result": [
    "p1393",
    "2xQ7atB7FgQ93jkZg39itdAP8Ea6WH7Pq5Y5LSD6Pu22ibXrdhF8DKbPPpSpH16Tf1A4SK7B2Gu4cqtgJX3TUtP7"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 15,
  "result": {
    "poolId": "L3WPJnV3cLbon1QRfvcR74HuYwV1LZ8feK5ZVmYMvJyGS7XZBVMNA7zQhUbsD2ah6GcLz9zEYPiGhLdo3hsb2Eqby6cYp3",
    "height": 6,
    "token0": "LYR",
    "token1": "testit/json-361478",
    "balance": {
      "testit/json-361478": 7000000.0,
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
  "id": 16,
  "params": [
    "LYR",
    "testit/json-361478"
  ]
}
```

Server reply:
```
{
  "jsonrpc": "2.0",
  "id": 16,
  "result": {
    "poolId": "L3WPJnV3cLbon1QRfvcR74HuYwV1LZ8feK5ZVmYMvJyGS7XZBVMNA7zQhUbsD2ah6GcLz9zEYPiGhLdo3hsb2Eqby6cYp3",
    "height": 6,
    "token0": "LYR",
    "token1": "testit/json-361478",
    "balance": {
      "testit/json-361478": 7000000.0,
      "LYR": 300.0
    }
  }
}
```

