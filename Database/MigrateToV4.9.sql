-- ============================================================
-- Migration v4.9
-- 1) Ambienti: nuove colonne Descrizione4 e Descrizione5
--    (versioni aggiuntive oltre alla Descrizione principale)
-- 2) ClientiAmbienti: nuove colonne TipoVersione e NumeroVersione
--    TipoVersione    NVARCHAR(20)  NULL   ("Versione" | "Versione4" | "Versione5")
--    NumeroVersione  NVARCHAR(100) NULL   (numero della versione, libero)
-- ============================================================

-- === AMBIENTI ===
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Ambienti' AND COLUMN_NAME = 'Descrizione4'
)
BEGIN
    ALTER TABLE Ambienti
        ADD Descrizione4 NVARCHAR(100) NULL;
END

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Ambienti' AND COLUMN_NAME = 'Descrizione5'
)
BEGIN
    ALTER TABLE Ambienti
        ADD Descrizione5 NVARCHAR(100) NULL;
END

-- === CLIENTIAMBIENTI ===
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ClientiAmbienti' AND COLUMN_NAME = 'TipoVersione'
)
BEGIN
    ALTER TABLE ClientiAmbienti
        ADD TipoVersione NVARCHAR(20) NULL;
END

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ClientiAmbienti' AND COLUMN_NAME = 'NumeroVersione'
)
BEGIN
    ALTER TABLE ClientiAmbienti
        ADD NumeroVersione NVARCHAR(100) NULL;
END
