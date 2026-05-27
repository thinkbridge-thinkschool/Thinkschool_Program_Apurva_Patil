# Day 9 — Isolation Levels + Read Anomalies

## Table
`BankAccounts` — 5 rows (AccountId, Owner, Balance)

---

## The Three Read Anomalies

### 1. Dirty Read
Session A reads data that Session B has modified but not yet committed.
If Session B rolls back, Session A has read a value that never truly existed.

**Reproduced at:** `READ UNCOMMITTED`  
**Prevented from:** `READ COMMITTED` and above

### 2. Non-Repeatable Read
Session A reads the same row twice inside one transaction.
Session B commits an UPDATE in between → Session A sees two different values.

**Reproduced at:** `READ COMMITTED`  
**Prevented from:** `REPEATABLE READ` and above (shared lock held on read rows)

### 3. Phantom Read
Session A runs the same range query twice inside one transaction.
Session B commits an INSERT that matches the range → Session A sees a new "phantom" row.

**Reproduced at:** `REPEATABLE READ`  
**Prevented from:** `SERIALIZABLE` only (key-range lock held on the scanned range)

---

## Prevention Matrix

| Isolation Level     | Dirty Read | Non-Repeatable Read     | Phantom Read |
|---------------------|------------|------------------------|---------------|
| READ UNCOMMITTED    | ✗          | ✗                     | ✗            |
| READ COMMITTED      | ✅         | ✗                     | ✗            |
| REPEATABLE READ     | ✅         | ✅                    | ✗            |
| SERIALIZABLE        | ✅         | ✅                    | ✅           |

---

## Key Takeaways

- Each higher isolation level adds more locking — preventing more anomalies but increasing blocking.
- `READ COMMITTED` is SQL Server's **default** — a good balance for most OLTP workloads.
- `REPEATABLE READ` holds shared locks until transaction end — prevents row-level changes but not new rows.
- `SERIALIZABLE` adds **key-range locks** — the only level that blocks phantom inserts/deletes.
- Higher isolation = fewer anomalies but **more contention and potential deadlocks**.
