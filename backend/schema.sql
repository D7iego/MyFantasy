-- =====================================================================
-- MyFantasy — script de base de datos + tablas (MySQL 8 / MariaDB)
-- Generado desde las migraciones EF Core (InitialCreate … AddPlayerMatchStats).
--
-- Uso:
--   mysql -u root -p < backend/schema.sql
-- o pégalo en MySQL Workbench / DBeaver / phpMyAdmin y ejecútalo.
--
-- IDEMPOTENTE: se puede ejecutar entero tantas veces como quieras, tanto en una
-- BD vacía como en una que ya existe (tablas con IF NOT EXISTS, índices en línea
-- dentro de cada tabla, e INSERT IGNORE en el historial). NO borra ni pisa datos.
--
-- Registra TODAS las migraciones en __EFMigrationsHistory, así que aunque dejes
-- Database:AutoMigrate = true, la app arranca como no-op (no recrea ni migra).
-- =====================================================================

CREATE DATABASE IF NOT EXISTS `myfantasy`
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;
USE `myfantasy`;

-- Control de versiones de esquema de EF Core.
CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

-- Ligas del usuario. La por defecto es IsDefault; si ninguna, la de menor CreatedAt.
CREATE TABLE IF NOT EXISTS `Leagues` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ExternalId` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `Name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `IsDefault` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `TeamId` longtext CHARACTER SET utf8mb4 NULL,   -- mi teamId en esta liga (AddLeagueTeamId)
    CONSTRAINT `PK_Leagues` PRIMARY KEY (`Id`),
    UNIQUE KEY `IX_Leagues_ExternalId` (`ExternalId`)
) CHARACTER SET=utf8mb4;

-- Jugadores (dato de mercado). El precio actual NO va aquí, va en PriceSnapshots.
CREATE TABLE IF NOT EXISTS `Players` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ExternalId` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `Name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `Team` varchar(120) CHARACTER SET utf8mb4 NULL,
    `Position` int NOT NULL,                 -- 1=POR 2=DEF 3=MED 4=DEL
    `ImageUrl` longtext CHARACTER SET utf8mb4 NULL,
    `TeamId` longtext CHARACTER SET utf8mb4 NULL,   -- teamId real de LaLiga (AddPlayerTeamId)
    CONSTRAINT `PK_Players` PRIMARY KEY (`Id`),
    UNIQUE KEY `IX_Players_ExternalId` (`ExternalId`)
) CHARACTER SET=utf8mb4;

-- Jugadores activos en mi plantilla (dato personal de trading).
CREATE TABLE IF NOT EXISTS `Holdings` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `PlayerId` int NOT NULL,
    `LeagueId` int NOT NULL,
    `PurchasePrice` bigint NOT NULL,
    `PurchaseDate` date NOT NULL,
    `Status` int NOT NULL,                    -- 0=Activo 1=Vendido
    `PurchasePriceIsManual` tinyint(1) NOT NULL,
    `Season` varchar(9) CHARACTER SET utf8mb4 NOT NULL DEFAULT '',   -- "2026/27" (AddSeasons)
    CONSTRAINT `PK_Holdings` PRIMARY KEY (`Id`),
    KEY `IX_Holdings_LeagueId_PlayerId_Status` (`LeagueId`, `PlayerId`, `Status`),
    KEY `IX_Holdings_PlayerId` (`PlayerId`),
    CONSTRAINT `FK_Holdings_Leagues_LeagueId` FOREIGN KEY (`LeagueId`) REFERENCES `Leagues` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_Holdings_Players_PlayerId` FOREIGN KEY (`PlayerId`) REFERENCES `Players` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

-- Snapshot diario de precio por jugador. PK compuesta (PlayerId, Date) -> UPSERT.
CREATE TABLE IF NOT EXISTS `PriceSnapshots` (
    `PlayerId` int NOT NULL,
    `Date` date NOT NULL,
    `MarketValue` bigint NOT NULL,
    CONSTRAINT `PK_PriceSnapshots` PRIMARY KEY (`PlayerId`, `Date`),
    CONSTRAINT `FK_PriceSnapshots_Players_PlayerId` FOREIGN KEY (`PlayerId`) REFERENCES `Players` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

-- Rendimiento por jornada (goles, asistencias, minutos, puntos) + datos del
-- partido. La API solo expone la temporada en curso; guardamos cada jornada
-- (UPSERT) para conservar histórico entre temporadas. PK (PlayerId, Season, Week).
CREATE TABLE IF NOT EXISTS `PlayerMatchStats` (
    `PlayerId` int NOT NULL,
    `Season` varchar(9) CHARACTER SET utf8mb4 NOT NULL,   -- "2026/27"
    `Week` int NOT NULL,
    `Points` double NULL,
    `Goals` int NULL,
    `Assists` int NULL,
    `Minutes` int NULL,
    `HomeTeam` varchar(120) CHARACTER SET utf8mb4 NULL,
    `AwayTeam` varchar(120) CHARACTER SET utf8mb4 NULL,
    `HomeGoals` int NULL,
    `AwayGoals` int NULL,
    `IsHome` tinyint(1) NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_PlayerMatchStats` PRIMARY KEY (`PlayerId`, `Season`, `Week`),
    CONSTRAINT `FK_PlayerMatchStats_Players_PlayerId` FOREIGN KEY (`PlayerId`) REFERENCES `Players` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

