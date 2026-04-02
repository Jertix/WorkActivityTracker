-- =============================================
-- Script per creare il database WorkActivityTracker
-- Versione: 3.0
-- Modifiche v3.0:
--   - Aggiunta colonna CartellaDocumentazione
--   - Modificata colonna UrlTicket in NVARCHAR(MAX) per supporto multilinea
--   - Aggiornati Ambienti con VirtualXL3250-3270 e relative versioni
--   - Aumentata dimensione colonna Codice ambienti a 50 caratteri
-- =============================================

-- Crea il database
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'WorkActivityTracker')
BEGIN
    CREATE DATABASE WorkActivityTracker;
END
GO

USE WorkActivityTracker;
GO

-- =============================================
-- Tabelle di lookup
-- =============================================

-- Tabella Anni
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Anni]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Anni] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [Anno] INT NOT NULL UNIQUE
    );
    
    DECLARE @anno INT = 2020;
    WHILE @anno <= 2030
    BEGIN
        INSERT INTO [dbo].[Anni] ([Anno]) VALUES (@anno);
        SET @anno = @anno + 1;
    END
END
GO

-- Tabella Mesi
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Mesi]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Mesi] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [Numero] INT NOT NULL UNIQUE,
        [Nome] NVARCHAR(20) NOT NULL
    );
    
    INSERT INTO [dbo].[Mesi] ([Numero], [Nome]) VALUES 
        (1, 'Gennaio'), (2, 'Febbraio'), (3, 'Marzo'), (4, 'Aprile'),
        (5, 'Maggio'), (6, 'Giugno'), (7, 'Luglio'), (8, 'Agosto'),
        (9, 'Settembre'), (10, 'Ottobre'), (11, 'Novembre'), (12, 'Dicembre');
END
GO

-- Tabella Giorni
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Giorni]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Giorni] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [Giorno] INT NOT NULL UNIQUE
    );
    
    DECLARE @giorno INT = 1;
    WHILE @giorno <= 31
    BEGIN
        INSERT INTO [dbo].[Giorni] ([Giorno]) VALUES (@giorno);
        SET @giorno = @giorno + 1;
    END
END
GO

-- Tabella Clienti
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Clienti]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Clienti] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [Nome] NVARCHAR(100) NOT NULL UNIQUE,
        [Attivo] BIT NOT NULL DEFAULT 1
    );
    
    INSERT INTO [dbo].[Clienti] ([Nome]) VALUES 
        ('Cattolica'), ('Aviva'), ('Axa'), ('Generali'), ('Allianz'), ('UnipolSai');
END
GO

-- Tabella Ambienti (Congelati) - MODIFICATA v3.0
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Ambienti]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Ambienti] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [Codice] NVARCHAR(50) NOT NULL UNIQUE,  -- Nome congelato (es: VirtualXL3250)
        [Descrizione] NVARCHAR(100) NULL,       -- Versione (es: 3.2.50 - solo il numero)
        [DataCongelamento] DATETIME2 NULL,      -- Data di congelamento (v3.6)
        [Attivo] BIT NOT NULL DEFAULT 1
    );
    
    -- Inserisce i congelati in ordine DECRESCENTE (dalla versione più alta alla più bassa)
    -- per facilitare l'autocompletamento della versione di sviluppo
    -- Descrizione contiene SOLO il numero versione (es: 3.2.60, non "Versione 3.2.60")
    DECLARE @num INT = 3260;
    WHILE @num >= 3232
    BEGIN
        INSERT INTO [dbo].[Ambienti] ([Codice], [Descrizione]) 
        VALUES (
            'VirtualXL' + CAST(@num AS NVARCHAR(4)),
            '3.2.' + CAST(@num - 3200 AS NVARCHAR(2))
        );
        SET @num = @num - 1;
    END
    
    PRINT 'Inseriti 29 congelati (VirtualXL3260 - VirtualXL3232) in ordine decrescente';
