-- ============================================================
-- DEMO 2: Non-Repeatable Read
-- Isolation level that ALLOWS it : READ COMMITTED
-- Isolation level that PREVENTS it: REPEATABLE READ
-- ============================================================

-- ──────────────────────────────────────────
-- SESSION A  (run STEP 1)
-- ──────────────────────────────────────────

-- STEP 1 (Session A): First read of Bob's balance
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

BEGIN TRAN;
    SELECT AccountId, Owner, Balance
    FROM   BankAccounts
    WHERE  AccountId = 2;
    -- ➜ Balance = 2500.00  (first read)

    -- !! STOP — switch to Session B and run STEP 2 !!


-- ──────────────────────────────────────────
-- SESSION B  (run STEP 2 while Session A is open)
-- ──────────────────────────────────────────

-- STEP 2 (Session B): Commit a change to Bob's balance
BEGIN TRAN;
    UPDATE BankAccounts
    SET    Balance = Balance + 1000.00
    WHERE  AccountId = 2;
COMMIT;
-- Switch back to Session A


-- ──────────────────────────────────────────
-- SESSION A  (run STEP 3)
-- ──────────────────────────────────────────

-- STEP 3 (Session A): Second read of the SAME row inside the SAME transaction
    SELECT AccountId, Owner, Balance
    FROM   BankAccounts
    WHERE  AccountId = 2;
    -- ➜ Balance = 3500.00  (different from first read!)
    --   Same transaction, same query, different result. Non-repeatable read.

COMMIT;


-- ============================================================
-- PREVENTION: Repeat with REPEATABLE READ
-- ============================================================

-- Reset Bob
UPDATE BankAccounts SET Balance = 2500.00 WHERE AccountId = 2;

-- STEP 4 (Session A): First read under REPEATABLE READ
SET TRANSACTION ISOLATION LEVEL REPEATABLE READ;

BEGIN TRAN;
    SELECT AccountId, Owner, Balance
    FROM   BankAccounts
    WHERE  AccountId = 2;
    -- ➜ Balance = 2500.00 — and a shared lock is HELD on this row

    -- !! STOP — switch to Session B and run STEP 5 !!


-- STEP 5 (Session B): Try to update Bob — will BLOCK
BEGIN TRAN;
    UPDATE BankAccounts
    SET    Balance = Balance + 1000.00
    WHERE  AccountId = 2;
    -- ➜ Session B HANGS — Session A holds a shared lock, preventing the update.
COMMIT;


-- STEP 6 (Session A): Second read — still 2500.00, non-repeatable read PREVENTED
    SELECT AccountId, Owner, Balance
    FROM   BankAccounts
    WHERE  AccountId = 2;
    -- ➜ Balance = 2500.00 — same as first read ✅
COMMIT;
-- Session B unblocks after Session A commits
