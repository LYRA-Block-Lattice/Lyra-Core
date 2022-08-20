#!/bin/bash

echo 'restore database from nebula daily backup...'
apt-get update \
  && apt-get install -y wget \
  && rm -rf /var/lib/apt/lists/*
wget https://download.lyra.live/database/lyradb-testnet-daily.tar.gz
tar -xzf lyradb-testnet-daily.tar.gz
mongorestore -u $MONGO_INITDB_ROOT_USERNAME -p $MONGO_INITDB_ROOT_PASSWORD --drop daily
rm -rf daily
rm -f lyradb-testnet-daily.tar.gz
echo 'done restore database.'