-- =============================================
-- Script di migrazione da v3.5 a v3.6
-- Aggiunge:
-- 1. Colonna Titolo alla tabella TodoItems
-- 2. Colonna DataCongelamento alla tabella Ambienti
-- 3. Tabella Ambienti_Log per tracciabilità modifiche
-- =============================================

USE [WorkActivityTracker]
GO

PRINT 'Inizio migrazione a v3.6...';
PRINT '';

-- =============================================
-- FASE 1: Aggiunta colonna Titolo a TodoItems
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TodoItems]') AND name = 'Titolo')
BEGIN
    PRINT 'Aggiunta colonna [Titolo] alla tabella [TodoItems]...';
    
    ALTER TABLE [dbo].[TodoItems]
    ADD [Titolo] NVARCHAR(200) NOT NULL DEFAULT '';
    
    PRINT 'Colonna [Titolo] aggiunta con successo.';
END
ELSE
BEGIN
    PRINT 'Colonna [Titolo] già esistente in TodoItems, skip.';
END
GO

-- =============================================
-- FASE 2: Aggiunta colonna DataCongelamento a Ambienti
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Ambienti]') AND name = 'DataCongelamento')
BEGIN
    PRINT 'Aggiunta colonna [DataCongelamento] alla tabella [Ambienti]...';
    
    ALTER TABLE [dbo].[Ambienti]
    ADD [DataCongelamento] DATETIME2 NULL;
    
    PRINT 'Colonna [DataCongelamento] aggiunta con successo.';
END
ELSE
BEGIN
    PRINT 'Colonna [DataCongelamento] già esistente in Ambienti, skip.';
END
GO

-- =============================================
-- FASE 3: Creazione tabella Ambienti_Log
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Ambienti_Log]') AND type in (N'U'))
BEGIN
    PRINT 'Creazione tabella [Ambienti_Log]...';
    
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
    
    PRINT 'Tabella [Ambienti_Log] creata con successo.';
END
ELSE
BEGIN
    PRINT 'Tabella [Ambienti_Log] già esistente, skip creazione.';
END
GO

-- =============================================
-- FASE 4: Verifica finale
-- =============================================

PRINT '';
PRINT 'Verifica struttura tabella TodoItems:';

SELECT 
    c.name AS NomeColonna,
    t.name AS TipoDato,
    c.max_length AS Lunghezza,
    c.is_nullable AS Nullable
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.TodoItems')
AND c.name IN ('Titolo', 'DataInserimento')
ORDER BY c.column_id;

PRINT '';
PRINT 'Verifica struttura tabella Ambienti:';

SELECT 
    c.name AS NomeColonna,
    t.name AS TipoDato,
    c.max_length AS Lunghezza,
    c.is_nullable AS Nullable
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.Ambienti')
AND c.name = 'DataCongelamento';

PRINT '';
PRINT 'Verifica struttura tabella Ambienti_Log:';

SELECT 
    c.name AS NomeColonna,
    t.name AS TipoDato,
    c.max_length AS Lunghezza,
    c.is_nullable AS Nullable
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.Ambienti_Log')
ORDER BY c.column_id;

PRINT '';
PRINT '=========================================';
PRINT 'Migrazione a v3.6 completata!';
PRINT 'Novità:';
PRINT '  - Campo Titolo nella TODO List';
PRINT '  - Data Inserimento editabile nei TODO';
PRINT '  - Data Congelamento nella tabella Ambienti';
PRINT '  - Editor Congelati con logging delle modifiche';
PRINT '  - Tabella Ambienti_Log per tracciabilità';
PRINT '=========================================';
GO
