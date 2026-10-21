# Smart School Website API — Contact Form Backend

Minimal, production-conscious ASP.NET Core Web API for receiving, validating, and persisting school contact and demo form submissions from [Smart School Website](https://sms.rootaca.com/).

---

## 1. Overview & Architecture

- **Framework**: .NET 8 LTS (ASP.NET Core Web API)
- **Database**: PostgreSQL (Neon Cloud / Local PostgreSQL)
- **ORM**: Entity Framework Core 8 (`Npgsql.EntityFrameworkCore.PostgreSQL`)
- **Validation**: FluentValidation with structured RFC 7807/9110 `ValidationProblemDetails`
- **Security**:
  - Endpoint rate-limiting (Fixed window per IP)
  - Strict Request body size limit (32 KB)
  - CORS restricted to allowed origins (`https://sms.rootaca.com`, Firebase, Localhost)
  - Safe global exception handling (no sensitive data or stack traces leaked)
  - Privacy-compliant structured logging (submitted messages and personal data are NOT logged)
- **Testing**: 34 unit & integration tests (FluentValidation rules, API persistence, tamper-proofing, rate limiting)

---

## 2. Database Schema

Single dedicated table: `ContactSubmissions`

| Column | Type | Constraints | Description |
|---|---|---|---|
| `Id` | `UUID` | Primary Key, Not Null | Server-generated UUID |
| `Name` | `VARCHAR(150)` | Not Null | Prospect / Requester Name |
| `SchoolName` | `VARCHAR(200)` | Not Null | School Institution Name |
| `PhoneNumber` | `VARCHAR(30)` | Not Null | Contact Phone Number |
| `WorkEmail` | `VARCHAR(254)` | Not Null | Work Email Address |
| `Message` | `TEXT` | Nullable, Max 2000 chars | Optional Message / Specific Requirements |
| `CreatedAtUtc` | `TIMESTAMPTZ` | Not Null, Indexed | Server-generated timestamp (`DateTime.UtcNow`) |

Index: `IX_ContactSubmissions_CreatedAtUtc` on `CreatedAtUtc`.

---

## 3. Configuration

### Connection String Setup
The API automatically supports:
1. Standard ADO.NET connection strings:
   `Host=...;Port=5432;Database=smart_school_website;Username=...;Password=...;SslMode=Require`
2. Standard PostgreSQL URI format:
   `postgresql://username:password@host:port/database?sslmode=require`
3. Environment Variable `DATABASE_URL` (overrides `ConnectionStrings:DefaultConnection` if present)

### Sample `appsettings.json`
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "postgresql://neondb_owner:npg_T2Fen4oiDqOQ@ep-withered-cake-b4iocdkv-pooler.c-6.us-east-2.aws.neon.tech/neondb?sslmode=require&channel_binding=require"
  },
  "Cors": {
    "AllowedOrigins": [
      "https://sms.rootaca.com",
      "https://smart-school-website.web.app",
      "https://smart-school-website.firebaseapp.com",
      "http://localhost:5173",
      "http://localhost:3000"
    ]
  },
  "RateLimiting": {
    "PermitLimit": 5,
    "WindowSeconds": 60,
    "QueueLimit": 0
  },
  "Swagger": {
    "Enabled": true
  }
}
```

A template file with placeholders is provided in [`appsettings.example.json`](./SmartSchool.Website.Api/appsettings.example.json).

---

## 4. How to Run & Apply Migrations

### Prerequisites
- .NET 8 SDK or .NET 9 SDK installed.

### Restore & Build
```bash
dotnet restore
dotnet build
```

### Apply Database Migrations
To apply the initial migration (`InitialCreate`) to the configured PostgreSQL database:
```bash
dotnet dotnet-ef database update --project SmartSchool.Website.Api --startup-project SmartSchool.Website.Api
```

### Run the API
```bash
dotnet run --project SmartSchool.Website.Api
```
The API starts listening (default ports `http://localhost:5000` / `https://localhost:5001` or configured launch URLs).

### Access Swagger UI
When running in Development or with `Swagger:Enabled=true`:
- Swagger UI: `http://localhost:5080/swagger` (or port used)
- OpenAPI Spec: `http://localhost:5080/swagger/v1/swagger.json`

---

## 5. API Endpoint Documentation

### Public Submission Endpoint
`POST /api/v1/contact-submissions`

#### Headers
- `Content-Type: application/json`
- `Accept: application/json`

#### Example Request Body
```json
{
  "name": "Ahmed Mohamed",
  "schoolName": "Example International School",
  "phoneNumber": "+966500000000",
  "workEmail": "contact@example.com",
  "message": "We would like to learn more about the platform."
}
```

*(Note: The API accepts both `name` and `fullName` to seamlessly support both frontend conventions).*

#### Success Response (`201 Created`)
```json
{
  "id": "15151fb4-348b-412a-8b63-2a5d8a4f2768",
  "message": "Your request has been submitted successfully."
}
```

#### Validation Error Response (`400 Bad Request`)
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "WorkEmail": ["Please enter a valid work email."],
    "PhoneNumber": ["Please enter a valid phone number."]
  }
}
```

#### Rate Limit Exceeded Response (`429 Too Many Requests`)
```json
{
  "status": 429,
  "title": "Too Many Requests",
  "detail": "Rate limit exceeded. Please wait before submitting another request."
}
```

---

## 6. Frontend Integration

In the frontend application (`smart-school-website`):
1. The [`demoService.ts`](../smart-school-website/src/services/demoService.ts) automatically sends requests to the backend:
   - In Development: Uses `http://localhost:5080` (or Vite proxy `/api`).
   - In Production: Configured via environment variable `VITE_API_URL`.
2. To configure a production backend URL in Vite:
   Create or update `.env.production`:
   ```env
   VITE_API_URL=https://your-api-domain.com
   ```

---

## 7. Testing & Verification Results

Run all test suites:
```bash
dotnet test
```

### Verified Test Results:
- **Total Tests**: 34
- **Passed**: 34 (100%)
- **Failed**: 0
- **Coverage**:
  - `Name` required, length validation (< 150 chars).
  - `SchoolName` required, length validation (< 200 chars).
  - `PhoneNumber` required, format & length validation (< 30 chars).
  - `WorkEmail` required, RFC syntax validation, length (< 254 chars).
  - `Message` optional, length validation (< 2000 chars).
  - Server generation of `Id` and `CreatedAtUtc` (tamper-proofing).
  - Entity Framework Core PostgreSQL persistence.
  - Rate limiting (429 Too Many Requests on threshold breach).
