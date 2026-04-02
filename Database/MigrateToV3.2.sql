-- =============================================
-- Script di migrazione v3.2
-- Aggiorna i congelati al nuovo range (3.2.60 - 3.2.32)
-- ESEGUIRE PRIMA DI USARE LA VERSIONE 3.2 DELL'APPLICAZIONE
-- =============================================

USE [WorkActivityTracker]
GO

PRINT '=== Inizio migrazione a v3.2 ==='
PRINT ''

-- =============================================
-- FASE 1: Backup delle associazioni esistenti
-- =============================================
PRINT 'FASE 1: Backup associazioni attività-ambienti...'

IF OBJECT_ID('tempdb..#AttivitaAmbienti_Backup_v32') IS NOT NULL
    DROP TABLE #AttivitaAmbienti_Backup_v32;

-- Crea una tabella temporanea con le associazioni
SELECT 
    aa.AttivitaId,
    a.Codice AS VecchioCodice
INTO #AttivitaAmbienti_Backup_v32
FROM [dbo].[AttivitaAmbienti] aa
INNER JOIN [dbo].[Ambienti] a ON aa.AmbienteId = a.Id;

DECLARE @backupCount INT = (SELECT COUNT(*) FROM #AttivitaAmbienti_Backup_v32);
PRINT 'Backup completato: ' + CAST(@backupCount AS NVARCHAR(10)) + ' associazioni salvate';
PRINT ''

-- =============================================
-- FASE 2: Elimina i collegamenti esistenti
-- =============================================
PRINT 'FASE 2: Rimozione collegamenti esistenti...'

DELETE FROM [dbo].[AttivitaAmbienti];
PRINT 'Collegamenti rimossi';
PRINT ''

-- =============================================
-- FASE 3: Elimina i vecchi ambienti
-- =============================================
PRINT 'FASE 3: Rimozione vecchi ambienti...'

DELETE FROM [dbo].[Ambienti];
PRINT 'Ambienti rimossi';

-- Reset identity
DBCC CHECKIDENT ('[dbo].[Ambienti]', RESEED, 0);
PRINT 'Identity reset completato';
PRINT ''

-- =============================================
-- FASE 4: Inserimento nuovi congelati (3.2.60 - 3.2.32)
-- Inseriti in ORDINE DECRESCENTE per l'autocompletamento versione
-- =============================================
PRINT 'FASE 4: Inserimento nuovi congelati (VirtualXL3260 - VirtualXL3232)...'

DECLARE @num INT = 3260;
WHILE @num >= 3232
BEGIN
    INSERT INTO [dbo].[Ambienti] ([Codice], [Descrizione]) 
    VALUES (
        'VirtualXL' + CAST(@num AS NVARCHAR(4)),
        'Versione 3.2.' + CAST(@num - 3200 AS NVARCHAR(2))
    );
    SET @num = @num - 1;
END

DECLARE @newCount INT = (SELECT COUNT(*) FROM [dbo].[Ambienti]);
PRINT 'Inseriti ' + CAST(@newCount AS NVARCHAR(10)) + ' nuovi congelati';
PRINT ''

-- =============================================
-- FASE 5: Ripristino associazioni (se possibile)
-- Nota: le associazioni vengono ripristinate solo se 
-- il codice ambiente esiste ancora nel nuovo set
-- =============================================
PRINT 'FASE 5: Ripristino associazioni compatibili...'

INSERT INTO [dbo].[AttivitaAmbienti] (AttivitaId, AmbienteId)
SELECT 
    b.AttivitaId,
    a.Id
FROM #AttivitaAmbienti_Backup_v32 b
INNER JOIN [dbo].[Ambienti] a ON a.Codice = b.VecchioCodice;

DECLARE @restoredCount INT = @@ROWCOUNT;
PRINT 'Ripristinate ' + CAST(@restoredCount AS NVARCHAR(10)) + ' associazioni';

IF @backupCount > @restoredCount
BEGIN
    PRINT ''
    PRINT '!!! ATTENZIONE !!!'
    PRINT 'Alcune associazioni non sono state ripristinate perché'
    PRINT 'i relativi ambienti non esistono più nel nuovo set.'
    PRINT 'Associazioni perse: ' + CAST(@backupCount - @restoredCount AS NVARCHAR(10))
END
PRINT ''

-- =============================================
-- FASE 6: Pulizia
-- =============================================
DROP TABLE #AttivitaAmbienti_Backup_v32;
PRINT 'Pulizia completata'
PRINT ''

-- =============================================
-- VERIFICA FINALE
-- =============================================
PRINT '=== VERIFICA FINALE ==='
PRINT ''
PRINT 'Nuovi congelati inseriti:'

SELECT [Id], [Codice], [Descrizione]
FROM [dbo].[Ambienti]
ORDER BY [Id];

PRINT ''
PRINT '=== Migrazione a v3.2 completata con successo ==='
GO
