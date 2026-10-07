# CRN Products API

A .NET 8 Web API for managing Products and their related Items.

## Tech stack

ASP.NET Core, SQL Server, Entity Framework Core, FluentValidation, JWT authentication, Serilog, Swagger, Docker, and xUnit.

## Project structure

- **API**: controllers and middleware.
- **Application**: DTOs, validation, and services.
- **Domain**: entities.
- **Infrastructure**: database access and authentication.
- **Tests**: unit and integration tests.

## Run locally

Install Docker with Compose, then run from the project folder:

```sh
cp .env.example .env
```

Replace the placeholders in `.env` with your own secrets. Use passwords of at least 12 characters and a JWT key of at least 32 bytes.

```sh
docker compose up --build -d
```

Open [Swagger](http://localhost:5080/swagger).

To run the API without Docker, install the .NET 8 SDK and configure SQL Server. Set `ConnectionStrings:Database`, `Jwt:Key`, `Seed:AdminPassword`, `Seed:ReaderPassword`, and `Database:Initialize=true` through user secrets or environment variables, then run:

```sh
dotnet run --project src/API
```

Startup initialization applies the included EF migrations and creates the demo users.

## Using the API

1. Log in through `POST /api/v1/auth/login` using `admin` or `reader` and the password you configured.
2. Copy the access token into Swagger's **Authorize** box.
3. Use the Products and Items endpoints. Admin can create, update, and delete; Reader can view.
4. When the access token expires, send the refresh token to `/api/v1/auth/refresh` and save the new token pair.
5. To log out, send the refresh token to `/api/v1/auth/revoke`. Existing access tokens remain valid until expiry.

## Endpoints

Base URL: `http://localhost:5080`. Protected endpoints require `Authorization: Bearer <accessToken>`. Read endpoints accept Admin or Reader; writes require Admin. IDs must be positive integers.

| Method | Path | Purpose | Access | Success |
|---|---|---|---|---|
| POST | `/api/v1/auth/login` | Verify username and password | Public | 200: token pair |
| POST | `/api/v1/auth/refresh` | Rotate refresh token | Public; valid refresh token required | 200: new token pair |
| POST | `/api/v1/auth/revoke` | Revoke refresh token family | Public; refresh token required | 204: no body |
| GET | `/api/v1/products` | List products | Admin / Reader | 200: paginated products |
| GET | `/api/v1/products/{id}` | Get one product | Admin / Reader | 200: product |
| POST | `/api/v1/products` | Create product | Admin | 201: product and Location header |
| PUT | `/api/v1/products/{id}` | Update product name | Admin | 204: no body |
| DELETE | `/api/v1/products/{id}` | Delete product and its Items | Admin | 204: no body |
| GET | `/api/v1/products/{id}/items` | List a product's Items | Admin / Reader | 200: paginated Items |
| POST | `/api/v1/products/{id}/items` | Add Item to product | Admin | 201: Item and Location header |
| PUT | `/api/v1/products/{id}/items/{itemId}` | Update Item quantity | Admin | 204: no body |
| DELETE | `/api/v1/products/{id}/items/{itemId}` | Delete Item | Admin | 204: no body |
| GET | `/health` | Check process liveness; does not check SQL | Public | 200: `{"status":"healthy"}` |

### Request bodies

Use `Content-Type: application/json` for requests with a body.

| Endpoint | Example body |
|---|---|
| Login | `{"username":"admin","password":"YOUR_PASSWORD"}` |
| Refresh / revoke | `{"refreshToken":"YOUR_REFRESH_TOKEN"}` |
| Create / update Product | `{"productName":"Keyboard"}` |
| Create / update Item | `{"quantity":10}` |

Product names are required and limited to 255 characters. Quantity must be a nonnegative integer. Audit fields are set by the server.

### Responses and pagination

Product responses contain `id`, `productName`, `createdBy`, `createdOn`, `modifiedBy`, and `modifiedOn`. Item responses contain `id`, `productId`, and `quantity`. Token responses contain `accessToken`, `refreshToken`, and `accessTokenExpiresAt`.

Both list endpoints accept `?pageNumber=1&pageSize=20` (defaults). Page size is 1–100; page number is 1–1,000,000. Results contain `data`, `pageNumber`, `pageSize`, and `totalCount`.

### Errors

Errors use Problem Details JSON: 400 for invalid input, 401 for missing/invalid authentication or invalid login/refresh, 403 for insufficient permission, 404 for unknown resources, 409 for database conflicts, 429 for authentication rate limits, and 500 for unexpected errors. Swagger provides the interactive endpoint documentation and models.

## Tests

```sh
dotnet test CrnAssessment.sln
```

All 14 tests passed locally, and the GitHub build and test workflow passed. See [validation details](VALIDATION.md) for the verification scope, including Docker limitations.

## Documentation

Swagger UI: `/swagger`. OpenAPI JSON: `/swagger/v1/swagger.json` (Development only). Controller actions have C# XML summary comments that appear in Swagger. This C# project uses XML documentation comments in place of JavaScript's JSDoc.

## Deployment

The Compose setup is intended for local use. For deployment:

1. Configure SQL Server and supply credentials and the JWT key through secure configuration.
2. Apply the reviewed migration script in `artifacts/migrations.sql` and provision users before starting the API.
3. Build the Docker image with `docker build -t crn-products-api .` and run it with `ASPNETCORE_ENVIRONMENT=Production` and `Database__Initialize=false`.
4. Configure HTTPS, trusted proxy settings if applicable, database backups, and log collection. Swagger is disabled in Production.

## Local screenshot

![Products API running locally](docs/screenshots/local-swagger.jpg)
