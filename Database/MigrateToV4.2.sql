-- ============================================================
-- Migration v4.2 — WorkActivityTracker
-- Nuove funzionalità:
--   1. TipiAttivita: lista dinamica dei tipi attività (Lavoro, Permesso, Ferie + custom)
--   2. TipiAttivita_Log: log delle modifiche ai tipi attività
--   3. EventiCalendario: eventi/promemoria con avviso configurabile
-- ============================================================

-- ============================================================
-- 1. Tabella TipiAttivita
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TipiAttivita')
BEGIN
    CREATE TABLE [dbo].[TipiAttivita] (
        [Id]     INT            NOT NULL IDENTITY(1,1) PRIMARY KEY,
        [Nome]   NVARCHAR(50)   NOT NULL,
        [Ordine] INT            NOT NULL DEFAULT 99,
        [Attivo] BIT            NOT NULL DEFAULT 1,
        CONSTRAINT UQ_TipiAttivita_Nome UNIQUE ([Nome])
    );

    -- Inserisce i tipi base
    INSERT INTO [dbo].[TipiAttivita] ([Nome], [Ordine], [Attivo]) VALUES
        (N'Lavoro',   1, 1),
        (N'Permesso', 2, 1),
        (N'Ferie',    3, 1);

    PRINT 'Tabella TipiAttivita creata e popolata con i tipi base.';
END
ELSE
BEGIN
    PRINT 'Tabella TipiAttivita già esistente, skip.';
END

-- ============================================================
-- 2. Tabella TipiAttivita_Log
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TipiAttivita_Log')
BEGIN
    CREATE TABLE [dbo].[TipiAttivita_Log] (
        [Id]           INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        [NomeUtente]   NVARCHAR(100)   NOT NULL,
        [AzioneSvolta] NVARCHAR(20)    NOT NULL,
        [NomeValore]   NVARCHAR(50)    NULL,
        [VecchioValore] NVARCHAR(MAX)  NULL,
        [NuovoValore]  NVARCHAR(MAX)   NULL,
        [Timestamp]    DATETIME        NOT NULL DEFAULT GETDATE()
    );

    PRINT 'Tabella TipiAttivita_Log creata.';
END
ELSE
BEGIN
    PRINT 'Tabella TipiAttivita_Log già esistente, skip.';
END

-- ============================================================
-- 3. Tabella EventiCalendario
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'EventiCalendario')
BEGIN
    CREATE TABLE [dbo].[EventiCalendario] (
        [Id]                INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        [UtenteId]          INT             NOT NULL,
        [Descrizione]       NVARCHAR(300)   NOT NULL,
        [DataEvento]        DATETIME        NOT NULL,
        [GiorniPrimaAvviso] INT             NOT NULL DEFAULT 5,
        [Risolto]           BIT             NOT NULL DEFAULT 0,
        [DataCreazione]     DATETIME        NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_EventiCalendario_Utenti
            FOREIGN KEY ([UtenteId]) REFERENCES [dbo].[Utenti]([Id])
            ON DELETE NO ACTION
    );

    CREATE INDEX IX_EventiCalendario_UtenteId ON [dbo].[EventiCalendario] ([UtenteId]);
    CREATE INDEX IX_EventiCalendario_DataEvento ON [dbo].[EventiCalendario] ([DataEvento]);

    PRINT 'Tabella EventiCalendario creata.';
END
ELSE
BEGIN
    PRINT 'Tabella EventiCalendario già esistente, skip.';
END

PRINT 'Migration v4.2 completata.';
