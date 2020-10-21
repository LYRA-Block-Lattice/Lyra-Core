# Index

[LYRA Node Setup](#lyra-node-setup)

[Run Lyra Node Daemon](#run-lyra-node-daemon)

[Run Lyra Node Daemon as systemd service](#run-noded-as-systemd-service)

[Run noded as Windows service](#run-noded-as-windows-service)

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

	```
	tar -xjvf lyra.permissionless-1.7.6.15.tar.gz
	```

5. create mongodb user

	```
	mongo  
	use lyra  
	db.createUser({user:'lexuser',pwd:'alongpassword',roles:[{role:'readWrite',db:'lyra'}]})  
	use dex  
	db.createUser({user:'lexuser',pwd:'alongpassword',roles:[{role:'readWrite',db:'dex'}]})
	```

6. generate staking wallet by, give the wallet a name, e.g. "poswallet"

	```
	dotnet ~/lyra/cli/lyra.dll --networkid testnet -p webapi -g poswallet
	```

7. modify ~/lyra/noded/config.testnet.json

change monodb account/password, change the wallet/name (was poswallet) to the name you created previous step. change the wallet/password if your wallet has a password.
or see step 12

# Run Lyra node daemon

* testnet
	```
	dotnet dev-certs https --clean
	dotnet dev-certs https
	cd ~/lyra/noded
	export LYRA_NETWORK=testnet
	export ASPNETCORE_URLS=http://*:4505\;https://*:4504
	export ASPNETCORE_HTTPS_PORT=4504
	# optional mongodb credential if not specified in config*.json
	# export LYRA_ApplicationConfiguration__LyraNode__Lyra__Database__DBConnect=mongodb://user:alongpassword@127.0.0.1/lyra
	dotnet lyra.noded.dll
	```

* mainnet
	```
	dotnet dev-certs https --clean
	dotnet dev-certs https
	cd ~/lyra/noded
	export LYRA_NETWORK=mainnet
	export ASPNETCORE_URLS=http://*:5505\;https://*:5504
	export ASPNETCORE_HTTPS_PORT=5504
	# optional mongodb credential if not specified in config*.json
	# export LYRA_ApplicationConfiguration__LyraNode__Lyra__Database__DBConnect=mongodb://user:alongpassword@127.0.0.1/lyra
	dotnet lyra.noded.dll
	```

1. verify (use port 5504 for mainnet)

https://localhost:4504/api/Node/GetSyncState
should return like:

	{"mode":0,"newestBlockUIndex":8,"resultCode":0,"resultMessage":null}
	mode 0 is normal, mode 1 is syncing blocks.

https://localhost:4504/api/Node/GetBillboard
display all connected nodes.

2. refresh DPoS wallet balance

	```
	dotnet ~/lyra/cli/lyra.dll --networkid testnet -p webapi
	poswallet
	sync
	balance
	stop
	```

3. set DPoS vote Account ID

use "votefor" command in wallet cli.


# Run noded as systemd service

1, create /etc/systemd/system/lyra-noded.service (replace [username] with your user name, change mongodb login)

	
	[Unit]
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
	# optional mongodb credential if not specified in config*.json
	# Environment=LYRA_ApplicationConfiguration__LyraNode__Lyra__Database__DBConnect=mongodb://lexuser:alongpassword@127.0.0.1/lyra

	# for mainnet
	# Environment=LYRA_NETWORK=mainnet
	# Environment=ASPNETCORE_URLS=http://*:5505;https://*:5504
	# Environment=ASPNETCORE_HTTPS_PORT=5504

	# for testnet
	Environment=LYRA_NETWORK=testnet
	Environment=ASPNETCORE_URLS=http://*:4505;https://*:4504
	Environment=ASPNETCORE_HTTPS_PORT=4504

	# if use Engix front end
	# Environment=ASPNETCORE_HTTPS_PORT=443

	[Install]
	WantedBy=multi-user.target
	

2, run these command to start noded service

	
	sudo systemctl daemon-reload
	sudo systemctl restart lyra-noded
	

3, view noded output

	
	sudo journalctl -u lyra-noded -f
	

# Run noded as Windows service

	# remember to set all environment variables.
	# add rights "Log on as a service" to current user. (because it need POS wallet in current user's folder)
	#     secpol.msc -> Local Policies -> User Rights Assignment -> Log on as a service -> add current user
	New-Service -Name lyranoded -BinaryPathName "path\to\lyra\noded\lyra.noded.exe" -Credential "[domain/computer name]\current user" -Description "Lyra Node Daemon provides authorization service for Lyra network." -DisplayName "Lyra Node Daemon" -StartupType Automatic
	Start-Service -Name lyranoded

* powershell script for service creation/auto upgrading

	 param (
		[string]$url = ""
	 )

	Function ServiceExists([string] $ServiceName) {
		[bool] $Return = $False
		if ( Get-WmiObject -Class Win32_Service -Filter "Name='$ServiceName'" ) {
			$Return = $True
		}
		Return $Return
	}

	$lyrasvc = "lyranoded"

	write-output "downoad from " + $url

	#Remove-Item lyra -Force -Recurse

	Invoke-WebRequest $url -OutFile lyra.tar.bz2

	& "C:\Program Files\7-Zip\7z.exe" x lyra.tar.bz2
	& "C:\Program Files\7-Zip\7z.exe" x lyra.tar

	Remove-Item lyra.tar* -Force

	[bool] $thisServiceExists = ServiceExists $lyrasvc


	if($thisServiceExists)
	{
		Stop-Service -Name $lyrasvc
	}

	Start-Sleep -s 3
	& cmd /c rd /q/s C:\Users\Administrator\lyra\
	Start-Sleep -s 3
	& cmd /c rd /q/s C:\Users\Administrator\lyra\

	Move-Item -Path .\lyra -Destination C:\Users\Administrator -force

	if(-Not $thisServiceExists)
	{
		New-Service -Name $lyrasvc -BinaryPathName "C:\Users\Administrator\lyra\noded\lyra.noded.exe" -Credential "$env:COMPUTERNAME\Administrator" -Description "Lyra Node Daemon provides authorization service for Lyra network." -DisplayName "Lyra Node Daemon" -StartupType Automatic
	}

	[Environment]::SetEnvironmentVariable("LYRA_NETWORK", "testnet", "User")
	[Environment]::SetEnvironmentVariable("ASPNETCORE_URLS", "http://*:4505;https://*:4504", "User")
	[Environment]::SetEnvironmentVariable("ASPNETCORE_HTTPS_PORT", "4504", "User")

	Start-Service -Name $lyrasvc


[Detailed Guide from Microsoft](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/windows-service?view=aspnetcore-3.1&tabs=visual-studio#log-on-as-a-service-rights)