-- Resumen del jugador por temporada: su equipo/posición ESE año (pueden cambiar)
-- + agregados de rendimiento y valor. La "foto por temporada" para comparar años.
-- PK (PlayerId, Season).
CREATE TABLE IF NOT EXISTS `PlayerSeasonStats` (
    `PlayerId` int NOT NULL,
    `Season` varchar(9) CHARACTER SET utf8mb4 NOT NULL,   -- "2026/27"
    `Team` varchar(120) CHARACTER SET utf8mb4 NULL,
    `TeamId` varchar(64) CHARACTER SET utf8mb4 NULL,
    `Position` int NOT NULL,                 -- 1=POR 2=DEF 3=MED 4=DEL
    `TotalPoints` double NULL,
    `Goals` int NULL,
    `Assists` int NULL,
    `Minutes` int NULL,
    `StartValue` bigint NULL,
    `EndValue` bigint NULL,
    `PeakValue` bigint NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_PlayerSeasonStats` PRIMARY KEY (`PlayerId`, `Season`),
    CONSTRAINT `FK_PlayerSeasonStats_Players_PlayerId` FOREIGN KEY (`PlayerId`) REFERENCES `Players` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

-- Operaciones cerradas. Stats congeladas en el momento de la venta.
CREATE TABLE IF NOT EXISTS `Sales` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `PlayerId` int NOT NULL,
    `LeagueId` int NOT NULL,
    `PurchasePrice` bigint NOT NULL,
    `SalePrice` bigint NOT NULL,
    `PurchaseDate` date NOT NULL,
    `SaleDate` date NOT NULL,
    `ProfitLoss` bigint NOT NULL,
    `DailyDelta` bigint NULL,
    `WeeklyDelta` bigint NULL,
    `SalePriceIsManual` tinyint(1) NOT NULL,
    `Season` varchar(9) CHARACTER SET utf8mb4 NOT NULL DEFAULT '',   -- "2026/27" (AddSeasons)
    CONSTRAINT `PK_Sales` PRIMARY KEY (`Id`),
    KEY `IX_Sales_LeagueId_SaleDate` (`LeagueId`, `SaleDate`),
    KEY `IX_Sales_PlayerId` (`PlayerId`),
    CONSTRAINT `FK_Sales_Leagues_LeagueId` FOREIGN KEY (`LeagueId`) REFERENCES `Leagues` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_Sales_Players_PlayerId` FOREIGN KEY (`PlayerId`) REFERENCES `Players` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

-- Alineaciones guardadas (planificador local de onces). FK a Leagues.
CREATE TABLE IF NOT EXISTS `Lineups` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `LeagueId` int NOT NULL,
    `Name` varchar(80) CHARACTER SET utf8mb4 NOT NULL,
    `Formation` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `Data` longtext CHARACTER SET utf8mb4 NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_Lineups` PRIMARY KEY (`Id`),
    KEY `IX_Lineups_LeagueId` (`LeagueId`),
    CONSTRAINT `FK_Lineups_Leagues_LeagueId` FOREIGN KEY (`LeagueId`) REFERENCES `Leagues` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

-- Temporadas: límites (inicio/fin) y cuál está en curso. Tabla auxiliar para
-- separar y comparar datos entre años. Clave = etiqueta "2026/27".
CREATE TABLE IF NOT EXISTS `Seasons` (
    `Label` varchar(9) CHARACTER SET utf8mb4 NOT NULL,
    `StartsOn` date NOT NULL,
    `EndsOn` date NULL,
    `IsCurrent` tinyint(1) NOT NULL,
    CONSTRAINT `PK_Seasons` PRIMARY KEY (`Label`)
) CHARACTER SET=utf8mb4;

-- Token de LaLiga cifrado (una sola fila, Id fijo = 1, NO autoincremental).
CREATE TABLE IF NOT EXISTS `AuthStates` (
    `Id` int NOT NULL,
    `RefreshTokenEnc` longtext CHARACTER SET utf8mb4 NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_AuthStates` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

-- Nota: los índices van EN LÍNEA dentro de cada CREATE TABLE (arriba) para que el
-- IF NOT EXISTS de la tabla los cubra; MySQL 8 no admite IF NOT EXISTS en CREATE INDEX.
-- PlayerMatchStats y PriceSnapshots no necesitan índice extra: su FK (PlayerId) es
-- la columna líder de la PK.

-- Marca TODAS las migraciones como aplicadas (evita que AutoMigrate haga nada).
-- INSERT IGNORE: no falla si alguna ya estaba registrada.
INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES
    ('20260727182003_InitialCreate',     '9.0.11'),
    ('20260728133852_AddAuthState',      '9.0.11'),
    ('20260728134812_AddLeagueTeamId',   '9.0.11'),
    ('20260728140409_AddPlayerTeamId',   '9.0.11'),
    ('20260730132017_AddLineup',         '9.0.11'),
    ('20260801153819_AddPlayerMatchStats','9.0.11'),
    ('20260806120525_AddSeasons',        '9.0.11');

-- Temporada actual (equivale al back-fill de la migración AddSeasons). En BD nueva
-- no hay Holdings/Sales que etiquetar; el primer sync la crea igualmente si falta.
INSERT IGNORE INTO `Seasons` (`Label`, `StartsOn`, `IsCurrent`) VALUES ('2026/27', '2026-07-01', 1);
