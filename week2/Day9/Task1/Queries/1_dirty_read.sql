-- ============================================================
-- DEMO 1: Dirty Read
-- Isolation level that ALLOWS it : READ UNCOMMITTED
-- Isolation level that PREVENTS it: READ COMMITTED (default)
-- ============================================================

-- ──────────────────────────────────────────
-- SESSION B  (run STEP 1 first)
-- ──────────────────────────────────────────

-- STEP 1 (Session B): Start a transaction, update Alice's balance
--         but DO NOT commit yet
BEGIN TRAN;
    UPDATE BankAccounts
    SET    Balance = Balance - 900.00  
    WHERE  AccountId = 1;

    -- !! STOP HERE — do NOT run COMMIT or ROLLBACK yet !!
    -- Switch to Session A and run STEP 2


-- ──────────────────────────────────────────
-- SESSION A  (run STEP 2 while B is open)
-- ──────────────────────────────────────────

-- STEP 2 (Session A): Read with READ UNCOMMITTED — sees Session B's dirty data
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

BEGIN TRAN;
    SELECT AccountId, Owner, Balance
    FROM   BankAccounts
    WHERE  AccountId = 1;
    -- ➜ You will see Balance = 100.00  (the UNCOMMITTED value!)
    --   That data doesn't truly exist yet — Session B might roll back.
COMMIT;


-- ──────────────────────────────────────────
-- SESSION B  (run STEP 3 after Session A reads)
-- ──────────────────────────────────────────

-- STEP 3 (Session B): Roll back — the change never happened
ROLLBACK;

SELECT * FROM BankAccounts WHERE AccountId = 1;
-- ➜ Balance is back to 1000.00
-- Session A read 100.00 — a value that NEVER existed. That is a dirty read.


-- ============================================================
-- PREVENTION: Repeat with READ COMMITTED
-- ============================================================

-- STEP 4 (Session B): Start again, same update, no commit
BEGIN TRAN;
    UPDATE BankAccounts
    SET    Balance = Balance - 900.00
    WHERE  AccountId = 1;
    -- !! STOP — switch to Session A !!


-- STEP 5 (Session A): Read with READ COMMITTED — Session A BLOCKS
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

BEGIN TRAN;
    SELECT AccountId, Owner, Balance
    FROM   BankAccounts
    WHERE  AccountId = 1;
    -- ➜ Session A HANGS here waiting for Session B to commit or rollback.
    --   Dirty read is PREVENTED.
COMMIT;


-- STEP 6 (Session B): Commit — Session A now unblocks and reads committed data
COMMIT;
-- Session A will now return Balance = 100.00 (the COMMITTED update)
