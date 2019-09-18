#! /bin/bash
#
# This script runs local Lyra node ("local" network)
#
cd $HOME/Projects/LyraNetwork/Node/Lyra.Node/bin/Debug/netcoreapp2.1/netcoreapp2.1
dotnet lyranode.dll --networkid local --seed self --database litedb

