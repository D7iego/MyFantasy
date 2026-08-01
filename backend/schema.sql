-- =====================================================================
-- MyFantasy — script de base de datos + tablas (MySQL 8 / MariaDB)
-- Generado desde las migraciones EF Core (InitialCreate … AddPlayerMatchStats).
--
-- Uso:
--   mysql -u root -p < backend/schema.sql
-- o pégalo en MySQL Workbench / DBeaver / phpMyAdmin y ejecútalo.
--
-- Registra TODAS las migraciones en __EFMigrationsHistory, así que aunque
-- dejes Database:AutoMigrate = true, la app NO intentará recrear ni migrar
-- las tablas (arranca como no-op).
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

START TRANSACTION;

-- Ligas del usuario. La por defecto es IsDefault; si ninguna, la de menor CreatedAt.
CREATE TABLE `Leagues` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ExternalId` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `Name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `IsDefault` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `TeamId` longtext CHARACTER SET utf8mb4 NULL,   -- mi teamId en esta liga (AddLeagueTeamId)
    CONSTRAINT `PK_Leagues` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

-- Jugadores (dato de mercado). El precio actual NO va aquí, va en PriceSnapshots.
CREATE TABLE `Players` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ExternalId` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `Name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `Team` varchar(120) CHARACTER SET utf8mb4 NULL,
    `Position` int NOT NULL,                 -- 1=POR 2=DEF 3=MED 4=DEL
    `ImageUrl` longtext CHARACTER SET utf8mb4 NULL,
    `TeamId` longtext CHARACTER SET utf8mb4 NULL,   -- teamId real de LaLiga (AddPlayerTeamId)
    CONSTRAINT `PK_Players` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

-- Jugadores activos en mi plantilla (dato personal de trading).
CREATE TABLE `Holdings` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `PlayerId` int NOT NULL,
    `LeagueId` int NOT NULL,
    `PurchasePrice` bigint NOT NULL,
    `PurchaseDate` date NOT NULL,
    `Status` int NOT NULL,                    -- 0=Activo 1=Vendido
    `PurchasePriceIsManual` tinyint(1) NOT NULL,
    CONSTRAINT `PK_Holdings` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Holdings_Leagues_LeagueId` FOREIGN KEY (`LeagueId`) REFERENCES `Leagues` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_Holdings_Players_PlayerId` FOREIGN KEY (`PlayerId`) REFERENCES `Players` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

-- Snapshot diario de precio por jugador. PK compuesta (PlayerId, Date) -> UPSERT.
CREATE TABLE `PriceSnapshots` (
    `PlayerId` int NOT NULL,
    `Date` date NOT NULL,
    `MarketValue` bigint NOT NULL,
    CONSTRAINT `PK_PriceSnapshots` PRIMARY KEY (`PlayerId`, `Date`),
    CONSTRAINT `FK_PriceSnapshots_Players_PlayerId` FOREIGN KEY (`PlayerId`) REFERENCES `Players` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

-- Rendimiento por jornada (goles, asistencias, minutos, puntos) + datos del
-- partido. La API solo expone la temporada en curso; guardamos cada jornada
-- (UPSERT) para conservar histórico entre temporadas. PK (PlayerId, Season, Week).
CREATE TABLE `PlayerMatchStats` (
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

-- Operaciones cerradas. Stats congeladas en el momento de la venta.
CREATE TABLE `Sales` (
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
    CONSTRAINT `PK_Sales` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Sales_Leagues_LeagueId` FOREIGN KEY (`LeagueId`) REFERENCES `Leagues` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_Sales_Players_PlayerId` FOREIGN KEY (`PlayerId`) REFERENCES `Players` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

-- Alineaciones guardadas (planificador local de onces). FK a Leagues.
CREATE TABLE `Lineups` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `LeagueId` int NOT NULL,
    `Name` varchar(80) CHARACTER SET utf8mb4 NOT NULL,
    `Formation` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `Data` longtext CHARACTER SET utf8mb4 NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_Lineups` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Lineups_Leagues_LeagueId` FOREIGN KEY (`LeagueId`) REFERENCES `Leagues` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

-- Token de LaLiga cifrado (una sola fila, Id fijo = 1, NO autoincremental).
CREATE TABLE `AuthStates` (
    `Id` int NOT NULL,
    `RefreshTokenEnc` longtext CHARACTER SET utf8mb4 NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_AuthStates` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

-- Índices.
CREATE INDEX `IX_Holdings_LeagueId_PlayerId_Status` ON `Holdings` (`LeagueId`, `PlayerId`, `Status`);
CREATE INDEX `IX_Holdings_PlayerId` ON `Holdings` (`PlayerId`);
CREATE UNIQUE INDEX `IX_Leagues_ExternalId` ON `Leagues` (`ExternalId`);
CREATE UNIQUE INDEX `IX_Players_ExternalId` ON `Players` (`ExternalId`);
CREATE INDEX `IX_Sales_LeagueId_SaleDate` ON `Sales` (`LeagueId`, `SaleDate`);
CREATE INDEX `IX_Sales_PlayerId` ON `Sales` (`PlayerId`);
CREATE INDEX `IX_Lineups_LeagueId` ON `Lineups` (`LeagueId`);
-- PlayerMatchStats no necesita índice de FK: PlayerId es la columna líder de su PK.

-- Marca TODAS las migraciones como aplicadas (evita que AutoMigrate haga nada).
INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES
    ('20260727182003_InitialCreate',     '9.0.11'),
    ('20260728133852_AddAuthState',      '9.0.11'),
    ('20260728134812_AddLeagueTeamId',   '9.0.11'),
    ('20260728140409_AddPlayerTeamId',   '9.0.11'),
    ('20260730132017_AddLineup',         '9.0.11'),
    ('20260801153819_AddPlayerMatchStats','9.0.11');

COMMIT;
