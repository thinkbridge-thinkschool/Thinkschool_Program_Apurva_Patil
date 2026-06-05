---DAT 9 Setup: BankAccounts table

DROP TABLE IF EXISTS BankAccounts;

CREATE TABLE BankAccounts(
    AccountId INT PRIMARY KEY,
    Owner VARCHAR(50) NOT NULL,
    Balance DECIMAL(10,2) Not NULL
);

INSERT INTO BankAccounts VALUES
(1, 'Alice',  1000.00),
(2, 'Bob',    2500.00),
(3, 'Carol',   800.00),
(4, 'Dave',   3200.00),
(5, 'Eve',     450.00);

SELECT * FROM BankAccounts;