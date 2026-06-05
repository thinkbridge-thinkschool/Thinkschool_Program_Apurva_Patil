-- ============================================================
-- SESSION 1 — Long-running transaction window
-- Open this in Window 1 (left side)
-- Run each SECTION separately by selecting its lines → F5
-- ============================================================

USE IsolationDemo;

-- ============================================================
-- SECTION A: Dirty Read — REPRODUCE
-- Run this block. While it pauses (30 sec), switch to Session2 → run Section A
-- ============================================================
IF @@TRANCOUNT > 0 ROLLBACK;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
BEGIN TRAN;
    UPDATE BankAccounts SET Balance = 9999.00 WHERE AccountId = 1;
    WAITFOR DELAY '00:00:30';   -- switch to Session2 NOW — you have 30 seconds
ROLLBACK;
-- After rollback: Alice is back to 1000.00
-- Screenshot: Session2 result showing 9999 (dirty uncommitted value)

-- ============================================================
-- SECTION B: Dirty Read — PREVENT
-- Run this block. While it pauses (30 sec), switch to Session2 → run Section B
-- ============================================================
IF @@TRANCOUNT > 0 ROLLBACK;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
BEGIN TRAN;
    UPDATE BankAccounts SET Balance = 9999.00 WHERE AccountId = 1;
    WAITFOR DELAY '00:00:30';   -- switch to Session2 NOW — you have 30 seconds
COMMIT;
-- Screenshot: Session2 spinning/blocked (cannot read uncommitted data)

-- Reset Alice after Section B
UPDATE BankAccounts SET Balance = 1000.00 WHERE AccountId = 1;

-- ============================================================
-- SECTION C: Non-Repeatable Read — REPRODUCE
-- Run this block. While it pauses, switch to Session2 → run Section C
-- ============================================================
IF @@TRANCOUNT > 0 ROLLBACK;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
BEGIN TRAN;
    SELECT AccountId, Owner, Balance FROM BankAccounts WHERE AccountId = 2;
    -- First read → 2500.00
    WAITFOR DELAY '00:00:30';   -- switch to Session2 NOW — you have 30 seconds
    SELECT AccountId, Owner, Balance FROM BankAccounts WHERE AccountId = 2;
    -- Second read → 3500.00  (Session2 changed it in between!)
COMMIT;
-- Screenshot: both result sets visible — first=2500, second=3500

-- Reset Bob after Section C
UPDATE BankAccounts SET Balance = 2500.00 WHERE AccountId = 2;

-- ============================================================
-- SECTION D: Non-Repeatable Read — PREVENT
-- Run this block. While it pauses, switch to Session2 → run Section D
-- ============================================================
IF @@TRANCOUNT > 0 ROLLBACK;
SET TRANSACTION ISOLATION LEVEL REPEATABLE READ;
BEGIN TRAN;
    SELECT AccountId, Owner, Balance FROM BankAccounts WHERE AccountId = 2;
    -- First read → 2500.00  (shared lock HELD on this row)
    WAITFOR DELAY '00:00:30';   -- switch to Session2 NOW — its UPDATE will BLOCK
    SELECT AccountId, Owner, Balance FROM BankAccounts WHERE AccountId = 2;
    -- Second read → still 2500.00  ✅ non-repeatable read PREVENTED
COMMIT;
-- Screenshot: Session2 spinning/blocked while Session1 shows first read = 2500

-- Reset Bob after Section D
UPDATE BankAccounts SET Balance = 2500.00 WHERE AccountId = 2;

-- ============================================================
-- SECTION E: Phantom Read — REPRODUCE
-- Run this block. While it pauses, switch to Session2 → run Section E
-- ============================================================
IF @@TRANCOUNT > 0 ROLLBACK;
SET TRANSACTION ISOLATION LEVEL REPEATABLE READ;
BEGIN TRAN;
    SELECT COUNT(*) AS RichAccounts FROM BankAccounts WHERE Balance > 1000.00;
    -- First count → 2  (Bob=2500, Dave=3200)
    WAITFOR DELAY '00:00:30';   -- switch to Session2 NOW — Frank will sneak in
    SELECT COUNT(*) AS RichAccounts FROM BankAccounts WHERE Balance > 1000.00;
    -- Second count → 3  (Frank=5000 appeared — PHANTOM!)
COMMIT;
-- Screenshot: both result sets visible — first=2, second=3

-- Cleanup Frank after Section E
DELETE FROM BankAccounts WHERE AccountId = 6;

-- ============================================================
-- SECTION F: Phantom Read — PREVENT
-- Run this block. While it pauses, switch to Session2 → run Section F
-- ============================================================
IF @@TRANCOUNT > 0 ROLLBACK;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRAN;
    SELECT COUNT(*) AS RichAccounts FROM BankAccounts WHERE Balance > 1000.00;
    -- First count → 2  (key-range lock placed on the entire range)
    WAITFOR DELAY '00:00:30';   -- switch to Session2 NOW — its INSERT will BLOCK
    SELECT COUNT(*) AS RichAccounts FROM BankAccounts WHERE Balance > 1000.00;
    -- Second count → still 2  ✅ phantom read PREVENTED
COMMIT;
-- Screenshot: Session2 spinning/blocked while Session1 shows first count = 2
-- After Session1 commits: Frank is finally inserted by Session2
