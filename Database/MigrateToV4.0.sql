-- ============================================================
-- Migrazione a v4.0
-- Aggiunge:
--   1. Tabella Clienti_Log   (log modifiche lista clienti)
--   2. Tabella Segnalazioni  (segnalazioni bug / richieste)
--   3. Tabella Segnalazioni_Risposte (risposte alle segnalazioni)
-- Script idempotente: usa IF NOT EXISTS prima di ogni CREATE
-- ============================================================

-- ============================================================
-- 1. CLIENTI_LOG
-- ============================================================
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_NAME = 'Clienti_Log'
)
BEGIN
    CREATE TABLE [Clienti_Log] (
        [Id]           INT            NOT NULL IDENTITY(1,1) PRIMARY KEY,
        [NomeUtente]   NVARCHAR(100)  NOT NULL,
        [AzioneSvolta] NVARCHAR(20)   NOT NULL,  -- 'Nuovo', 'Modifica', 'Elimina'
        [NomeCliente]  NVARCHAR(100)  NULL,
        [VecchioValore] NVARCHAR(MAX) NULL,
        [NuovoValore]  NVARCHAR(MAX)  NULL,
        [Timestamp]    DATETIME2      NOT NULL DEFAULT GETDATE()
    );

    CREATE INDEX [IX_Clienti_Log_Timestamp]
        ON [Clienti_Log] ([Timestamp] DESC);

    PRINT 'Tabella Clienti_Log creata.';
END
ELSE
    PRINT 'Tabella Clienti_Log gia'' presente.';
GO

-- ============================================================
-- 2. SEGNALAZIONI
-- ============================================================
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_NAME = 'Segnalazioni'
)
BEGIN
    CREATE TABLE [Segnalazioni] (
        [Id]                INT            NOT NULL IDENTITY(1,1) PRIMARY KEY,
        [UtenteId]          INT            NOT NULL,
        [TestoSegnalazione] NVARCHAR(MAX)  NOT NULL,
        [DataRichiesta]     DATETIME2      NOT NULL DEFAULT GETDATE(),
        [Stato]             NVARCHAR(50)   NOT NULL DEFAULT 'In attesa di risposta',

        CONSTRAINT [FK_Segnalazioni_Utenti]
            FOREIGN KEY ([UtenteId]) REFERENCES [Utenti]([Id])
    );

    CREATE INDEX [IX_Segnalazioni_DataRichiesta]
        ON [Segnalazioni] ([DataRichiesta] DESC);

    CREATE INDEX [IX_Segnalazioni_UtenteId]
        ON [Segnalazioni] ([UtenteId]);

    PRINT 'Tabella Segnalazioni creata.';
END
ELSE
    PRINT 'Tabella Segnalazioni gia'' presente.';
GO

-- ============================================================
-- 3. SEGNALAZIONI_RISPOSTE
-- ============================================================
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_NAME = 'Segnalazioni_Risposte'
)
BEGIN
    CREATE TABLE [Segnalazioni_Risposte] (
        [Id]              INT            NOT NULL IDENTITY(1,1) PRIMARY KEY,
        [SegnalazioneId]  INT            NOT NULL,
        [UtenteId]        INT            NOT NULL,
        [TestoRisposta]   NVARCHAR(MAX)  NOT NULL,
        [NuovoStato]      NVARCHAR(50)   NOT NULL,
        [DataRisposta]    DATETIME2      NOT NULL DEFAULT GETDATE(),

        CONSTRAINT [FK_SegnalazioniRisposte_Segnalazioni]
            FOREIGN KEY ([SegnalazioneId]) REFERENCES [Segnalazioni]([Id])
            ON DELETE CASCADE,

        CONSTRAINT [FK_SegnalazioniRisposte_Utenti]
            FOREIGN KEY ([UtenteId]) REFERENCES [Utenti]([Id])
    );

    CREATE INDEX [IX_SegnalazioniRisposte_SegnalazioneId]
        ON [Segnalazioni_Risposte] ([SegnalazioneId]);

    PRINT 'Tabella Segnalazioni_Risposte creata.';
END
ELSE
    PRINT 'Tabella Segnalazioni_Risposte gia'' presente.';
GO

-- ============================================================
-- Aggiorna versione in appsettings (manuale - non via SQL)
-- Ricorda di aggiornare AppSettings:Version a "4.0"
-- ============================================================
PRINT 'Migrazione v4.0 completata.';