END
ELSE
BEGIN
    -- La tabella esiste già - verifica se ha il nuovo formato
    IF EXISTS (SELECT 1 FROM [dbo].[Ambienti] WHERE [Codice] LIKE 'VirtualXL%')
    BEGIN
        PRINT 'Tabella Ambienti già esistente - verificare se necessario aggiornamento con MigrateToV3.3.sql';
    END
    ELSE IF EXISTS (SELECT 1 FROM [dbo].[Ambienti] WHERE [Codice] = '3250')
    BEGIN
        -- Vecchio formato rilevato
        PRINT '';
        PRINT '!!! ATTENZIONE !!!';
        PRINT 'Rilevato formato Ambienti v2.0 (3250, 3251, etc.)';
        PRINT 'Per aggiornare al nuovo formato esegui: MigrateToV3.3.sql';
        PRINT '';
    END
END
GO

-- Tabella Utenti (basata su Windows Login)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Utenti]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Utenti] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [WindowsUsername] NVARCHAR(100) NOT NULL UNIQUE,
        [NomeCompleto] NVARCHAR(200) NULL,
        [Email] NVARCHAR(200) NULL,
        [DataPrimoAccesso] DATETIME NOT NULL DEFAULT GETDATE(),
        [UltimoAccesso] DATETIME NOT NULL DEFAULT GETDATE(),
        [Attivo] BIT NOT NULL DEFAULT 1
    );
END
GO

-- =============================================
-- Tabella principale delle attività
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Attivita]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Attivita] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [UtenteId] INT NOT NULL,
        [Data] DATE NOT NULL,
        [Descrizione] NVARCHAR(500) NOT NULL,
        [UrlTicket] NVARCHAR(MAX) NULL,              -- Modificato in MAX per multilinea (v3.0)
        [ClienteId] INT NULL,
        [OreLavorate] DECIMAL(4,2) NOT NULL DEFAULT 0,
        [Versione] NVARCHAR(200) NULL,
        [Vedere] NVARCHAR(500) NULL,
        [Note] NVARCHAR(MAX) NULL,
        [ChangesetCoinvolti] NVARCHAR(MAX) NULL,      -- Aggiunto v2.0
        [UrlPatchRilasci] NVARCHAR(500) NULL,         -- Aggiunto v3.5
        [TestoCheckIn] NVARCHAR(500) NULL,            -- Aggiunto v3.3
        [CartellaDocumentazione] NVARCHAR(1000) NULL, -- Aggiunto v3.0
        [DataCreazione] DATETIME NOT NULL DEFAULT GETDATE(),
        [DataModifica] DATETIME NOT NULL DEFAULT GETDATE(),
        
        -- Foreign Keys
        CONSTRAINT [FK_Attivita_Utenti] FOREIGN KEY ([UtenteId]) REFERENCES [dbo].[Utenti]([Id]),
        CONSTRAINT [FK_Attivita_Clienti] FOREIGN KEY ([ClienteId]) REFERENCES [dbo].[Clienti]([Id])
    );
    
    -- Indici per performance
    CREATE INDEX [IX_Attivita_UtenteId] ON [dbo].[Attivita]([UtenteId]);
    CREATE INDEX [IX_Attivita_Data] ON [dbo].[Attivita]([Data]);
    CREATE INDEX [IX_Attivita_ClienteId] ON [dbo].[Attivita]([ClienteId]);
