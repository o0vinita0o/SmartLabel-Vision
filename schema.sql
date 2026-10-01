CREATE DATABASE SmartLabelDB;
GO

USE SmartLabelDB;
GO

CREATE TABLE InspectionLogs (
    LogId INT IDENTITY(1,1) PRIMARY KEY,
    Timestamp DATETIME DEFAULT GETDATE(),
    BarcodeData NVARCHAR(100) NULL,
    ExpiryDateText NVARCHAR(50) NULL,
    ContourAreaRatio FLOAT NOT NULL,
    Verdict NVARCHAR(10) NOT NULL,    -- 'PASS' or 'FAIL'
    FailureReason NVARCHAR(255) NULL, -- e.g., 'BARCODE_UNREADABLE', 'LABEL_SKEWED'
    CycleTimeMs FLOAT NOT NULL
);
GO
