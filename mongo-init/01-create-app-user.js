// Runs once, on first start with an empty data directory (mongo image entrypoint).
// Creates the application user with readWrite on its own database only.
// The root user is created by MONGO_INITDB_ROOT_USERNAME / MONGO_INITDB_ROOT_PASSWORD.
const dbName = process.env.APP_DB_NAME;
db.getSiblingDB(dbName).createUser({
  user: process.env.APP_DB_USER,
  pwd: process.env.APP_DB_PASSWORD,
  roles: [{ role: 'readWrite', db: dbName }],
});
