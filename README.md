# MyFantasy

App **personal** de seguimiento de precios de mercado de LaLiga Fantasy y registro
automático de operaciones de compra/venta (G/P y evolución de precios), con
planificador de onces, análisis de mercado (puja sugerida) y vista de rivales.
Uso local.

- **Backend:** ASP.NET Core Web API (.NET 10) + EF Core + MySQL (Pomelo) — ✅ implementado
- **Frontend:** Nuxt 3 (SPA) + Tailwind + ApexCharts — ✅ implementado

La API C# es la **única** que habla con la API no oficial de LaLiga
(`https://fantasy-api.llt-services.com`); el frontend solo consume `/api/...`.

---

## Arquitectura

```
La API de LaLiga  ──►  Backend C# (IFantasyApiClient)  ──►  MySQL (snapshots + operaciones)
                                     │
                                     └──►  API interna /api/...  ──►  Frontend Nuxt (SPA)
```

- **Dato de mercado** (de la API, igual para todos): precio actual → se guarda un
  **snapshot diario** por jugador (de TODA la competición) para poder calcular
  deltas (la API solo da el precio de hoy).
- **Dato personal de trading** (lo genero yo): compra/venta, estado, G/P, alineaciones.
- La **detección de fichajes/ventas es automática**: al sincronizar se compara la
  plantilla de la API con la guardada (nuevo = fichaje, desaparecido = venta con stats congeladas).

## Pestañas (frontend)

| Pestaña | Qué hace | Endpoints |
|---------|----------|-----------|
| 🏆 **Ligas** | Tus ligas; marcar la liga por defecto (el resto de vistas trabajan sobre ella). | `GET /api/leagues`, `PUT /api/leagues/{id}/default` |
| 👥 **Jugadores** | Tu plantilla activa: precio, deltas diario/semanal, precio de compra y G/P. Editar precio de compra manual. | `GET /api/players`, `PUT /api/players/holdings/{id}/purchase-price` |
| ⚽ **Alineación** | Planificador **local** de onces (formación + huecos). Carga el once oficial como punto de partida. No escribe en el juego. | `GET/POST /api/lineups`, `PUT/DELETE /api/lineups/{id}`, `GET /api/lineups/official` |
| 🛒 **Mercado** | Agentes libres que vende la liga con su evolución de precio y **puja sugerida** (heurística); bloque "En venta" con tus listados y la mejor oferta recibida. | `GET /api/market` |
| 📋 **General** | TODOS los jugadores de la competición (los tengas o no): precio, deltas y tendencia. Filtro por equipo (con escudos) y agregados del equipo. | `GET /api/players/all?teamId=`, `GET /api/teams` |
| ⚔️ **Rivales** | Plantilla de cualquier manager de la liga, enriquecida con deltas y estado de cláusula/blindaje. | `GET /api/rivals?teamId=` |
| 🗂️ **Historial** | "Sin vender" (holdings activos, en vivo) y "Vendidos" (operaciones cerradas, valores congelados). Ajustar precio de venta. | `GET /api/history/holdings`, `GET /api/history/sales`, `PUT /api/history/sales/{id}/sale-price` |
| 📊 **Stats** | Métricas sobre tus datos: G/P total, ratio de aciertos, mejor/peor venta, valor de cartera, plusvalía no realizada, dinero disponible y serie diaria. | `GET /api/stats`, `GET /api/stats/daily-pnl?days=` |

Además, cualquier jugador abre un **modal de detalle** con histórico de precios y
parte deportiva (puntos/partidos): `GET /api/players/{id}/detail`.

El **modal de re-login** aparece solo cuando caduca la sesión de LaLiga (ver más abajo).

## Modelo de datos

