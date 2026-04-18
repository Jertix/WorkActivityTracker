-- ============================================================
-- Migration v4.8
-- Nuovi campi in ClientiAmbienti:
--   DatiAmbiente          NVARCHAR(MAX)   NULL  (HTML, dati recuperati dall'ambiente)
--   DirectoryInstallazione NVARCHAR(MAX)  NULL  (path installazione XLayers)
--   InformazioniPool      NVARCHAR(1000)  NULL  (info pool 32/64bit, ecc.)
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ClientiAmbienti' AND COLUMN_NAME = 'DatiAmbiente'
)
BEGIN
    ALTER TABLE ClientiAmbienti
        ADD DatiAmbiente NVARCHAR(MAX) NULL;
END

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ClientiAmbienti' AND COLUMN_NAME = 'DirectoryInstallazione'
)
BEGIN
    ALTER TABLE ClientiAmbienti
        ADD DirectoryInstallazione NVARCHAR(MAX) NULL;
END

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ClientiAmbienti' AND COLUMN_NAME = 'InformazioniPool'
)
BEGIN
    ALTER TABLE ClientiAmbienti
        ADD InformazioniPool NVARCHAR(1000) NULL;
END
