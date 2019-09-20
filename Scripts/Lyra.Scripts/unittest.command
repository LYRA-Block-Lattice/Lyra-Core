#! /bin/bash
#
# This script runs Lyra node and node webapi in unit test mode (unittest network) for wallet unit testing
#
# Remove old database
rm -r "$HOME/Lyra-Node-unittest"
#
# Node
#
cd $HOME/Projects/LyraNetwork/Node/Lyra.Node/bin/Debug/netcoreapp2.1/netcoreapp2.1
dotnet lyranode.dll --networkid unittest --seed self --database litedb
