# ReportingHub API

A lightweight **.NET 8 Minimal API** that powers internal data processing tasks for the Omni Studio application.  
Currently supports secure **Excel workbook merging** with customizable sheet naming. Future modules will include reporting and export endpoints.

---

## ✨  Features

- **Excel Merge**  
  Upload multiple `.xlsx` files and receive a single workbook with all sheets combined.
- **Custom sheet naming**  
  Supply JSON rules (`names` field) to control how worksheets are named in the merged file.  
  Supports:
  - Keep original names (`{sheet}`)
  - Prefix/suffix with file name (`{file}_{sheet}`)
  - Explicit mapping per file/sheet
  - Collision handling: `dedupe` (auto suffix) or `error`
- **In-memory processing**  
  No temporary files or disk writes; runs entirely in memory.
- **Internal-only**  
  Hosted on IIS inside the government intranet. Not exposed externally.
- **Correlation IDs**  
  Every request/response includes `X-Correlation-Id` for audit and traceability.
- **Swagger UI**  
  Test endpoints interactively, including file uploads.

---

## 🛠️ Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- IIS with ASP.NET Core Hosting Bundle (for on-prem deployment)
- Visual Studio / VS Code (optional for dev)

### Local Run
```bash
dotnet run --project ReportingHub.Api
```

Browse Swagger UI at [https://localhost:5001/swagger](https://localhost:5001/swagger).

---

## 🚀 Usage

### Endpoint

```bash
POST /excel/merge
```

### Parameters

- **files** — one or more `.xlsx` files (required)  
- **names** — *(optional string)* JSON rules for sheet naming  

Content type: `multipart/form-data`

### Example `names` JSON

**Keep original sheet names:**
```json
{
  "mode": "pattern",
  "pattern": "{sheet}",
  "collision": "dedupe"
}
```

**Map file/sheet names explicitly:**
```json
{
  "mode": "map",
  "collision": "dedupe",
  "map": {
    "A.xlsx": { "Sheet1": "GrantsFY25" },
    "B.xlsx": { "*": "{file}-{sheet}" }
  }
}
```

---

## 📥 Response

- **200 OK** → returns the merged `.xlsx` file  
- **400 Bad Request** → returns structured error JSON with correlation ID  

**Example error response:**
```json
{
  "error": {
    "code": "InvalidFileType",
    "message": "Only .xlsx is allowed.",
    "correlationId": "b8f3c0e4a1d2"
  }
}
```

---

## 🔒 Security

- Hosted **on-premises** in IIS, not internet-exposed (TODO)
- Protected by **Windows Authentication** or **API Key**, depending on environment (TODO)
- Every request/response includes an `X-Correlation-Id` header for traceability  

---

## 📂 Project Structure

```text
ReportingHub/
  ReportingHub.Api/
    Endpoints/        # Minimal API endpoints
    Contracts/        # Request/response DTOs
    Infrastructure/   # Middleware, helpers (ApiResults, Swagger config)
    Program.cs        # Startup
  README.md         # This document
```

---

## 🗺️ Roadmap

- [ ] Reporting endpoints (generate/download standard reports)  
- [ ] Authentication & authorization integration with Omni Studio  
- [ ] Excel templating with charts/pivot tables  
- [ ] Enhanced logging & metrics  

---

## 🛠 Development Notes

- Keep endpoints **stateless** and in-memory  
- Enforce **file size** and **worksheet limits** (configurable in `Program.cs`)  
- Use **ClosedXML** for Excel processing (Interop-free)  
- Add tests for naming patterns and edge cases (duplicate names, invalid chars, etc.)  
