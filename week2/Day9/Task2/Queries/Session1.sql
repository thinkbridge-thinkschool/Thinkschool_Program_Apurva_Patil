-- ============================================================
-- SESSION 1 — Always locks Widgets FIRST, then Orders
-- Open this in Window 1 (left side)
-- ============================================================

USE DeadlockLab;

-- ============================================================
-- REPRODUCE: Run while Session2 REPRODUCE is also running
-- ============================================================

BEGIN TRAN;

    -- Step 1: Lock Widgets row 1 first
    UPDATE Widgets
    SET    Stock = Stock - 10
    WHERE  WidgetId = 1;
    -- ➜ Session1 now holds a lock on Widgets row 1

    WAITFOR DELAY '00:00:10';
    -- !! Switch to Session2 NOW and run its REPRODUCE section !!
    -- Session2 grabs Orders lock while we sleep here
    -- After 10 seconds: Session1 holds Widgets lock, wants Orders
    --                   Session2 holds Orders lock, wants Widgets → DEADLOCK

    -- Step 2: Try to lock Orders row 1
    UPDATE Orders
    SET    Quantity = Quantity + 5
    WHERE  OrderId = 1;
    -- ➜ One session gets Msg 1205 (deadlock victim), the other completes

COMMIT;
-- Screenshot: DeadlockRepro.png — Msg 1205 error on the victim session


-- ============================================================
-- DIAGNOSTICS: Run after the deadlock fires
-- ============================================================

-- 1. Read error log — trace flag 1222 writes the deadlock graph here
EXEC sp_readerrorlog 0, 1, 'deadlock';

-- 2. Query system_health XEvents ring buffer for the deadlock XML
SELECT
    xdr.value('@timestamp', 'datetime2')  AS DeadlockTime,
    xdr.query('.')                         AS DeadlockGraph
FROM (
    SELECT CAST(target_data AS XML) AS TargetData
    FROM   sys.dm_xe_session_targets  t
    JOIN   sys.dm_xe_sessions         s ON s.address = t.event_session_address
    WHERE  s.name = 'system_health'
      AND  t.target_name = 'ring_buffer'
) AS Data
CROSS APPLY TargetData.nodes('//RingBufferTarget/event[@name="xml_deadlock_report"]') AS XEventData(xdr);

-- 3. Check current blocking (run while deadlock is forming)
SELECT
    r.session_id,
    r.blocking_session_id,
    r.wait_type,
    r.wait_time,
    SUBSTRING(t.text, 1, 100) AS QueryText
FROM       sys.dm_exec_requests   r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
WHERE  r.blocking_session_id <> 0;


-- ============================================================
-- FIXED: Run after updating Session2 to use consistent lock order
-- Both sessions now lock Widgets first → no circular wait
-- ============================================================

BEGIN TRAN;

    UPDATE Widgets
    SET    Stock = Stock - 10
    WHERE  WidgetId = 1;

    WAITFOR DELAY '00:00:10';

    UPDATE Orders
    SET    Quantity = Quantity + 5
    WHERE  OrderId = 1;

COMMIT;
-- ➜ Both sessions complete cleanly, no Msg 1205 ✅
-- Screenshot: Fixed.png — both sessions show "1 row(s) affected"
