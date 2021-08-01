# Pre-requisites

Ubuntu 20.04 LTS X86_64

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
# testnet ports: 4503 & 4505
LYRA_P2P_PORT=5503
LYRA_API_PORT=5505

# if you want to listen to extra port other than standart port. (seeds for example, use ";https://*:443")
LYRA_API_EXTRA_URL=
```

# Setup Docker

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

# setup docker containers
docker-compose --env-file .env up -d

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
cd ~/Lyra-Core
git pull
cd Lyra-Core/Docker
# !!! don't do this if you have other containers!
docker stop $(docker ps -a -q) && docker rm $(docker ps -a -q) && docker rmi $(docker images -a -q) && docker-compose down -v
docker-compose --env-file .env up -d

```

# Build your own docker image
```
~/Lyra-Core/Core/Lyra.Node2/Dockerfile
~/Lyra-Core/Client/Lyra.Client.CLI/Dockfile
```
