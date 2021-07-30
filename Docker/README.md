

git clone https://github.com/LYRA-Block-Lattice/Lyra-Core
cd Lyra-Core/Docker
# review .env.*-example and change it
# for mainnet
docker-compose --env-file .env.mainnet-example up -d
# for testnet
# docker-compose --env-file .env.testnet-example up -d


