-- =============================================
-- Script di migrazione da v3.4 a v3.5
-- Aggiunge il campo UrlPatchRilasci alla tabella Attivita
-- =============================================

USE [WorkActivityTracker]
GO

PRINT 'Inizio migrazione a v3.5...';
PRINT '';

-- =============================================
-- FASE 1: Aggiunta colonna UrlPatchRilasci
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Attivita]') AND name = 'UrlPatchRilasci')
BEGIN
    PRINT 'Aggiunta colonna [UrlPatchRilasci] alla tabella [Attivita]...';
    
    ALTER TABLE [dbo].[Attivita]
    ADD [UrlPatchRilasci] NVARCHAR(500) NULL;
    
    PRINT 'Colonna [UrlPatchRilasci] aggiunta con successo.';
END
ELSE
BEGIN
    PRINT 'Colonna [UrlPatchRilasci] già esistente, skip.';
END
GO

-- =============================================
-- FASE 2: Verifica finale
-- =============================================

PRINT '';
PRINT 'Verifica struttura tabella Attivita (nuova colonna):';

SELECT 
    c.name AS NomeColonna,
    t.name AS TipoDato,
    c.max_length AS Lunghezza,
    c.is_nullable AS Nullable
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.Attivita')
AND c.name = 'UrlPatchRilasci';

PRINT '';
PRINT '=========================================';
PRINT 'Migrazione a v3.5 completata!';
PRINT 'Novità:';
PRINT '  - Campo "URL Patch/Pacchetto nei rilasci" (max 500 caratteri)';
PRINT '  - Ricercabile nella finestra di ricerca';
PRINT '  - Visibile nel dettaglio elemento selezionato';
PRINT '=========================================';
GO
