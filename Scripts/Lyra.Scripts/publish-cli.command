#! /bin/bash
#
# This script creates ZIP files with CLI deployment binaries
#

rm -r "$HOME/Projects/Lyra/Releases/*.zip"

cd $HOME/Projects/Lyra/lyracliwallet
dotnet publish -c Release -r win-x64 --self-contained true
cd $HOME/Projects/Lyra/lyracliwallet/bin/Release/netcoreapp2.1/netcoreapp2.1/win-x64/publish
zip -r $HOME/Projects/Lyra/lyracliwallet/lyracli-windows-0.5.3.zip *
cp $HOME/Projects/Lyra/lyracliwallet/lyracli-windows-0.5.3.zip "$HOME/Projects/Lyra/Releases"

cd $HOME/Projects/Lyra/lyracliwallet
dotnet publish -c Release -r osx-x64 --self-contained true
cd $HOME/Projects/Lyra/lyracliwallet/bin/Release/netcoreapp2.1/netcoreapp2.1/osx-x64/publish
zip -r $HOME/Projects/Lyra/lyracliwallet/lyracli-mac-0.5.3.zip *
cp $HOME/Projects/Lyra/lyracliwallet/lyracli-mac-0.5.3.zip "$HOME/Projects/Lyra/Releases"

cd $HOME/Projects/Lyra/lyracliwallet
dotnet publish -c Release -r linux-x64 --self-contained true
cd $HOME/Projects/Lyra/lyracliwallet/bin/Release/netcoreapp2.1/netcoreapp2.1/linux-x64/publish
zip -r $HOME/Projects/Lyra/lyracliwallet/lyracli-linux-0.5.3.zip *
cp $HOME/Projects/Lyra/lyracliwallet/lyracli-linux-0.5.3.zip "$HOME/Projects/Lyra/Releases"

cd $HOME/Projects/Lyra/lyracliwallet
dotnet publish -c Release -r linux-arm --self-contained true
cd $HOME/Projects/Lyra/lyracliwallet/bin/Release/netcoreapp2.1/netcoreapp2.1/linux-arm/publish
zip -r $HOME/Projects/Lyra/lyracliwallet/lyracli-raspberry-0.5.3.zip *
cp $HOME/Projects/Lyra/lyracliwallet/lyracli-raspberry-0.5.3.zip "$HOME/Projects/Lyra/Releases"