| Tabla | Campos clave |
|-------|--------------|
| `Leagues` | `ExternalId`, `Name`, `IsDefault`, `TeamId`, `CreatedAt` |
| `Players` | `ExternalId`, `Name`, `Team`, `TeamId`, `Position`, `ImageUrl` |
| `PriceSnapshots` | **PK (`PlayerId`, `Date`)**, `MarketValue` — UPSERT diario |
| `Holdings` | `PlayerId`, `LeagueId`, `PurchasePrice`, `PurchaseDate`, `Status`, `PurchasePriceIsManual` |
| `Sales` | + `SalePrice`, `SaleDate`, `ProfitLoss`, `DailyDelta`, `WeeklyDelta` (**congelados**), `SalePriceIsManual` |
| `Lineups` | `LeagueId`, `Name`, `Formation` (p. ej. `4-3-3`), `Data` (JSON hueco→jugador), `UpdatedAt` |
| `AuthStates` | refresh token **cifrado** en BD (ASP.NET Data Protection), reutilizado tras reinicios |

> `backend/schema.sql` refleja solo la migración `InitialCreate`; las tablas y
> columnas añadidas después (`AuthStates`, `Lineups`, `Leagues.TeamId`,
> `Players.TeamId`) las crea `Database:AutoMigrate` al arrancar. Para partir de
> cero, deja que la app migre sola (ver §5).

## API interna (`/api`)

| Método | Ruta | Pestaña / Uso |
|--------|------|---------------|
| `POST` | `/api/sync` | Sincroniza (snapshot de precios + diff de plantilla) |
| `GET` | `/api/leagues` · `PUT /api/leagues/{id}/default` | Ligas |
| `GET` | `/api/players` · `PUT /api/players/holdings/{id}/purchase-price` | Jugadores |
| `GET` | `/api/players/all?teamId=` | General (todos los jugadores + agregado por equipo) |
| `GET` | `/api/players/{id}/detail` | Modal de detalle (histórico + deportivo) |
| `GET` | `/api/teams` | Equipos con escudo (filtro de General) |
| `GET/POST` | `/api/lineups` · `PUT/DELETE /api/lineups/{id}` · `GET /api/lineups/official` | Alineación |
| `GET` | `/api/market` | Mercado (agentes libres + puja + "En venta") |
| `GET` | `/api/rivals?teamId=` | Rivales |
| `GET` | `/api/history/holdings` · `/api/history/sales` · `PUT …/sales/{id}/sale-price` | Historial |
| `GET` | `/api/stats` · `/api/stats/daily-pnl?days=` | Stats |
| `POST` | `/api/auth/login` · `GET /api/auth/status` | Re-login / estado de sesión |

## Puja sugerida (heurística de Mercado)

**No es un dato de la API**: es una recomendación orientativa. Combina al 50/50:

- **Rendimiento reciente:** media de puntos de las últimas N jornadas (por defecto 5),
  normalizada frente al resto del mercado de hoy (comparativo).
- **Tendencia de precio:** variación reciente de precio (%) contra un rango de referencia fijo.

La puja se **ancla en la recompra de la liga**: como al vender recuperas hasta +10 %
del valor, ese es el techo "seguro"; el score `0..1` mapea de `-BidMaxDrop` (jugador
flojo/en caída) a `+BidSellBackPct + BidMomentumPremium` (fuerte, apostando a que
siga subiendo). En pretemporada (sin partidos) la puja es **solo económica**
(100 % tendencia de precio). Todos los pesos son configurables en la sección
`Fantasy` de `appsettings.json` (`BidRecentWeeks`, `BidPriceTrendRangePct`,
`BidMaxDrop`, `BidSellBackPct`, `BidMomentumPremium`).

---

## Puesta en marcha

Atajo: **`start.bat`** arranca backend + frontend en dos ventanas y abre el
navegador. Requiere haber configurado antes las credenciales (§3–§4). El resto de
la sección explica el arranque manual.

### Backend

#### 1. Requisitos
- .NET SDK 10
- MySQL 8 (o MariaDB) en marcha
- Node 20 (para el frontend)

