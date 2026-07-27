# MyFantasy

App **personal** de seguimiento de precios de mercado de LaLiga Fantasy y registro
automático de operaciones de compra/venta (G/P y evolución de precios). Uso local.

- **Backend:** ASP.NET Core Web API (.NET 10) + EF Core + MySQL (Pomelo) — ✅ implementado
- **Frontend:** Nuxt 3 + Tailwind + ApexCharts — ⏳ siguiente paso

La API C# es la **única** que habla con la API no oficial de LaLiga
(`https://fantasy-api.llt-services.com`); el frontend solo consumirá `/api/...`.

---

## Arquitectura

```
La API de LaLiga  ──►  Backend C# (IFantasyApiClient)  ──►  MySQL (snapshots + operaciones)
                                     │
                                     └──►  API interna /api/...  ──►  Frontend Nuxt
```

- **Dato de mercado** (de la API, igual para todos): precio actual → se guarda un
  **snapshot diario** por jugador para poder calcular deltas (la API solo da el precio de hoy).
- **Dato personal de trading** (lo genero yo): compra/venta, estado, G/P.
- La **detección de fichajes/ventas es automática**: al sincronizar se compara la
  plantilla de la API con la guardada (nuevo = fichaje, desaparecido = venta con stats congeladas).

## Modelo de datos

| Tabla | Campos clave |
|-------|--------------|
| `Leagues` | `ExternalId`, `Name`, `IsDefault`, `CreatedAt` |
| `Players` | `ExternalId`, `Name`, `Team`, `Position` |
| `PriceSnapshots` | **PK (`PlayerId`, `Date`)**, `MarketValue` — UPSERT diario |
| `Holdings` | `PlayerId`, `LeagueId`, `PurchasePrice`, `PurchaseDate`, `Status` |
| `Sales` | + `SalePrice`, `SaleDate`, `ProfitLoss`, `DailyDelta`, `WeeklyDelta` (**congelados**) |

## API interna (`/api`)

| Método | Ruta | Pestaña |
|--------|------|---------|
| `POST` | `/api/sync` | Sincroniza (precios + diff de plantilla) |
| `GET` | `/api/leagues` · `PUT /api/leagues/{id}/default` | 1 — Ligas |
| `GET` | `/api/players` · `PUT /api/players/holdings/{id}/purchase-price` | 2 — Jugadores |
| `GET` | `/api/history/holdings` · `/api/history/sales` · `PUT …/sales/{id}/sale-price` | 3 — Historial |
| `GET` | `/api/stats` | 4 — Stats |

---

## Puesta en marcha (backend)

### 1. Requisitos
- .NET SDK 10
- MySQL 8 (o MariaDB) en marcha

### 2. Crear la base de datos
```sql
CREATE DATABASE myfantasy CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

### 3. Configurar la cadena de conexión
Edita `backend/MyFantasy.Api/appsettings.json` → `ConnectionStrings:MySql`, o mejor
con user-secrets (no se commitea):
```bash
cd backend/MyFantasy.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:MySql" "server=localhost;port=3306;database=myfantasy;user=root;password=TU_PASSWORD"
```

### 4. Configurar el token de LaLiga (user-secrets, NO en el repo)
No hay UI de login: se coloca un **refresh token** (o un `id_token` ya obtenido)
y el backend renueva solo el bearer. Consíguelo capturando el tráfico de la app
oficial o desde la app de referencia.
```bash
dotnet user-secrets set "Fantasy:Auth:RefreshToken" "<refresh_token>"
# Si tu cuenta usó el flujo Google/nativo, cambia también el client:
# dotnet user-secrets set "Fantasy:Auth:ClientId" "af88bcff-1157-40a0-b579-030728aacf0b"
```
> Alternativa rápida para probar: `dotnet user-secrets set "Fantasy:Auth:BearerToken" "<id_token>"` (caduca en ~1 h, sin refresh).

### 5. Migraciones y arranque
La migración `InitialCreate` ya está creada y se aplica **automáticamente** al
arrancar (`Database:AutoMigrate`). Solo:
```bash
dotnet run --project backend/MyFantasy.Api
```
API en `http://localhost:5298` (Swagger en `/openapi/v1.json`).

### 6. Primer uso
```bash
curl -X POST http://localhost:5298/api/sync   # trae precios + detecta tu plantilla
curl http://localhost:5298/api/players
```

Los endpoints de prueba están en `backend/MyFantasy.Api/MyFantasy.Api.http`.

## Endpoints reales de LaLiga confirmados

Los valores por defecto en `appsettings.json` (`Fantasy` section) están **confirmados**
a partir de la app de referencia y son configurables sin tocar código:
base `fantasy-api.llt-services.com`, competición `1`, jugadores
`/v1/competition/1/players`, plantilla `/v1/competition/1/leagues/{leagueId}/teams/{teamId}`,
auth B2C en `login.laliga.es` (el bearer real es el `id_token`).

> Aviso: la API de LaLiga es no oficial y no documentada. Uso personal. No incluir
> credenciales en el repo (usar user-secrets).
