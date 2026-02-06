-- Run this SQL in Azure Portal Query Editor or Azure Data Studio
-- Connect to: pfd-server-sodhims.database.windows.net
-- Database: pfd-db

ALTER TABLE participants ADD Phone NVARCHAR(50) NULL;
