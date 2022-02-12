#!/usr/bin/env bash
set -eu
mongo -- "$MONGO_DB" <<EOF
    var rootUser = '$MONGO_INITDB_ROOT_USERNAME';
    var rootPassword = '$MONGO_INITDB_ROOT_PASSWORD';
    var db1 = '$MONGO_DB';
    var db2 = 'Worflow_testnet';
    var db3 = 'Worflow_mainnet';
    var admin = db.getSiblingDB('admin');
    admin.auth(rootUser, rootPassword);

    var user = '$MONGO_USER';
    var passwd = '${MONGO_PASSWORD}';
    use db1
    db.createUser({user: user, pwd: passwd, roles: ["readWrite"]});
    use db2
    db.createUser({user: user, pwd: passwd, roles: ["readWrite"]});
    use db3
    db.createUser({user: user, pwd: passwd, roles: ["readWrite"]});
EOF