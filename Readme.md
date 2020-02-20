### [About Lyra DAG](https://github.com/graft-project/LyraNetwork/wiki)
### [Tokenomics](https://github.com/graft-project/LyraNetwork/wiki/Tokenomics)
### [Roadmap](https://github.com/graft-project/LyraNetwork/wiki/Roadmap)

# Note
Testnet Live Now! (From 10 Jan 2020)

Stable Release: https://github.com/graft-project/LyraNetwork/releases

Nightly/Testing Releases: https://github.com/wizd/LyraNetwork/releases

# Lyra Permissionless Node Setup

Note: 

* for windows 10 Installation please reference community contributed guide: https://docs.google.com/document/d/1DThxgbWSt_79zr2Rwnl0SaJah1wR-SmHxKrkeYOtzTM/edit Many thanks to our community member Roggy Dog.
* for docker: http://gitlab.com/uiii/lyranetwork-node Many thanks to our community member Jan Svobada.

1. Install Linux (Ubuntu 18.04), or Windows, macOS

[https://github.com/dotnet/core/blob/master/release-notes/3.1/3.1-supported-os.md](https://github.com/dotnet/core/blob/master/release-notes/3.1/3.1-supported-os.md)

2. Install Mongodb 4.2 Community Edition

[https://docs.mongodb.com/manual/tutorial/install-mongodb-on-ubuntu/](https://docs.mongodb.com/manual/tutorial/install-mongodb-on-ubuntu/)

​	2.1 Enable mongodb security by this guide: [https://medium.com/mongoaudit/how-to-enable-authentication-on-mongodb-b9e8a924efac](https://medium.com/mongoaudit/how-to-enable-authentication-on-mongodb-b9e8a924efac)

3. Install dotnet core 3.1 LTS

https://dotnet.microsoft.com/download/dotnet-core/3.1

Install the ASP.NET Core runtime

4. download Lyra releases from https://github.com/graft-project/LyraNetwork/releases to a folder, e.g. ~/lyra.permissionless-1.0.6.tar.gz

`tar -xjvf lyra.permissionless-1.0.6.tar.gz`

5. create mongodb user

`mongo`  
`use lyra`  
`db.createUser({user:'lexuser',pwd:'alongpassword',roles:[{role:'readWrite',db:'lyra'}]})`  
`use dex`  
`db.createUser({user:'lexuser',pwd:'alongpassword',roles:[{role:'readWrite',db:'dex'}]})`

6. generate staking wallet by, give the wallet a name, e.g. "poswallet"

`dotnet ~/lyra/cli/lyracli.dll --networkid testnet -p webapi -g poswallet`

7. modify ~/lyra/node/config.testnet.json, change monodb account/password, change the wallet/name (was poswallet) to the name you created previous step.


8. run. (remember to set environment variable LYRA_NETWORK to testnet/mainnet etc.)

`dotnet dev-certs https --clean`

`dotnet dev-certs https`

`cd ~/lyra/node`

`export LYRA_NETWORK=testnet`

`dotnet Lyra.Node2.dll`

9. verify

https://localhost:4505/api/LyraNode/GetSyncState
should return like:
`{"mode":0,"newestBlockUIndex":8,"resultCode":0,"resultMessage":null}`
mode 0 is normal, mode 1 is syncing blocks.

https://localhost:4505/api/LyraNode/GetBillboard
display all connected nodes.

10. refresh POS wallet balance (when node not running)

`dotnet ~/lyra/cli/lyracli.dll --networkid testnet -p webapi`

`poswallet`

`sync`

`balance`

`stop`