END
ELSE
BEGIN
    -- Aggiorna tabella esistente con nuove colonne
    
    -- Aggiungi ChangesetCoinvolti se mancante (v2.0)
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Attivita]') AND name = 'ChangesetCoinvolti')
    BEGIN
        ALTER TABLE [dbo].[Attivita] ADD [ChangesetCoinvolti] NVARCHAR(MAX) NULL;
        PRINT 'Colonna ChangesetCoinvolti aggiunta';
    END
    
    -- Aggiungi UrlPatchRilasci se mancante (v3.5)
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Attivita]') AND name = 'UrlPatchRilasci')
    BEGIN
        ALTER TABLE [dbo].[Attivita] ADD [UrlPatchRilasci] NVARCHAR(500) NULL;
        PRINT 'Colonna UrlPatchRilasci aggiunta';
    END
    
    -- Aggiungi TestoCheckIn se mancante (v3.3)
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Attivita]') AND name = 'TestoCheckIn')
    BEGIN
        ALTER TABLE [dbo].[Attivita] ADD [TestoCheckIn] NVARCHAR(500) NULL;
        PRINT 'Colonna TestoCheckIn aggiunta';
    END
    
    -- Aggiungi CartellaDocumentazione se mancante (v3.0)
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Attivita]') AND name = 'CartellaDocumentazione')
    BEGIN
        ALTER TABLE [dbo].[Attivita] ADD [CartellaDocumentazione] NVARCHAR(1000) NULL;
        PRINT 'Colonna CartellaDocumentazione aggiunta';
    END
    
    -- Modifica UrlTicket a NVARCHAR(MAX) se non lo è già (v3.0)
    DECLARE @currentType NVARCHAR(128);
    SELECT @currentType = DATA_TYPE + CASE WHEN CHARACTER_MAXIMUM_LENGTH IS NOT NULL AND CHARACTER_MAXIMUM_LENGTH <> -1 
                                           THEN '(' + CAST(CHARACTER_MAXIMUM_LENGTH AS NVARCHAR(10)) + ')' 
                                           ELSE '' END
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Attivita' AND COLUMN_NAME = 'UrlTicket';
    
    IF @currentType <> 'nvarchar' OR @currentType LIKE '%(%'
    BEGIN
        ALTER TABLE [dbo].[Attivita] ALTER COLUMN [UrlTicket] NVARCHAR(MAX) NULL;
        PRINT 'Colonna UrlTicket modificata a NVARCHAR(MAX)';
    END
END
GO

-- =============================================
-- Tabella ponte Attività-Ambienti (relazione N:N)
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AttivitaAmbienti]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AttivitaAmbienti] (
        [AttivitaId] INT NOT NULL,
        [AmbienteId] INT NOT NULL,
        
        PRIMARY KEY ([AttivitaId], [AmbienteId]),
        CONSTRAINT [FK_AttivitaAmbienti_Attivita] FOREIGN KEY ([AttivitaId]) REFERENCES [dbo].[Attivita]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AttivitaAmbienti_Ambienti] FOREIGN KEY ([AmbienteId]) REFERENCES [dbo].[Ambienti]([Id])
    );
END
GO

-- =============================================
-- Tabella TodoItems per gestione TODO List (v3.4)
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TodoItems]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[TodoItems] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [UtenteId] INT NOT NULL,
        [Titolo] NVARCHAR(200) NOT NULL DEFAULT '',       -- Titolo breve (v3.6)
        [DataInserimento] DATETIME2 NOT NULL DEFAULT GETDATE(),
        [Descrizione] NVARCHAR(MAX) NOT NULL,
        [DataCompletamento] DATETIME2 NULL,
        [LivelloUrgenza] INT NOT NULL DEFAULT 3,
        [Completato] BIT NOT NULL DEFAULT 0,
        
        CONSTRAINT [PK_TodoItems] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_TodoItems_Utenti] FOREIGN KEY ([UtenteId]) REFERENCES [dbo].[Utenti]([Id]),
        CONSTRAINT [CK_TodoItems_LivelloUrgenza] CHECK ([LivelloUrgenza] >= 1 AND [LivelloUrgenza] <= 5)
    );
    
    -- Indice per velocizzare le query per utente e stato
    CREATE NONCLUSTERED INDEX [IX_TodoItems_UtenteId_Completato] 
    ON [dbo].[TodoItems] ([UtenteId], [Completato]) 
    INCLUDE ([LivelloUrgenza], [DataInserimento]);
END
GO

