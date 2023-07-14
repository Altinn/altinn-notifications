const { Client } = require('pg');

const pgclient = new Client({
    host: process.env.POSTGRES_HOST,
    port: process.env.POSTGRES_PORT,
    user: 'platform_notifications_admin',
    password: 'Password',
    database: 'notificationsdb'
});

pgclient.connect();

const table = 'CREATE TABLE student(id SERIAL PRIMARY KEY, firstName VARCHAR(40) NOT NULL, lastName VARCHAR(40) NOT NULL, age INT, address VARCHAR(80), email VARCHAR(40))'

const users ='CREATE ROLE platform_notifications WITH   LOGIN  PASSWORD \'Password\';'
pgclient.query(users, (err, res) => {
    if (err) throw err
});

pgclient.query(table, (err, res) => {
    if (err) throw err
});
