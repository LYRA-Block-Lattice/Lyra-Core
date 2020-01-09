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

9. verify

https://localhost:4505/api/LyraNode/GetSyncState
should return like:
`{"mode":0,"newestBlockUIndex":8,"resultCode":0,"resultMessage":null}`
mode 0 is normal, mode 1 is syncing blocks.

https://localhost:4505/api/LyraNode/GetBillboard
display all connected nodes.


# "Tokenomics"

The following model is being considered for the native/gas tokens for Lyra.

* keep the LYRA native/gas token max supply the same as GRFT (1.8B)
* convert portion of the GRAFT Reserve funds into LYRA and operate the first set of nodes ourselves (this will give us a chance to flush out the DPOS)
* Once DPOS is flushed out, put together an exchange gateway that will allow GRFT holders to exchange their GRFT for Lyra.GRFT

### Participation

1. Authorizers. (21-seeds) primary +21 backup nodes, run by the community
2. Seeds and zookeepers: 3~11 nodes run by dev team.
3. Voters. Vote for the authorizers via DPOS.

All nodes share evenly.  Authorizers are free to share their profits with the voters.

# Roadmap

1) Initial Block lattice Implementation (generate blocks, accept blocks), single node - DONE
2) CLI client - DONE
3) UI client - DONE
4) Multi-node implementation (P2P communication, consensus) - In Progress
5) TestNet -  * Launching *
)6 DPOS - TBD
8) Mainnet (closed loop, multi-token, single blockchain) - TBD
9) Mainnet reference application (loyalty programs, decentralized ecommerce) - TBD
10) Alt-chain account binding, Exchange broker - TBD
11) Mainnet alt-chain support for alt-chain payments - TBD



