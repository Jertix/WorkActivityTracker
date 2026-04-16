-- =============================================================================
-- Migrazione v4.7 — Gestione Clienti avanzata
-- -----------------------------------------------------------------------------
-- Aggiunge le seguenti tabelle:
--   ClientiAmbienti      — dettagli per coppia (cliente, ambiente) condivisi
--                          fra tutti gli utenti: Application Server, Database
--                          Server, persone di riferimento, come collegarsi (HTML)
--   ClientiAmbienti_Log  — tracciabilità modifiche (Nuovo/Modifica/Elimina)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ClientiAmbienti'
)
BEGIN
    CREATE TABLE [ClientiAmbienti] (
        [Id]                  INT            IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ClienteId]           INT            NOT NULL,
        [Ambiente]            NVARCHAR(100)  NOT NULL,
        [ApplicationServer]   NVARCHAR(500)  NULL,
        [DatabaseServer]      NVARCHAR(500)  NULL,
        [PersoneRiferimento]  NVARCHAR(MAX)  NULL,
        [ComeCollegarsi]      NVARCHAR(MAX)  NULL,
        [DataModifica]        DATETIME2      NULL,
        CONSTRAINT FK_ClientiAmbienti_Cliente
            FOREIGN KEY (ClienteId) REFERENCES [Clienti](Id),
        CONSTRAINT UQ_ClientiAmbienti_Cliente_Ambiente
            UNIQUE (ClienteId, Ambiente)
    );

    CREATE INDEX IX_ClientiAmbienti_ClienteId ON ClientiAmbienti(ClienteId);

    PRINT 'Tabella ClientiAmbienti creata.';
END
ELSE
    PRINT 'Tabella ClientiAmbienti già presente.';
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ClientiAmbienti_Log'
)
BEGIN
    CREATE TABLE [ClientiAmbienti_Log] (
        [Id]             INT            IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [NomeUtente]     NVARCHAR(100)  NOT NULL,
        [AzioneSvolta]   NVARCHAR(20)   NOT NULL,  -- 'Nuovo', 'Modifica', 'Elimina'
        [ClienteId]      INT            NULL,
        [Ambiente]       NVARCHAR(100)  NULL,
        [VecchioValore]  NVARCHAR(MAX)  NULL,
        [NuovoValore]    NVARCHAR(MAX)  NULL,
        [Timestamp]      DATETIME2      NOT NULL DEFAULT GETDATE()
    );

    CREATE INDEX IX_ClientiAmbienti_Log_Timestamp
        ON ClientiAmbienti_Log(Timestamp DESC);

    PRINT 'Tabella ClientiAmbienti_Log creata.';
END
ELSE
    PRINT 'Tabella ClientiAmbienti_Log già presente.';
GO
