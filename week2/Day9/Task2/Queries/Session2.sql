-- ============================================================
-- SESSION 2 — Concurrent window
-- Open this in Window 2 (right side)
-- ============================================================

USE DeadlockLab;

-- ============================================================
-- REPRODUCE (Broken): Locks Orders FIRST, then Widgets
-- Run while Session1 REPRODUCE is paused on WAITFOR
-- ============================================================

BEGIN TRAN;

    -- Step 1: Lock Orders row 1 first  ← OPPOSITE order to Session1
    UPDATE Orders
    SET    Quantity = Quantity + 5
    WHERE  OrderId = 1;
    -- ➜ Session2 now holds a lock on Orders row 1

    WAITFOR DELAY '00:00:10';
    -- Session1 holds Widgets lock and is now trying to get Orders lock
    -- Session2 holds Orders lock and is now trying to get Widgets lock
    -- → Circular wait → SQL Server detects deadlock → kills one victim

    -- Step 2: Try to lock Widgets row 1
    UPDATE Widgets
    SET    Stock = Stock - 10
    WHERE  WidgetId = 1;
    -- ➜ This session may receive: Msg 1205 - Transaction was deadlocked

COMMIT;
-- Screenshot: DeadlockRepro.png — Msg 1205 on whichever session SQL Server chose as victim


-- ============================================================
-- Reset data before running FIXED section
-- ============================================================
UPDATE Widgets SET Stock    = 100 WHERE WidgetId = 1;
UPDATE Orders  SET Quantity =  10 WHERE OrderId  = 1;


-- ============================================================
-- FIXED: Locks Widgets FIRST, then Orders — same order as Session1
-- Run while Session1 FIXED is paused on WAITFOR
-- ============================================================

BEGIN TRAN;

    -- Step 1: Lock Widgets first  ← NOW MATCHES Session1 order
    UPDATE Widgets
    SET    Stock = Stock - 10
    WHERE  WidgetId = 1;

    WAITFOR DELAY '00:00:10';

    -- Step 2: Lock Orders second
    UPDATE Orders
    SET    Quantity = Quantity + 5
    WHERE  OrderId = 1;
    -- ➜ No circular wait — both sessions acquire locks in the same sequence

COMMIT;
-- ➜ Session2 completes cleanly ✅  No Msg 1205
-- Screenshot: Fixed.png — both sessions show "1 row(s) affected"
