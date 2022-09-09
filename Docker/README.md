<img src="lyradocker.png"/>

- [Pre-requisites](#pre-requisites)
- [dotenv file specification](#dotenv-file-specification)
- [Setup Docker](#setup-docker)
- [Setup Lyra Node Daemon Container](#setup-lyra-node-daemon-container)
- [Upgrade Lyra container](#upgrade-lyra-container)
- [Migrate from legacy Lyra node to Docker](#migrate-from-legacy-lyra-node-to-docker)
- [Build your own docker image](#build-your-own-docker-image)

# Pre-requisites

* Ubuntu 20.04 LTS X86_64
* Debian 10 X86_64

# dotenv file specification

```
#certificate used by Lyra API. it should be cert.pfx or so.
HTTPS_CERT_NAME=cert
HTTPS_CERT_PASSWORD=P@ssW0rd

MONGO_ROOT_NAME=root
MONGO_ROOT_PASSWORD=StrongP@ssW0rd

LYRA_DB_NAME=lyra
LYRA_DB_USER=dbuser
LYRA_DB_PASSWORD=alongpassword

# which network
LYRA_NETWORK=mainnet
# Normal for normal staking node, App for app mode.
LYRA_NODE_MODE=Normal

# the staking wallet. auto create if not exists ~/.lyra/mainnet/wallets
LYRA_POS_WALLET_NAME=poswallet
LYRA_POS_WALLET_PASSWORD=VeryStrongP@ssW0rd
# testnet ports: 4503 & 4504
LYRA_P2P_PORT=5503
LYRA_API_PORT=5504
# specify endpoint with or without host. like port, host:port. 
LYRA_API_ENDPOINT=5504

```

# Setup Docker

* Setup Docker and Docker Compose
* Ubuntu 20.04 X86_64 specifed. Other OS please follow Docker official documents https://docs.docker.com/engine/install/

```
# install prerequisities
sudo apt-get update
sudo apt-get -y install -y apt-transport-https ca-certificates curl gnupg lsb-release software-properties-common

# install docker
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /usr/share/keyrings/docker-archive-keyring.gpg
echo "deb [arch=amd64 signed-by=/usr/share/keyrings/docker-archive-keyring.gpg] https://download.docker.com/linux/ubuntu \
  $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
sudo apt-get update
sudo apt-get -y install -y docker-ce docker-ce-cli containerd.io docker-compose

# make docker runs as normal user
sudo usermod -aG docker $USER
newgrp docker
```

# Setup Lyra Node Daemon Container
```

# create a self-signed certificate
mkdir ~/.lyra
mkdir ~/.lyra/https

# store your own https certification in ~/.lyra/https. or generate self-signed certificate by openssl as bellow:
# run openssl commands separately.
cd ~/.lyra/https
openssl req -x509 -days 3650 -newkey rsa:2048 -keyout cert.pem -out cert.pem
openssl pkcs12 -export -in cert.pem -inkey cert.pem -out cert.pfx

# get docker compose config
cd ~/
mkdir ~/.lyra/db
git clone https://github.com/LYRA-Block-Lattice/Lyra-Core
cd Lyra-Core/Docker
# review .env.*-example and change it
# change the HTTPS_CERT_PASSWORD to yours .pfx file
cp .env.mainnet-example .env
vi .env

# create a profiting account for node
docker pull wizdy/lyra:latest
docker run -it --env LYRA_NETWORK=mainnet -v ~/.lyra:/root/.lyra wizdy/lyra:latest
# then use the "profinting" command in cli to create a new profint account
# share ratio: percentage you want to share with others. (100 - sharerito)% will be yours.
# seats: how many staking account you accept.

# setup docker containers
docker-compose --env-file .env up -d

# or setup docker with database restoring and save a lot time!
#docker-compose --env-file .env up --no-start
#docker start docker_mongo_1
#cat dbrestore-mainnet.sh | docker exec -i docker_mongo_1 bash
#docker start docker_noded_1

# check if the daemon runs well
docker ps
docker logs docker_noded_1	# or other names

# Done!
# Your staking wallet is located ~/.lyra/mainnet/wallets

# on rare condition you may need to reset docker and redo
# docker stop $(docker ps -a -q)
# docker rm $(docker ps -a -q)
# docker volume prune
# docker rmi $(docker images -a -q)
# docker system prune -a
# docker-compose down -v
# rm -rf ~/.lyra/db/*

```

# Upgrade Lyra container

```
cd ~/Lyra-Core/Docker
# !!! don't do this if you have other containers!
docker stop $(docker ps -a -q) && docker rm $(docker ps -a -q) && docker rmi $(docker images -a -q) && docker-compose down -v

cd ~/Lyra-Core
git pull
cd Docker
docker-compose --env-file .env up -d

```

# Hosting Dual Network

After normal setup above, you may want host both testnet and mainnet nodes in the same docker for Lyra network.
```
# create a self-signed certificate if not done already
mkdir ~/.lyra
mkdir ~/.lyra/https
cd ~/.lyra/https
openssl req -x509 -days 3650 -newkey rsa:2048 -keyout cert.pem -out cert.pem
openssl pkcs12 -export -in cert.pem -inkey cert.pem -out cert.pfx

# clone lyra project if not done already
cd ~/
mkdir ~/.lyra/db
git clone https://github.com/LYRA-Block-Lattice/Lyra-Core

# create docker containers for dualnet
cd Lyra-Core/Docker
cp .env.dualnet-example .env-dualnet
vi .env-dualnet
docker-compose --env-file .env-dualnet -f docker-compose-dualnet.yml up --no-start
docker start docker_mongo_1
cat dbrestore-dualnet.sh | docker exec -i docker_mongo_1 bash
docker start docker_testnet_1
docker start docker_mainnet_1

# done!

# upgrade dualnet laterly
cd ~/Lyra-Core/Docker
docker stop $(docker ps -a -q) && docker rm $(docker ps -a -q) && docker rmi $(docker images -a -q)
cd ~/Lyra-Core
git pull
cd Docker
docker-compose --env-file .env-dualnet -f docker-compose-dualnet.yml up -d
```

# Migrate from legacy Lyra node to Docker

* keep legacy Lyra node untouched, setup a complete new Docker node and let it do database sync.
* wait for the database sync done. (monitor by Nebula https://nebula.lyra.live/showbb)
* stop legacy Lyra node. 
* stop and destroy docker containers, buy leave the mongodb there
```
cd Lyra-Core/Docker
docker stop $(docker ps -a -q) && docker rm $(docker ps -a -q) && docker rmi $(docker images -a -q) && docker-compose down -v
```
* copy poswallet.lyrawallet from legacy node to docker node's new location: ~/.lyra/mainnet/wallets
* modify dotenv file, change the wallet's password, and recreate the containers
```
cd Lyra-Core/Docker
docker-compose --env-file .env up -d
```


# Build your own docker image
```
~/Lyra-Core/Core/Lyra.Node2/Dockerfile
~/Lyra-Core/Client/Lyra.Client.CLI/Dockfile
```

# Reference

[Create a profiting account for node](node-create-pftid.txt)