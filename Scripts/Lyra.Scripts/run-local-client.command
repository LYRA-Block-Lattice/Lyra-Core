#! /bin/bash
#
# This script runs local client on "local" network
#

cd $HOME/Projects/LyraNetwork/Client/Lyra.Client.CLI/bin/Debug/netcoreapp2.1/netcoreapp2.1
dotnet lyracli.dll --database litedb --networkid local --protocol rpc

