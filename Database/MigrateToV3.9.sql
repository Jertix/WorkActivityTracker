-- ============================================================
-- MigrateToV3.9.sql
-- Aggiunge:
--   1. Colonna PrivacyMode (BIT) alla tabella Utenti
--   2. Colonne VecchioValore e NuovoValore (NVARCHAR(MAX)) alla tabella Ambienti_Log
-- ============================================================

-- 1. Aggiunge PrivacyMode alla tabella Utenti
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Utenti' AND COLUMN_NAME = 'PrivacyMode'
)
BEGIN
    ALTER TABLE Utenti
    ADD PrivacyMode BIT NOT NULL DEFAULT 0;

    PRINT 'Colonna PrivacyMode aggiunta alla tabella Utenti (default: 0 = disabilitata).';
END
ELSE
BEGIN
    PRINT 'Colonna PrivacyMode già presente in Utenti. Skip.';
END

-- 2. Aggiunge VecchioValore alla tabella Ambienti_Log
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Ambienti_Log' AND COLUMN_NAME = 'VecchioValore'
)
BEGIN
    ALTER TABLE Ambienti_Log
    ADD VecchioValore NVARCHAR(MAX) NULL;

    PRINT 'Colonna VecchioValore aggiunta alla tabella Ambienti_Log.';
END
ELSE
BEGIN
    PRINT 'Colonna VecchioValore già presente in Ambienti_Log. Skip.';
END

-- 3. Aggiunge NuovoValore alla tabella Ambienti_Log
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Ambienti_Log' AND COLUMN_NAME = 'NuovoValore'
)
BEGIN
    ALTER TABLE Ambienti_Log
    ADD NuovoValore NVARCHAR(MAX) NULL;

    PRINT 'Colonna NuovoValore aggiunta alla tabella Ambienti_Log.';
END
ELSE
BEGIN
    PRINT 'Colonna NuovoValore già presente in Ambienti_Log. Skip.';
END

PRINT 'Migrazione V3.9 completata.';
