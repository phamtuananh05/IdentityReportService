# 🚂 Hướng dẫn Deploy lên Railway

## Cấu trúc deploy

Bạn sẽ tạo **1 Railway Project** với **3 services**:

```
Railway Project: LibrarySystem
├── identity-service   ← IdentityReportService
├── api-gateway        ← ApiGateway  
└── postgres           ← PostgreSQL (Railway add-on)
```

---

## Bước 1 – Push code lên GitHub

Đảm bảo toàn bộ code đã được push lên GitHub repository.

```bash
git add .
git commit -m "feat: migrate to PostgreSQL, add Railway deploy config"
git push
```

---

## Bước 2 – Tạo Railway Project & PostgreSQL

1. Vào [railway.app](https://railway.app) → **New Project**
2. Chọn **Deploy from GitHub repo** → chọn repo của bạn
3. Trong project, click **New** → **Database** → **Add PostgreSQL**
4. Sau khi PostgreSQL khởi động, click vào nó → tab **Variables** → copy giá trị `DATABASE_URL`

---

## Bước 3 – Deploy IdentityReportService

1. Click **New** → **GitHub Repo** → chọn repo → chọn **Root Directory**: `IdentityReportService`
2. Railway tự nhận Dockerfile → build tự động
3. Vào tab **Variables** của service này, thêm:

| Variable | Value |
|----------|-------|
| `ConnectionStrings__DefaultConnection` | *(Npgsql format – xem bên dưới)* |
| `Jwt__Key` | `LibrarySystemSecretKey2026_MustBeLongEnough` |
| `Jwt__Issuer` | `LibrarySystem` |
| `Jwt__Audience` | `LibrarySystemUsers` |
| `InternalService__ApiKey` | `LibraryInternalServiceKey2026` |
| `GoogleAuth__ClientId` | `453621976215-21nl1joh0275t522ntlkrs53q8a10uec.apps.googleusercontent.com` |

4. Sau khi deploy xong → tab **Settings** → copy **Public Domain** (ví dụ: `identity-service-xxx.railway.app`)

---

## Bước 4 – Deploy ApiGateway

1. Click **New** → **GitHub Repo** → chọn repo → chọn **Root Directory**: `ApiGateway`
2. Vào tab **Variables**, thêm:

| Variable | Value |
|----------|-------|
| `IDENTITY_SERVICE_HOST` | `identity-service-xxx.railway.app` |
| `CATALOG_SERVICE_HOST` | *(domain của CatalogService)* |
| `CIRCULATION_SERVICE_HOST` | *(domain của CirculationService)* |
| `GATEWAY_BASE_URL` | `https://api-gateway-xxx.railway.app` |
| `Jwt__Key` | `LibrarySystemSecretKey2026_MustBeLongEnough` |
| `Jwt__Issuer` | `LibrarySystem` |
| `Jwt__Audience` | `LibrarySystemUsers` |

---

## Bước 5 – Kiểm tra

| URL | Kỳ vọng |
|-----|---------|
| `https://identity-service-xxx.railway.app/swagger` | Swagger UI của IdentityReportService |
| `https://api-gateway-xxx.railway.app/api/identity/auth/login` | Login qua Gateway |

---

## Chuyển đổi DATABASE_URL sang Npgsql format

Railway cung cấp `DATABASE_URL`:
```
postgresql://myuser:mypassword@containers-us-west-1.railway.app:5432/railway
```

Chuyển thành `ConnectionStrings__DefaultConnection`:
```
Host=containers-us-west-1.railway.app;Port=5432;Database=railway;Username=myuser;Password=mypassword;SSL Mode=Require;Trust Server Certificate=true
```
