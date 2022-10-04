#!/bin/sh

cd ~/Lyra-Core/Docker
# !!! don't do this if you have other containers!
docker stop $(docker ps -a -q) && docker rm $(docker ps -a -q) && docker rmi $(docker images -a -q)
cd ~/Lyra-Core
#git switch testnet
git pull
cd Docker
docker-compose --env-file .env-dualnet -f docker-compose-dualnet.yml up -d

