# Note
Testnet launching date is not set yet, but very soon.

# Lyra Permissionless Node Setup

1. Install Linux (Ubuntu 18.04), or Windows, macOS

[https://github.com/dotnet/core/blob/master/release-notes/3.1/3.1-supported-os.md](https://github.com/dotnet/core/blob/master/release-notes/3.1/3.1-supported-os.md)

2. Install Mongodb 4.2 Community Edition

[https://docs.mongodb.com/manual/tutorial/install-mongodb-on-ubuntu/](https://docs.mongodb.com/manual/tutorial/install-mongodb-on-ubuntu/)

​	2.1 Enable mongodb security by this guide: [https://medium.com/mongoaudit/how-to-enable-authentication-on-mongodb-b9e8a924efac](https://medium.com/mongoaudit/how-to-enable-authentication-on-mongodb-b9e8a924efac)

3. Install dotnet core 3.1 LTS

https://dotnet.microsoft.com/download/dotnet-core/3.1

Install the ASP.NET Core runtime

4. copy LyraNode2 releases to ~/lyranode2

`scp -rp ./Lyra.Node2/* user@[server address]:~/lyranode2`

5. create mongodb user

`mongo`  
`use lyra`  
`db.createUser({user:'lexuser',pwd:'alongpassword',roles:[{role:'readWrite',db:'lyra'}]})`  
`use dex`  
`db.createUser({user:'lexuser',pwd:'alongpassword',roles:[{role:'readWrite',db:'dex'}]})`

6. generate staking wallet by, give the wallet a name, e.g. "poswallet"

   lyracli.exe --networkid testnet -p webapi -g poswallet

7. modify lyranode2\appsettings.json, change monodb account/password, change the wallet/name to the name you created previous step.


8. run

`dotnet Lyra.Node2.dll`

