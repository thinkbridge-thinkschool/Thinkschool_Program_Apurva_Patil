-- Day 9 Task 2 Setup: DeadlockLab Widgets + Orders tables

USE master;
GO

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'DeadlockLab')
    CREATE DATABASE DeadlockLab;
GO

USE DeadlockLab;
GO

DROP TABLE IF EXISTS Orders;    -- ← drop child first
DROP TABLE IF EXISTS Widgets;   -- ← then parent


CREATE TABLE Widgets(
    WidgetId INT PRIMARY KEY,
    Name     VARCHAR(50)   NOT NULL,
    Stock   INT
);

INSERT INTO Widgets VALUES
(1, 'Widget A',  100),
(2, 'Widget B',  250);


SELECT * FROM Widgets;




CREATE TABLE Orders(
    OrderId INT PRIMARY KEY,
    WidgetId INT,
    Quantity INT,
    FOREIGN KEY (WidgetId) REFERENCES Widgets(WidgetId)
);

INSERT INTO Orders VALUES
(1, 1, 10),
(2, 2, 20);

DBCC TRACEON(1222, -1);



