
# SmartLabel-Vision

An industrial Automated Visual Inspection (AVI) and quality control station developed in **C# (.NET 10)**. The system inspects pharmaceutical and packaging labels along high-speed production lines, verifying physical label placement, decoding 1D/2D symbologies, extracting expiration date strings via optical character recognition (OCR), and persisting telemetry metrics to **Microsoft SQL Server**.

---

## Key Features

- **Geometric Alignment & Skew Verification**: Employs Canny edge detection, contour extraction, and minimum-area bounding boxes via `OpenCvSharp4` to detect label rotation defects within a $\pm 5^\circ$ tolerance.
- **Symbology Decoding**: Decodes standard 1D/2D barcodes (Code-128, QR Code) using `ZXing.Net` to enforce product serialization and tracking integrity.
- **OCR Expiry Extraction**: Extracts date and lot strings (`EXP: YYYY/MM`, `LOT: XXXXX`) through `Tesseract OCR` and validates formatting against production regex patterns.
- **Low-Latency Deterministic Pipeline**: Processes frames with sub-60ms average cycle times (~18–35 FPS throughput capacity), avoiding heavy GPU-bound inference overhead.
- **Relational Telemetry Logging**: Automatically persists inspection verdicts (`PASS`/`FAIL`), defect reason codes, ratio metrics, and frame latency directly to **SQL Server**.
- **Operator Dashboard**: Built with a responsive Windows Forms dashboard featuring dynamic status badges, annotated image previews, and live telemetry grid synchronization.

---

## System Architecture

```text
[ Input Image / Conveyor Frame ]
               │
               ▼
      [ InspectionEngine ]
               │
      ├── 1. Edge & Contour Detection (OpenCvSharp4)
      │      └── Geometric skew calculation & boundary sanity
      │
      ├── 2. Barcode/QR Decoding (ZXing.Net)
      │      └── Code-128 validation & barcode integrity check
      │
      └── 3. OCR Text Extraction (Tesseract OCR)
             └── Expiration date regex verification
               │
               ▼
      [ InspectionResult ] ──► Verdict: PASS / FAIL
               │
       ┌───────┴────────────────────────┐
       ▼                                ▼
[ WinForms Live UI ]         [ DatabaseLogger (ADO.NET) ]
- Image preview overlay       - InspectionLogs table
- Pass/Fail status banner     - Cycle time telemetry
- Real-time SQL grid view     - Failure reason classification

```

---

## Tech Stack

* **Runtime & Language**: C# (.NET 10.0 Windows Desktop SDK)
* **Computer Vision**: OpenCvSharp4 (`OpenCvSharp4`, `OpenCvSharp4.runtime.win`, `OpenCvSharp4.Extensions`)
* **Optical Character Recognition**: Tesseract 5.x (`Tesseract`) with `eng.traineddata`
* **Barcode Decoding**: ZXing.Net (`ZXing.Net`, `ZXing.Net.Bindings.Windows.Compatibility`)
* **Database & Storage**: Microsoft SQL Server Express, `Microsoft.Data.SqlClient`
* **UI Framework**: Windows Forms (WinForms)

---

## Database Schema

The station logs all inspection cycles to `SmartLabelDB`:

```sql
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
    Verdict NVARCHAR(10) NOT NULL,        -- 'PASS' or 'FAIL'
    FailureReason NVARCHAR(255) NULL,     -- e.g., 'LABEL_SKEWED (-12°)', 'BARCODE_UNREADABLE'
    CycleTimeMs FLOAT NOT NULL
);
GO

```

---

## Getting Started

### Prerequisites

* [Visual Studio 2026](https://visualstudio.microsoft.com/) (.NET Desktop Development workload installed)
* [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* [Microsoft SQL Server Express](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) and [SSMS](https://learn.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms)

### Installation & Run

1. **Clone the repository:**
```bash
git clone https://github.com/o0vinita0o/SmartLabel-Vision.git
cd SmartLabel-Vision

```


2. **Initialize the Database:**
* Open SSMS and connect to your local SQL Server instance (`localhost\SQLEXPRESS`).
* Run the script located in `schema.sql` to initialize `SmartLabelDB` and the `InspectionLogs` table.


3. **Verify OCR Model:**
* Ensure `tessdata/eng.traineddata` exists in the project root and its property is set to **Copy if newer**.


4. **Build and Run:**
* Open `SmartLabel-Vision.slnx` (or `.sln`) in Visual Studio 2022.
* Press `F5` to compile and launch.
* Click **▶ START BATCH INSPECTION** to trigger synthetic defect analysis and verify live database writes.



---

## Defect Classification Coverage

| Test Case | Simulated Condition | Detection Method | Verdict |
| --- | --- | --- | --- |
| `sample_01_PASS` | Normal production label | Contour bounds OK, Code-128 valid, EXP valid | **PASS** |
| `sample_02_SKEWED` | Label placed at $12^\circ$ tilt | `Cv2.MinAreaRect` skew $> \pm 5.0^\circ$ | **FAIL** (`LABEL_SKEWED`) |
| `sample_03_BAD_BARCODE` | Scratched / unreadable barcode | ZXing decode failure | **FAIL** (`BARCODE_UNREADABLE`) |
| `sample_04_MISSING_DATE` | Missing expiration date string | Tesseract OCR regex match failure | **FAIL** (`EXPIRY_DATE_MISSING`) |

---

## Performance Benchmark

* **Average Processing Latency**: ~28–55 ms per 600x450 frame (CPU-only, single thread)
* **Database Write Overhead**: < 5 ms via parameterized asynchronous ADO.NET queries
* **Target Line Speed**: Suitable for packaging lines running up to 1,000–1,200 parts per minute (PPM) under triggered acquisition.


