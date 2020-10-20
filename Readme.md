# Index

[LYRA Node Setup](#lyra-node-setup)

[Run noded as systemd service](#run-noded-as-systemd-service)

# What's New

[LYRA Documentation Site](https://docs.lyra.live) - 
[LYRA Website](https://lyra.live) - 
[LYRA on Twitter](https://twitter.com/LYRAblockchain) -
[LYRA white paper](https://github.com/LYRA-Block-Lattice/LYRA-Docs/blob/master/LYRA-BLock-Lattice-White-Paper.md) -
[LYRA on Bitcointalk](https://bitcointalk.org/index.php?topic=5258803.msg) -
[LYRA on LinkedIn](https://www.linkedin.com/company/lyra-block-lattice)

Mainnet Live Now! (from Sep. 30, 2020)

[Mainnet Block Explorer](https://nebula.lyra.live/)

Testnet Live Now! (From July 29, 2020)

[Testnet Block Explorer](https://blockexplorer.testnet.lyra.live/) - 
[How to run Lyra CLI wallet on testnet](https://github.com/LYRA-Block-Lattice/LYRA-Docs/blob/master/How%20to%20run%20Lyra%20CLI%20Wallet%20on%20testnet.md)

# LYRA Client Developer Guide

Please reference [LyraCodeExamples](https://github.com/LYRA-Block-Lattice/LyraCodeExamples).


# LYRA Node Setup

Notes:
* You don’t need to run Lyra node in order to use the Lyra network. Simply install CLI wallet by following [How to run Lyra CLI wallet on testnet](https://github.com/LYRA-Block-Lattice/LYRA-Docs/blob/master/How%20to%20run%20Lyra%20CLI%20Wallet%20on%20testnet.md) guide and start exploring the network by making transfers or even creating your own tokens! Mobile wallets are coming soon as well.
* [Lyra Testnet Node Windows Setup Guide](https://github.com/LYRA-Block-Lattice/LYRA-Docs/blob/master/Lyra%20Testnet%20Node%20%20Windows%20Setup%20Guide.md)
* [Detailed instructions for Ubuntu 18](https://github.com/MorgothCreator/LYRA-node-setup-on-UBUNTU-18.0.4/blob/main/README.md) - thanks to our commmunity memeber MorgothCreator! 
* For docker setup: http://gitlab.com/uiii/lyranetwork-node Many thanks to our community member Jan Svobada.

1. Install Linux (Ubuntu 18.04), or Windows, macOS

[https://github.com/dotnet/core/blob/master/release-notes/3.1/3.1-supported-os.md](https://github.com/dotnet/core/blob/master/release-notes/3.1/3.1-supported-os.md)

2. Install Mongodb 4.2 Community Edition

[https://docs.mongodb.com/manual/tutorial/install-mongodb-on-ubuntu/](https://docs.mongodb.com/manual/tutorial/install-mongodb-on-ubuntu/)

​	2.1 Enable mongodb security by this guide: [https://medium.com/mongoaudit/how-to-enable-authentication-on-mongodb-b9e8a924efac](https://medium.com/mongoaudit/how-to-enable-authentication-on-mongodb-b9e8a924efac)

3. Install dotnet core 3.1 LTS

https://dotnet.microsoft.com/download/dotnet-core/3.1

Install the ASP.NET Core runtime

4. download **the latest** release from https://github.com/LYRA-Block-Lattice/Lyra-Core/releases to a folder, for example, ~/lyra.permissionless-1.7.6.15tar.gz

`tar -xjvf lyra.permissionless-1.7.6.15.tar.gz`

5. create mongodb user

`mongo`  
`use lyra`  
`db.createUser({user:'lexuser',pwd:'alongpassword',roles:[{role:'readWrite',db:'lyra'}]})`  
`use dex`  
`db.createUser({user:'lexuser',pwd:'alongpassword',roles:[{role:'readWrite',db:'dex'}]})`

6. generate staking wallet by, give the wallet a name, e.g. "poswallet"

`dotnet ~/lyra/cli/lyra.dll --networkid testnet -p webapi -g poswallet`

7. modify ~/lyra/noded/config.testnet.json

change monodb account/password, change the wallet/name (was poswallet) to the name you created previous step. change the wallet/password if your wallet has a password.
or see step 12


8. run. (remember to set environment variable LYRA_NETWORK to testnet/mainnet etc.)

`dotnet dev-certs https --clean`

`dotnet dev-certs https`

`cd ~/lyra/noded`

`export LYRA_NETWORK=testnet`

`dotnet lyra.noded.dll`

9. verify

https://localhost:4505/api/Node/GetSyncState
should return like:
`{"mode":0,"newestBlockUIndex":8,"resultCode":0,"resultMessage":null}`
mode 0 is normal, mode 1 is syncing blocks.

https://localhost:4505/api/Node/GetBillboard
display all connected nodes.

10. refresh DPoS wallet balance

`dotnet ~/lyra/cli/lyra.dll --networkid testnet -p webapi`

`poswallet`

`sync`

`balance`

`stop`

11. set DPoS vote Account ID

use "votefor" command in wallet cli.

12. configure from environment varabiles (seprated by double underscore)

`export LYRA_ApplicationConfiguration__LyraNode__Lyra__Database__DBConnect=mongodb://user:alongpassword@127.0.0.1/lyra`

# Run noded as systemd service

1, create /etc/systemd/system/kestrel-noded.service (replace [username] with your user name, change mongodb login)

`[Unit]
Description=Lyra node daemon

[Service]
WorkingDirectory=/home/[username]/lyra/noded
ExecStart=/usr/bin/dotnet /home/[username]/lyra/noded/lyra.noded.dll
Restart=always
# Restart service after 10 seconds if the dotnet service crashes:
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=lyra-noded
User=[username]
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
Environment=LYRA_NETWORK=testnet
Environment=LYRA_ApplicationConfiguration__LyraNode__Lyra__Database__DBConnect=mongodb://lexuser:alongpassword@127.0.0.1/lyra
Environment=ASPNETCORE_URLS=http://*:4505;https://*:4504

[Install]
WantedBy=multi-user.target`

2, run these command to start noded service

`sudo systemctl daemon-reload
sudo systemctl restart kestrel-noded`

3, view noded output

`sudo journalctl -u kestrel-noded -f`



