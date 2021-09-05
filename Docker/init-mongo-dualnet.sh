#!/usr/bin/env bash
set -eu
mongo -- "$MONGO_DB" <<EOF
    var rootUser = '$MONGO_INITDB_ROOT_USERNAME';
    var rootPassword = '$MONGO_INITDB_ROOT_PASSWORD';
    var admin = db.getSiblingDB('admin');
    admin.auth(rootUser, rootPassword);

    var user = '$MONGO_USER';
    var passwd = '${MONGO_PASSWORD}';
    db.createUser({user: user, pwd: passwd, roles: ["readWrite"]});
EOF

echo 'restore database from nebula daily backup...'
apt-get update \
  && apt-get install -y wget \
  && rm -rf /var/lib/apt/lists/*
wget https://nebula.lyra.live/apps/lyradb-dualnet-daily.tar.gz
tar -xzf lyradb-dualnet-daily.tar.gz
mongorestore -u $MONGO_INITDB_ROOT_USERNAME -p $MONGO_INITDB_ROOT_PASSWORD --drop daily
# rm -rf daily
# rm -f lyradb-dualnet-daily.tar.gz
echo 'done restore database.'


