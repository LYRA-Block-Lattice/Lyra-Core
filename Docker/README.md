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
git clone https://github.com/LYRA-Block-Lattice/Lyra-Core
cd Lyra-Core/Docker
# review .env.*-example and change it
# change the HTTPS_CERT_PASSWORD to yours.
cp .env.mainnet-example .env
vi .env

# setup docker containers
docker-compose --env-file .env up -d



```