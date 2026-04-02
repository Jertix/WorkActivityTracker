-- =============================================
-- Script di migrazione v3.3
-- Aggiornamenti:
-- 1. Aggiunge colonna TestoCheckIn alla tabella Attivita
-- 2. Rimuove prefisso "Versione " dalla colonna Descrizione degli Ambienti
-- 3. Aggiunge cliente "Sviluppo" se non esiste
-- ESEGUIRE PRIMA DI USARE LA VERSIONE 3.3 DELL'APPLICAZIONE
-- =============================================

USE [WorkActivityTracker]
GO

PRINT '=== Inizio migrazione a v3.3 ==='
PRINT ''

-- =============================================
-- FASE 1: Aggiunge colonna TestoCheckIn
-- =============================================
PRINT 'FASE 1: Aggiunta colonna TestoCheckIn...'

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Attivita]') AND name = 'TestoCheckIn')
BEGIN
    ALTER TABLE [dbo].[Attivita] ADD [TestoCheckIn] NVARCHAR(500) NULL;
    PRINT 'Colonna TestoCheckIn aggiunta con successo';
END
ELSE
BEGIN
    PRINT 'Colonna TestoCheckIn già esistente - nessuna modifica';
END
PRINT ''

-- =============================================
-- FASE 2: Rimuove prefisso "Versione " dalla Descrizione degli Ambienti
-- =============================================
PRINT 'FASE 2: Rimozione prefisso "Versione " dalla colonna Descrizione...'

UPDATE [dbo].[Ambienti]
SET [Descrizione] = REPLACE([Descrizione], 'Versione ', '')
WHERE [Descrizione] LIKE 'Versione %';

DECLARE @rowsAffected INT = @@ROWCOUNT;
PRINT 'Aggiornate ' + CAST(@rowsAffected AS NVARCHAR(10)) + ' righe';
PRINT ''

-- =============================================
-- FASE 3: Aggiunge cliente "Sviluppo" se non esiste
-- =============================================
PRINT 'FASE 3: Verifica cliente "Sviluppo"...'

IF NOT EXISTS (SELECT 1 FROM [dbo].[Clienti] WHERE [Nome] = 'Sviluppo')
BEGIN
    INSERT INTO [dbo].[Clienti] ([Nome], [Attivo]) VALUES ('Sviluppo', 1);
    PRINT 'Cliente "Sviluppo" aggiunto con successo';
END
ELSE
BEGIN
    PRINT 'Cliente "Sviluppo" già esistente - nessuna modifica';
END
PRINT ''

-- =============================================
-- VERIFICA FINALE
-- =============================================
PRINT '=== VERIFICA FINALE ==='
PRINT ''

-- Mostra le prime 5 righe degli Ambienti per verificare il formato
PRINT 'Primi 5 Ambienti (dopo migrazione):'
SELECT TOP 5 [Id], [Codice], [Descrizione] FROM [dbo].[Ambienti] ORDER BY [Codice] DESC;

-- Verifica colonna TestoCheckIn
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Attivita]') AND name = 'TestoCheckIn')
    PRINT 'OK: Colonna TestoCheckIn presente'
ELSE
    PRINT 'ERRORE: Colonna TestoCheckIn mancante!'

-- Verifica cliente Sviluppo
IF EXISTS (SELECT 1 FROM [dbo].[Clienti] WHERE [Nome] = 'Sviluppo')
    PRINT 'OK: Cliente "Sviluppo" presente'
ELSE
    PRINT 'ERRORE: Cliente "Sviluppo" mancante!'

PRINT ''
PRINT '=== Migrazione a v3.3 completata con successo ==='
GO
