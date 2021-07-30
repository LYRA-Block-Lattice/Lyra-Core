# Prerequest

Ubuntu 20.04

# Setup Docker

```
# install prerequisities
apt-get update
apt-get install -y apt-transport-https ca-certificates curl gnupg lsb-release software-properties-common

# install docker
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /usr/share/keyrings/docker-archive-keyring.gpg
echo "deb [arch=amd64 signed-by=/usr/share/keyrings/docker-archive-keyring.gpg] https://download.docker.com/linux/ubuntu \
  $(lsb_release -cs) stable" | tee /etc/apt/sources.list.d/docker.list > /dev/null
apt-get update
apt-get install -y docker-ce docker-ce-cli containerd.io

# make docker runs as normal user
groupadd docker
usermod -aG docker $USER

echo "run \"newgrp docker\" to finish."
```

# Setup Lyra Node Daemon Container
```

# create a self-signed certificate
mkdir ~/.aspnet
mkdir ~/.aspnet/https
cd ~/.aspnet/https

# run openssl commands separately.
openssl req -x509 -days 3650 -newkey rsa:2048 -keyout cert.pem -out cert.pem
openssl pkcs12 -export -in cert.pem -inkey cert.pem -out cert.pfx

# get docker compose config
cd ~/
mkdir lyradb
git clone https://github.com/LYRA-Block-Lattice/Lyra-Core
cd Lyra-Core/Docker
# review .env.*-example and change it
# change the HTTPS_CERT_PASSWORD to yours.
cp .env.mainnet-example .env
vi .env

# setup docker containers
docker-compose --env-file .env up -d

# check if the daemon runs well
docker logs docker_noded_1	# or other names

```

