#! /bin/bash
#
# This script creates ZIP files with deployment binaries for node 
#
# Node
#
cd $HOME/Projects/Lyra/lyranode
dotnet publish -c Release -r win-x64 --self-contained false
cd $HOME/Projects/Lyra/lyranode/bin/Release/netcoreapp2.1/netcoreapp2.1/win-x64/publish
zip -r $HOME/Projects/Lyra/lyranode/bin/Release/netcoreapp2.1/netcoreapp2.1/win-x64/lyranode-Windows-v0.5.3.zip *
cp $HOME/Projects/Lyra/lyranode/bin/Release/netcoreapp2.1/netcoreapp2.1/win-x64/lyranode-Windows-v0.5.3.zip "$HOME/odrive/Google Drive Personal/LyraDeploy/lyranode"
#
# Node API
#
cd $HOME/Projects/Lyra/Lyra.Node.API
dotnet publish -c Release -r win-x64 --self-contained false
cd $HOME/Projects/Lyra/Lyra.Node.API/bin/Release/netcoreapp2.1/win-x64/publish
zip -r $HOME/Projects/Lyra/Lyra.Node.API/bin/Release/netcoreapp2.1/win-x64/Lyra.Node.API-Windows.zip *
cp $HOME/Projects/Lyra/Lyra.Node.API/bin/Release/netcoreapp2.1/win-x64/Lyra.Node.API-Windows.zip "$HOME/odrive/Google Drive Personal/LyraDeploy/Lyra.Node.API"
#
# LyraTrade
#
cd "$HOME/odrive/Google Drive Personal/LyraTradePublish"
zip -r "$HOME/odrive/Google Drive Personal/LyraTradePublish/lyratrade-Windows.zip" *
cp "$HOME/odrive/Google Drive Personal/LyraTradePublish/lyratrade-Windows.zip" "$HOME/odrive/Google Drive Personal/LyraDeploy/lyratrade"
