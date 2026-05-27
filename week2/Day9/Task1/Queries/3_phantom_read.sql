-- ============================================================
-- DEMO 3: Phantom Read
-- Isolation level that ALLOWS it : REPEATABLE READ
-- Isolation level that PREVENTS it: SERIALIZABLE
-- ============================================================

-- ──────────────────────────────────────────
-- SESSION A  (run STEP 1)
-- ──────────────────────────────────────────

-- STEP 1 (Session A): Count accounts with balance > 1000 (first range read)
SET TRANSACTION ISOLATION LEVEL REPEATABLE READ;

BEGIN TRAN;
    SELECT COUNT(*) AS RichAccounts
    FROM   BankAccounts
    WHERE  Balance > 1000.00;
    -- ➜ Count = 3  (Bob 2500, Dave 3200, and Bob after reset)
    --   Existing rows are locked, but the RANGE is not.

    -- !! STOP — switch to Session B and run STEP 2 !!


-- ──────────────────────────────────────────
-- SESSION B  (run STEP 2 while Session A is open)
-- ──────────────────────────────────────────

-- STEP 2 (Session B): Insert a NEW account that falls inside Session A's range
BEGIN TRAN;
    INSERT INTO BankAccounts VALUES (6, 'Frank', 5000.00);
COMMIT;
-- Under REPEATABLE READ, this INSERT is NOT blocked — range is not locked.
-- Switch back to Session A


-- ──────────────────────────────────────────
-- SESSION A  (run STEP 3)
-- ──────────────────────────────────────────

-- STEP 3 (Session A): Same range query — different count!
    SELECT COUNT(*) AS RichAccounts
    FROM   BankAccounts
    WHERE  Balance > 1000.00;
    -- ➜ Count = 4  (Frank appeared — a phantom row!)
    --   Session A is confused: same transaction, same query, different set.

COMMIT;


-- ============================================================
-- PREVENTION: Repeat with SERIALIZABLE
-- ============================================================

-- Cleanup Frank
DELETE FROM BankAccounts WHERE AccountId = 6;

-- STEP 4 (Session A): First range read under SERIALIZABLE
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

BEGIN TRAN;
    SELECT COUNT(*) AS RichAccounts
    FROM   BankAccounts
    WHERE  Balance > 1000.00;
    -- ➜ Count = 3 — and a KEY RANGE lock is placed on the entire range

    -- !! STOP — switch to Session B and run STEP 5 !!


-- STEP 5 (Session B): Try to insert Frank again — will BLOCK
BEGIN TRAN;
    INSERT INTO BankAccounts VALUES (6, 'Frank', 5000.00);
    -- ➜ Session B HANGS — SERIALIZABLE holds a range lock, no new rows allowed.
COMMIT;


-- STEP 6 (Session A): Same range query — count is still 3, phantom PREVENTED ✅
    SELECT COUNT(*) AS RichAccounts
    FROM   BankAccounts
    WHERE  Balance > 1000.00;
    -- ➜ Count = 3 — consistent, no phantoms
COMMIT;
-- Session B unblocks and Frank is finally inserted
