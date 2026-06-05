-- ============================================================
-- SESSION 2 — Concurrent window
-- Open this in Window 2 (right side)
-- Run each SECTION while Session1 is paused (during WAITFOR)
-- ============================================================

USE IsolationDemo;

-- ============================================================
-- SECTION A: Dirty Read — REPRODUCE
-- Run while Session1 Section A is paused
-- ============================================================
IF @@TRANCOUNT > 0 ROLLBACK;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
BEGIN TRAN;
    SELECT AccountId, Owner, Balance
    FROM   BankAccounts
    WHERE  AccountId = 1;
    -- ➜ Balance = 9999.00  ← DIRTY! Session1 hasn't committed yet
    --   This is a dirty read — reading uncommitted data
COMMIT;

-- ============================================================
-- SECTION B: Dirty Read — PREVENT
-- Run while Session1 Section B is paused
-- ============================================================
IF @@TRANCOUNT > 0 ROLLBACK;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
BEGIN TRAN;
    SELECT AccountId, Owner, Balance
    FROM   BankAccounts
    WHERE  AccountId = 1;
    -- ➜ Query HANGS / spins — blocked waiting for Session1 to commit
    --   Dirty read is PREVENTED. Returns 9999 only after Session1 commits.
COMMIT;

-- ============================================================
-- SECTION C: Non-Repeatable Read — REPRODUCE
-- Run while Session1 Section C is paused
-- ============================================================
IF @@TRANCOUNT > 0 ROLLBACK;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
UPDATE BankAccounts
SET    Balance = 3500.00
WHERE  AccountId = 2;
-- Single auto-commit UPDATE — no BEGIN TRAN needed, commits instantly
-- Slips in between Session1's two reads
-- Session1 will see 2500 on first read, then 3500 on second read

-- ============================================================
-- SECTION D: Non-Repeatable Read — PREVENT
-- Run while Session1 Section D is paused
-- ============================================================
IF @@TRANCOUNT > 0 ROLLBACK;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
UPDATE BankAccounts
SET    Balance = 3500.00
WHERE  AccountId = 2;
-- ➜ Query HANGS / spins — Session1 holds a shared lock under REPEATABLE READ
--   Cannot modify a locked row. Non-repeatable read is PREVENTED.

-- ============================================================
-- SECTION E: Phantom Read — REPRODUCE
-- Run while Session1 Section E is paused
-- ============================================================
IF @@TRANCOUNT > 0 ROLLBACK;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
INSERT INTO BankAccounts VALUES (6, 'Frank', 5000.00);
-- Single auto-commit INSERT — goes through immediately under REPEATABLE READ
-- (only existing rows are locked, not the range)
-- Session1 will see count=2 on first read, then count=3 on second read

-- ============================================================
-- SECTION F: Phantom Read — PREVENT
-- Run while Session1 Section F is paused
-- ============================================================
IF @@TRANCOUNT > 0 ROLLBACK;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
INSERT INTO BankAccounts VALUES (6, 'Frank', 5000.00);
-- ➜ Query HANGS / spins — SERIALIZABLE holds a key-range lock
--   No new rows can be inserted into the locked range.
--   Phantom read is PREVENTED.
