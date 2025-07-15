import { Pool } from "pg";

export const pool = new Pool({
  connectionString: process.env.SQL_CONNECTION_STRING,
});
