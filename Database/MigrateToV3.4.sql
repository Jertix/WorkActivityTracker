-- =============================================
-- Script di migrazione da v3.3 a v3.4
-- Aggiunge la tabella TodoItems per la gestione della TODO List
-- =============================================

USE [WorkActivityTracker]
GO

PRINT 'Inizio migrazione a v3.4...';
PRINT '';

-- =============================================
-- FASE 1: Creazione tabella TodoItems
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TodoItems]') AND type in (N'U'))
BEGIN
    PRINT 'Creazione tabella [TodoItems]...';
    
    CREATE TABLE [dbo].[TodoItems] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [UtenteId] INT NOT NULL,
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
    
    PRINT 'Tabella [TodoItems] creata con successo.';
END
ELSE
BEGIN
    PRINT 'Tabella [TodoItems] già esistente, skip creazione.';
END
GO

-- =============================================
-- FASE 2: Verifica finale
-- =============================================

PRINT '';
PRINT 'Verifica struttura tabella TodoItems:';

SELECT 
    c.name AS NomeColonna,
    t.name AS TipoDato,
    c.max_length AS Lunghezza,
    c.is_nullable AS Nullable,
    CASE WHEN dc.definition IS NOT NULL THEN dc.definition ELSE '-' END AS DefaultValue
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
LEFT JOIN sys.default_constraints dc ON c.default_object_id = dc.object_id
WHERE c.object_id = OBJECT_ID('dbo.TodoItems')
ORDER BY c.column_id;

PRINT '';
PRINT '=========================================';
PRINT 'Migrazione a v3.4 completata!';
PRINT 'Novità:';
PRINT '  - Tabella TodoItems per gestione TODO List';
PRINT '  - Livello urgenza da 1 (basso) a 5 (critico)';
PRINT '  - Supporto per TODO personali per utente';
PRINT '=========================================';
GO
