-- =============================================
-- Script di MIGRAZIONE da v2.0 a v3.0
-- Esegui PRIMA dello script CreateDatabase-v3.sql
-- =============================================

USE WorkActivityTracker;
GO

PRINT '=========================================';
PRINT 'Migrazione WorkActivityTracker v2.0 -> v3.0';
PRINT '=========================================';
PRINT '';

-- =============================================
-- STEP 1: Backup dei dati esistenti (opzionale ma consigliato)
-- =============================================
PRINT 'STEP 1: Creazione backup tabella AttivitaAmbienti...';

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AttivitaAmbienti_Backup]') AND type in (N'U'))
    DROP TABLE [dbo].[AttivitaAmbienti_Backup];

SELECT * INTO [dbo].[AttivitaAmbienti_Backup] FROM [dbo].[AttivitaAmbienti];
PRINT '  -> Backup creato in AttivitaAmbienti_Backup';
PRINT '';

-- =============================================
-- STEP 2: Eliminazione dati dalla tabella ponte
-- =============================================
PRINT 'STEP 2: Eliminazione collegamenti AttivitaAmbienti...';

DECLARE @countBefore INT = (SELECT COUNT(*) FROM [dbo].[AttivitaAmbienti]);
DELETE FROM [dbo].[AttivitaAmbienti];
PRINT '  -> Eliminati ' + CAST(@countBefore AS NVARCHAR(10)) + ' record da AttivitaAmbienti';
PRINT '';

-- =============================================
-- STEP 3: Eliminazione vecchi ambienti
-- =============================================
PRINT 'STEP 3: Eliminazione vecchi ambienti...';

DECLARE @countAmbienti INT = (SELECT COUNT(*) FROM [dbo].[Ambienti]);
DELETE FROM [dbo].[Ambienti];
PRINT '  -> Eliminati ' + CAST(@countAmbienti AS NVARCHAR(10)) + ' ambienti';

-- Reset identity
DBCC CHECKIDENT ('[dbo].[Ambienti]', RESEED, 0);
PRINT '  -> Identity resettata';
PRINT '';

-- =============================================
-- STEP 4: Aggiorna dimensione colonna Codice
-- =============================================
PRINT 'STEP 4: Aggiornamento struttura tabella Ambienti...';

ALTER TABLE [dbo].[Ambienti] ALTER COLUMN [Codice] NVARCHAR(50) NOT NULL;
PRINT '  -> Colonna Codice estesa a 50 caratteri';
PRINT '';

-- =============================================
-- STEP 5: Inserimento nuovi congelati
-- =============================================
PRINT 'STEP 5: Inserimento nuovi congelati (VirtualXL3250 - VirtualXL3270)...';

DECLARE @num INT = 3250;
WHILE @num <= 3270
BEGIN
    INSERT INTO [dbo].[Ambienti] ([Codice], [Descrizione]) 
    VALUES (
        'VirtualXL' + CAST(@num AS NVARCHAR(4)),
        'Versione 3.2.' + CAST(@num - 3200 AS NVARCHAR(2))
    );
    SET @num = @num + 1;
END

DECLARE @countNew INT = (SELECT COUNT(*) FROM [dbo].[Ambienti]);
PRINT '  -> Inseriti ' + CAST(@countNew AS NVARCHAR(10)) + ' nuovi congelati';
PRINT '';

-- =============================================
-- STEP 6: Aggiungi nuove colonne alla tabella Attivita
-- =============================================
PRINT 'STEP 6: Aggiornamento tabella Attivita...';

-- Aggiungi ChangesetCoinvolti se mancante
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Attivita]') AND name = 'ChangesetCoinvolti')
BEGIN
    ALTER TABLE [dbo].[Attivita] ADD [ChangesetCoinvolti] NVARCHAR(MAX) NULL;
    PRINT '  -> Colonna ChangesetCoinvolti aggiunta';
END
ELSE
    PRINT '  -> Colonna ChangesetCoinvolti già presente';

-- Aggiungi CartellaDocumentazione se mancante
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Attivita]') AND name = 'CartellaDocumentazione')
BEGIN
    ALTER TABLE [dbo].[Attivita] ADD [CartellaDocumentazione] NVARCHAR(1000) NULL;
    PRINT '  -> Colonna CartellaDocumentazione aggiunta';
END
ELSE
    PRINT '  -> Colonna CartellaDocumentazione già presente';

-- Modifica UrlTicket a NVARCHAR(MAX)
ALTER TABLE [dbo].[Attivita] ALTER COLUMN [UrlTicket] NVARCHAR(MAX) NULL;
PRINT '  -> Colonna UrlTicket modificata a NVARCHAR(MAX)';
PRINT '';

-- =============================================
-- STEP 7: Verifica finale
-- =============================================
PRINT 'STEP 7: Verifica finale...';
PRINT '';

SELECT 'Nuovi congelati disponibili:' AS Info;
SELECT Id, Codice AS Nome, Descrizione AS Versione FROM [dbo].[Ambienti] ORDER BY Id;

PRINT '';
PRINT '=========================================';
PRINT 'MIGRAZIONE COMPLETATA CON SUCCESSO!';
PRINT '';
PRINT 'NOTA: I collegamenti precedenti tra attività';
PRINT 'e ambienti sono stati eliminati.';
PRINT 'Backup disponibile in: AttivitaAmbienti_Backup';
PRINT '=========================================';
GO