#### 2. Crear la base de datos
```sql
CREATE DATABASE myfantasy CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

#### 3. Configurar la cadena de conexión
Edita `backend/MyFantasy.Api/appsettings.json` → `ConnectionStrings:MySql`, o mejor
con user-secrets (no se commitea):
```bash
cd backend/MyFantasy.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:MySql" "server=localhost;port=3306;database=myfantasy;user=root;password=TU_PASSWORD"
```

#### 4. Configurar el token de LaLiga (NO en el repo)
No hay UI de login: se coloca un **refresh token** (o un `id_token` ya obtenido) y
el backend renueva solo el bearer. Consíguelo capturando el tráfico de la app oficial.

**Con user-secrets** (arranque manual con `dotnet run`):
```bash
dotnet user-secrets set "Fantasy:Auth:RefreshToken" "<refresh_token>"
# Si tu cuenta usó el flujo Google/nativo, cambia también el client:
# dotnet user-secrets set "Fantasy:Auth:ClientId" "af88bcff-1157-40a0-b579-030728aacf0b"
```

**Con variables de entorno persistentes** (lo que espera `start.bat`). Ejecuta este
bloque de PowerShell **una vez** (nota el doble `__` = anidamiento de config):
```powershell
[Environment]::SetEnvironmentVariable('Fantasy__Auth__RefreshToken', '<refresh_token>', 'User')
[Environment]::SetEnvironmentVariable('ConnectionStrings__MySql', 'server=localhost;port=3306;database=myfantasy;user=root;password=TU_PASSWORD', 'User')
# Solo si usaste el flujo Google/nativo:
# [Environment]::SetEnvironmentVariable('Fantasy__Auth__ClientId', 'af88bcff-1157-40a0-b579-030728aacf0b', 'User')
```
Abre una consola nueva para que tome las variables.

> Alternativa rápida para probar: `Fantasy:Auth:BearerToken` (o `Fantasy__Auth__BearerToken`)
> con un `id_token` suelto — caduca en ~1 h y no se puede renovar sin refresh token.

#### 5. Migraciones y arranque
Las migraciones se aplican **automáticamente** al arrancar (`Database:AutoMigrate`).
Solo:
```bash
dotnet run --project backend/MyFantasy.Api
```
API en `http://localhost:5298` (Swagger en `/openapi/v1.json`).

#### 6. Primer uso
```bash
curl -X POST http://localhost:5298/api/sync   # trae precios + detecta tu plantilla
curl http://localhost:5298/api/players
```
Los endpoints de prueba están en `backend/MyFantasy.Api/MyFantasy.Api.http`.

### Frontend

SPA de Nuxt 3 (`ssr: false`) que consume la API C#. La URL del backend se configura
con `NUXT_PUBLIC_API_BASE` (por defecto `http://localhost:5298`).

```bash
cd frontend
npm install
npm run dev        # http://localhost:3000
```

**Deploy (Netlify):** ya hay `netlify.toml` con *Base directory* = `frontend`,
`npm run generate` → `.output/public` y fallback SPA. Define
`NUXT_PUBLIC_API_BASE` apuntando a tu backend público.

## Re-login cuando el refresh_token caduca (cuentas Google)

Con cuenta de **Google** no se puede automatizar el login (Azure B2C + Google no
admiten usuario/contraseña por API). Por eso, cuando el `refresh_token` deja de ser
válido, el backend responde `401 { needsLogin: true }` en cualquier endpoint que
dependa de LaLiga y el **frontend abre un modal** para pegar un token nuevo:

- `POST /api/auth/login` con `{ "refreshToken": "…" }` (recomendado) o
  `{ "bearerToken": "<id_token>" }` (parche, caduca ~1 h).
- El `refresh_token` se guarda **cifrado en BD** (tabla `AuthStates`, vía ASP.NET
  Data Protection) y se reutiliza tras reinicios; la contraseña de Google nunca se
  envía ni se almacena.
- `GET /api/auth/status` → `{ authenticated: bool }` (la app lo consulta al arrancar
  para abrir el login proactivamente).

## Endpoints reales de LaLiga confirmados

Los valores por defecto en `appsettings.json` (sección `Fantasy`) están **confirmados**
a partir de la app de referencia y son configurables sin tocar código: base
`fantasy-api.llt-services.com`, competición `1`, jugadores `/v1/competition/1/players`,
plantilla `/v1/competition/1/leagues/{leagueId}/teams/{teamId}`, mercado, ofertas,
clasificación, alineación, teams-master y stats por jornada; auth B2C en
`login.laliga.es` (el bearer real es el `id_token`).

> Aviso: la API de LaLiga es no oficial y no documentada. Uso personal. No incluir
> credenciales en el repo (usar user-secrets o variables de entorno).
