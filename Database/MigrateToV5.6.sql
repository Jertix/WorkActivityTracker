-- ============================================================
-- Migrazione v5.6 — WorkActivityTracker
--   1. Software: lista dei software (combo "Software" del form attività), CONDIVISA fra gli utenti
--   2. Software_Log: log delle modifiche alla lista (utente, azione, valori prima/dopo, data)
--   3. Attivita.SoftwareId: riferimento al software (NULL per Permesso/Ferie/personalizzati)
--   4. Backfill: assegna il software di default alle attività di tipo Lavoro già esistenti
--
-- Lo script è idempotente: si può rieseguire senza danni.
--
-- !!! PRIMA DI ESEGUIRE: impostare @NomeProgramma (sezione 4) con lo STESSO valore di
-- !!! AppSettings:NomeProgramma in appsettings.json. SQL non può leggere quel file.
-- !!! Se resta il segnaposto, le sezioni 1-3 vengono eseguite e il backfill viene saltato.
-- ============================================================

-- Necessario per creare indici su Attivita, che ha già un indice filtrato (v5.5).
-- SSMS lo imposta ON di default, sqlcmd no (senza -I).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ============================================================
-- 1. Tabella Software
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Software')
BEGIN
    CREATE TABLE [dbo].[Software] (
        [Id]     INT            NOT NULL IDENTITY(1,1) PRIMARY KEY,
        [Nome]   NVARCHAR(100)  NOT NULL,
        [Ordine] INT            NOT NULL DEFAULT 99,
        [Attivo] BIT            NOT NULL DEFAULT 1,
        CONSTRAINT UQ_Software_Nome UNIQUE ([Nome])
    );
    PRINT 'Tabella Software creata.';
END
ELSE
    PRINT 'Tabella Software già esistente, skip.';
GO

-- ============================================================
-- 2. Tabella Software_Log
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Software_Log')
BEGIN
    CREATE TABLE [dbo].[Software_Log] (
        [Id]            INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        [NomeUtente]    NVARCHAR(100)   NOT NULL,
        [AzioneSvolta]  NVARCHAR(20)    NOT NULL,
        [NomeValore]    NVARCHAR(100)   NULL,
        [VecchioValore] NVARCHAR(MAX)   NULL,
        [NuovoValore]   NVARCHAR(MAX)   NULL,
        [Timestamp]     DATETIME        NOT NULL DEFAULT GETDATE()
    );
    PRINT 'Tabella Software_Log creata.';
END
ELSE
    PRINT 'Tabella Software_Log già esistente, skip.';
GO

-- ============================================================
-- 3. Colonna Attivita.SoftwareId + FK + indice
-- ============================================================
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Attivita') AND name = 'SoftwareId'
)
BEGIN
    ALTER TABLE [dbo].[Attivita] ADD [SoftwareId] INT NULL;
    PRINT 'Colonna Attivita.SoftwareId aggiunta.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Attivita_Software')
BEGIN
    -- NO ACTION: il software si elimina solo in modo "soft" (Attivo = 0)
    ALTER TABLE [dbo].[Attivita]
        ADD CONSTRAINT FK_Attivita_Software
        FOREIGN KEY ([SoftwareId]) REFERENCES [dbo].[Software]([Id])
        ON DELETE NO ACTION;
    PRINT 'FK_Attivita_Software creata.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Attivita_SoftwareId' AND object_id = OBJECT_ID('Attivita')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Attivita_SoftwareId ON [dbo].[Attivita] ([SoftwareId]);
    PRINT 'Indice IX_Attivita_SoftwareId creato.';
END
GO

-- ============================================================
-- 4. Backfill: software di default sulle attività Lavoro esistenti
-- ============================================================
DECLARE @NomeProgramma NVARCHAR(100) = N'<<NOME_PROGRAMMA>>';   -- <-- IMPOSTARE (vedi intestazione)
-- Solo il nome utente senza dominio, come lo registra l'app (Environment.UserName)
DECLARE @Utente NVARCHAR(100) = RIGHT(SUSER_SNAME(), CHARINDEX(N'\', REVERSE(SUSER_SNAME()) + N'\') - 1);

IF @NomeProgramma = N'<<NOME_PROGRAMMA>>' OR LTRIM(RTRIM(@NomeProgramma)) = N''
BEGIN
    PRINT 'ATTENZIONE: @NomeProgramma non impostato, backfill SALTATO. Impostarlo e rieseguire lo script.';
END
ELSE
BEGIN
    SET @NomeProgramma = LTRIM(RTRIM(@NomeProgramma));

    IF NOT EXISTS (SELECT 1 FROM [dbo].[Software] WHERE [Nome] = @NomeProgramma)
    BEGIN
        INSERT INTO [dbo].[Software] ([Nome], [Ordine], [Attivo]) VALUES (@NomeProgramma, 1, 1);
        INSERT INTO [dbo].[Software_Log] ([NomeUtente], [AzioneSvolta], [NomeValore], [NuovoValore])
        VALUES (@Utente, N'Nuovo', @NomeProgramma,
                N'Nome: ' + @NomeProgramma + N' | Ordine: 1 | Attivo: Sì (default, MigrateToV5.6)');
        PRINT 'Software di default inserito: ' + @NomeProgramma;
    END

    DECLARE @SoftwareId INT = (SELECT [Id] FROM [dbo].[Software] WHERE [Nome] = @NomeProgramma);

    UPDATE [dbo].[Attivita]
    SET [SoftwareId] = @SoftwareId
    WHERE [SoftwareId] IS NULL
      AND ([TipoAttivita] = N'Lavoro' OR [TipoAttivita] IS NULL);   -- NULL = Lavoro (righe storiche)

    PRINT 'Attività Lavoro aggiornate con il software di default: ' + CAST(@@ROWCOUNT AS NVARCHAR(20));
END
GO

PRINT 'Migrazione v5.6 completata.';