-- =============================================
-- Tabella Ambienti_Log per tracciabilità modifiche (v3.6)
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Ambienti_Log]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Ambienti_Log] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [NomeUtente] NVARCHAR(100) NOT NULL,
        [AzioneSvolta] NVARCHAR(20) NOT NULL,  -- Nuovo, Modifica, Elimina
        [Codice] NVARCHAR(50) NULL,
        [Descrizione] NVARCHAR(100) NULL,
        [DataCongelamento] DATETIME2 NULL,
        [Timestamp] DATETIME2 NOT NULL DEFAULT GETDATE(),
        
        CONSTRAINT [PK_Ambienti_Log] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    
    -- Indice per velocizzare le query per timestamp
    CREATE NONCLUSTERED INDEX [IX_Ambienti_Log_Timestamp] 
    ON [dbo].[Ambienti_Log] ([Timestamp] DESC);
    
    PRINT 'Tabella Ambienti_Log creata per tracciabilità modifiche congelati';
END
GO

-- =============================================
-- Stored Procedures utili
-- =============================================

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_GetOrCreateUtente]') AND type in (N'P'))
    DROP PROCEDURE [dbo].[sp_GetOrCreateUtente];
GO

CREATE PROCEDURE [dbo].[sp_GetOrCreateUtente]
    @WindowsUsername NVARCHAR(100),
    @NomeCompleto NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @UtenteId INT;
    
    SELECT @UtenteId = Id FROM [dbo].[Utenti] WHERE [WindowsUsername] = @WindowsUsername;
    
    IF @UtenteId IS NULL
    BEGIN
        INSERT INTO [dbo].[Utenti] ([WindowsUsername], [NomeCompleto])
        VALUES (@WindowsUsername, @NomeCompleto);
        
        SET @UtenteId = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE [dbo].[Utenti] 
        SET [UltimoAccesso] = GETDATE(),
            [NomeCompleto] = COALESCE(@NomeCompleto, [NomeCompleto])
        WHERE [Id] = @UtenteId;
    END
    
    SELECT * FROM [dbo].[Utenti] WHERE [Id] = @UtenteId;
END
GO

-- =============================================
-- Vista per report attività (aggiornata v3.0)
-- =============================================

IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[dbo].[vw_AttivitaCompleta]'))
    DROP VIEW [dbo].[vw_AttivitaCompleta];
GO

CREATE VIEW [dbo].[vw_AttivitaCompleta]
AS
SELECT 
    a.[Id],
    a.[Data],
    a.[Descrizione],
    a.[UrlTicket],
    a.[OreLavorate],
    a.[Versione],
    a.[Vedere],
    a.[Note],
    a.[ChangesetCoinvolti],
    a.[CartellaDocumentazione],
    a.[DataCreazione],
    a.[DataModifica],
    u.[Id] AS UtenteId,
    u.[WindowsUsername],
    u.[NomeCompleto] AS UtenteNomeCompleto,
    c.[Id] AS ClienteId,
    c.[Nome] AS ClienteNome,
    YEAR(a.[Data]) AS Anno,
    MONTH(a.[Data]) AS Mese,
    DAY(a.[Data]) AS Giorno,
    STUFF((
        SELECT ', ' + amb.[Codice]
        FROM [dbo].[AttivitaAmbienti] aa
        INNER JOIN [dbo].[Ambienti] amb ON aa.[AmbienteId] = amb.[Id]
        WHERE aa.[AttivitaId] = a.[Id]
        FOR XML PATH('')
    ), 1, 2, '') AS CongelatiCoinvolti
FROM [dbo].[Attivita] a
INNER JOIN [dbo].[Utenti] u ON a.[UtenteId] = u.[Id]
LEFT JOIN [dbo].[Clienti] c ON a.[ClienteId] = c.[Id];
GO

PRINT '=========================================';
PRINT 'Database WorkActivityTracker v3.4';
PRINT 'Novità:';
PRINT '  - Campo CartellaDocumentazione aggiunto';
PRINT '  - Campo UrlTicket ora supporta multilinea';
PRINT '  - Congelati: VirtualXL3250 - VirtualXL3270';
PRINT '  - Tabella TodoItems per TODO List';
PRINT '=========================================';
GO
