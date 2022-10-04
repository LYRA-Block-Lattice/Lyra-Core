#!/bin/sh

cd ~/Lyra-Core/Docker
# !!! don't do this if you have other containers!
docker stop $(docker ps -a -q) && docker rm $(docker ps -a -q) && docker rmi $(docker images -a -q) && docker-compose down -v

cd ~/Lyra-Core
#git switch master
git pull
cd Docker
docker-compose --env-file .env up -d


