-- Migrazione v4.4
-- 1. Rimuove il limite di lunghezza dal campo UrlPatchRilasci (ora NVARCHAR(MAX) per supportare URL multipli lunghi)
-- 2. Aggiunge il campo DataDismissione alla tabella Ambienti

ALTER TABLE Attivita ALTER COLUMN UrlPatchRilasci NVARCHAR(MAX) NULL;

ALTER TABLE Ambienti ADD DataDismissione DATETIME NULL;
