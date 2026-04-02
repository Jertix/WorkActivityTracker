-- ============================================================
-- Migrazione WorkActivityTracker v4.0 -> v4.1
-- Aggiunge le tabelle per la gestione degli ambienti di rilascio:
--   - TipiAmbientiRilascio        (lista tipi: Test, Qualità, ecc.)
--   - TipiAmbientiRilascio_Log    (log modifiche tipi)
--   - VersioniRilascio            (lista versioni: 3.2.50, ecc.)
--   - VersioniRilascio_Log        (log modifiche versioni)
--   - AttivitaAmbientiRilascio    (3 coppie ambiente/versione per attività)
-- ============================================================

-- ============================================================
-- 1. Tipi ambienti di rilascio
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TipiAmbientiRilascio')
BEGIN
    CREATE TABLE [dbo].[TipiAmbientiRilascio] (
        [Id]     INT           IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Nome]   NVARCHAR(100) NOT NULL,
        [Attivo] BIT           NOT NULL DEFAULT 1,
        CONSTRAINT UQ_TipiAmbientiRilascio_Nome UNIQUE ([Nome])
    );

    -- Valori predefiniti
    INSERT INTO [dbo].[TipiAmbientiRilascio] ([Nome], [Attivo]) VALUES
        (N'Test', 1),
        (N'Qualità', 1),
        (N'Pre-produzione', 1),
        (N'Produzione', 1);

    PRINT 'Tabella TipiAmbientiRilascio creata con valori predefiniti.';
END
ELSE
    PRINT 'Tabella TipiAmbientiRilascio già esistente, saltata.';
GO

-- ============================================================
-- 2. Log modifiche tipi ambienti rilascio
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TipiAmbientiRilascio_Log')
BEGIN
    CREATE TABLE [dbo].[TipiAmbientiRilascio_Log] (
        [Id]           INT            IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [NomeUtente]   NVARCHAR(100)  NOT NULL,
        [AzioneSvolta] NVARCHAR(20)   NOT NULL,
        [NomeValore]   NVARCHAR(100)  NULL,
        [VecchioValore] NVARCHAR(MAX) NULL,
        [NuovoValore]   NVARCHAR(MAX) NULL,
        [Timestamp]    DATETIME2      NOT NULL DEFAULT GETDATE()
    );

    PRINT 'Tabella TipiAmbientiRilascio_Log creata.';
END
ELSE
    PRINT 'Tabella TipiAmbientiRilascio_Log già esistente, saltata.';
GO

-- ============================================================
-- 3. Versioni di rilascio
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'VersioniRilascio')
BEGIN
    CREATE TABLE [dbo].[VersioniRilascio] (
        [Id]      INT           IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Versione] NVARCHAR(100) NOT NULL,
        [Attivo]  BIT           NOT NULL DEFAULT 1,
        CONSTRAINT UQ_VersioniRilascio_Versione UNIQUE ([Versione])
    );

    PRINT 'Tabella VersioniRilascio creata.';
END
ELSE
    PRINT 'Tabella VersioniRilascio già esistente, saltata.';
GO

-- ============================================================
-- 4. Log modifiche versioni rilascio
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'VersioniRilascio_Log')
BEGIN
    CREATE TABLE [dbo].[VersioniRilascio_Log] (
        [Id]            INT            IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [NomeUtente]    NVARCHAR(100)  NOT NULL,
        [AzioneSvolta]  NVARCHAR(20)   NOT NULL,
        [ValoreVersione] NVARCHAR(100) NULL,
        [VecchioValore]  NVARCHAR(MAX) NULL,
        [NuovoValore]    NVARCHAR(MAX) NULL,
        [Timestamp]     DATETIME2      NOT NULL DEFAULT GETDATE()
    );

    PRINT 'Tabella VersioniRilascio_Log creata.';
END
ELSE
    PRINT 'Tabella VersioniRilascio_Log già esistente, saltata.';
GO

-- ============================================================
-- 5. Coppie ambiente/versione associate alle attività
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AttivitaAmbientiRilascio')
BEGIN
    CREATE TABLE [dbo].[AttivitaAmbientiRilascio] (
        [Id]           INT           IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [AttivitaId]   INT           NOT NULL,
        [Posizione]    INT           NOT NULL,     -- 1, 2 o 3
        [TipoAmbiente] NVARCHAR(100) NULL,
        [Versione]     NVARCHAR(100) NULL,
        CONSTRAINT FK_AttivitaAmbientiRilascio_Attivita
            FOREIGN KEY ([AttivitaId]) REFERENCES [dbo].[Attivita]([Id]) ON DELETE CASCADE
    );

    CREATE INDEX IX_AttivitaAmbientiRilascio_AttivitaId
        ON [dbo].[AttivitaAmbientiRilascio] ([AttivitaId]);

    PRINT 'Tabella AttivitaAmbientiRilascio creata.';
END
ELSE
    PRINT 'Tabella AttivitaAmbientiRilascio già esistente, saltata.';
GO

PRINT 'Migrazione v4.1 completata.';
GO
