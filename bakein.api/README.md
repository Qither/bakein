# Bakein API

Postgres-backed ASP.NET API for the current Bakein Taro frontend.

## Local Docker

```powershell
cd H:\CustomProject\bakein\bakein.api
copy .env.example .env
docker compose up --build
```

The API listens on `http://localhost:5164` by default. Startup initializes the schema and seeds the frontend-aligned demo catalog.

Demo account:

- Email: `demo@bakein.local`
- Password: `bakein123`

Demo admin account:

- Email: `admin@bakein.local`
- Password: `bakein123`

`DATABASE_SEED_DEMO_DATA=true` is intended for local Docker/dev only. Production-style deployments should leave demo seeding disabled and rely on migrations plus explicit content/admin setup.

## Core Endpoints

- `GET /health`
- `POST /api/auth/register`
- `POST /api/auth/login`
- `GET /api/auth/me`
- `POST /api/auth/logout`
- `GET /api/catalog/home`
- `GET /api/catalog/categories`
- `GET /api/courses?category=面包&memberFree=false&search=吐司`
- `GET /api/courses/{id}`
- `GET /api/courses/{id}/steps`
- `GET /api/membership/plans`
- `GET /api/community/posts`
- `POST /api/community/posts`
- `GET /api/users/me/profile`
- `GET /api/users/me/cart`
- `PUT /api/users/me/cart/items`
- `PATCH /api/users/me/cart/items/{id}`
- `DELETE /api/users/me/cart/items/{id}`
- `POST /api/users/me/cart/checkout`
- `GET /api/users/me/orders`
- `GET /api/users/me/progress?courseId=soft-bread`
- `PUT /api/users/me/progress`
- `POST /api/media/upload-intents`
- `POST /api/media/callbacks/local`
- `POST /api/payments/intents`
- `POST /api/payments/callbacks/local`
- `POST /api/community/check-ins`
- `POST /api/community/posts/{id}/comments`
- `POST /api/community/posts/{id}/likes`
- `POST /api/community/posts/{id}/reports`
- `GET /api/users/me/addresses`
- `POST /api/users/me/addresses`
- `POST /api/admin/courses/versions`
- `POST /api/admin/courses/versions/{id}/publish`
- `GET /api/admin/moderation/tasks`
- `GET /api/admin/audit-logs`
- `GET /api/operations/readiness`
- `GET /api/operations/provider-diagnostics`

Authenticated endpoints use `Authorization: Bearer <token>` from login/register.
