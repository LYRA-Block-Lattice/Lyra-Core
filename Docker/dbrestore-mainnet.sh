#!/bin/bash

echo 'restore database from nebula daily backup...'
apt-get update \
  && apt-get install -y wget \
  && rm -rf /var/lib/apt/lists/*
wget https://nebula.lyra.live/apps/lyradb-mainnet-daily.tar.gz
tar -xzf lyradb-mainnet-daily.tar.gz
mongorestore -u $MONGO_INITDB_ROOT_USERNAME -p $MONGO_INITDB_ROOT_PASSWORD --drop daily
rm -rf daily
rm -f lyradb-mainnet-daily.tar.gz
echo 'done restore database.'