-- ============================================================
-- Migrazione v5.7 — WorkActivityTracker
--   Testo degli screenshot ricercabile (OCR)
--   1. Attivita.NoteOcr / Attivita.ChangesetOcr: testo letto dagli screenshot incollati
--      nella Descrizione dettagliata e nei Changeset
--   2. Attivita.OcrImpronta: impronta (SHA-256) degli screenshot letti.
--      NULL = da leggere, stringa vuota = nessuno screenshot
--   3. Indice filtrato per trovare in fretta le attività ancora da leggere
--   4. Le attività SENZA screenshot vengono segnate subito come lette: così l'applicazione
--      non deve scaricarne l'HTML. Quelle con screenshot restano NULL e vengono lette
--      dall'applicazione in background, dalla più recente alla più vecchia.
--   5. Gestione Clienti (ClientiAmbienti, dati condivisi fra gli utenti):
--      ComeCollegarsiTesto / DatiAmbienteTesto = i due campi HTML ripuliti da tag e
--      screenshot (per la casella di ricerca della griglia); ComeCollegarsiOcr /
--      DatiAmbienteOcr = testo letto negli screenshot; OcrImpronta come per le attività.
--   6. Ambienti con entrambi i campi vuoti: segnati subito come elaborati. Gli altri li
--      elabora l'applicazione in background una volta sola (testo ripulito + OCR: il testo
--      ripulito non si può calcolare in SQL), da qualsiasi postazione.
--
-- Va eseguita DOPO MigrateToV5.6.sql.
-- Lo script è idempotente: si può rieseguire senza danni.
--
-- Per far rileggere tutti gli screenshot (es. dopo un cambio della regola di lettura):
--   UPDATE Attivita SET OcrImpronta = NULL WHERE OcrImpronta <> '';
--   UPDATE ClientiAmbienti SET OcrImpronta = NULL WHERE OcrImpronta <> '';
-- Per far ricalcolare anche il testo ripulito di Gestione Clienti (es. ambienti modificati
-- da una postazione con la versione precedente, che non aggiorna queste colonne):
--   UPDATE ClientiAmbienti SET OcrImpronta = NULL;
-- ============================================================

-- Necessario per creare indici su Attivita, che ha già indici filtrati (v5.5).
-- SSMS lo imposta ON di default, sqlcmd no (senza -I).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ============================================================
-- 1-2. Colonne
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Attivita') AND name = 'NoteOcr')
BEGIN
    ALTER TABLE Attivita ADD NoteOcr NVARCHAR(MAX) NULL;
    PRINT 'Colonna Attivita.NoteOcr aggiunta.';
END
ELSE
    PRINT 'Colonna Attivita.NoteOcr già esistente, skip.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Attivita') AND name = 'ChangesetOcr')
BEGIN
    ALTER TABLE Attivita ADD ChangesetOcr NVARCHAR(MAX) NULL;
    PRINT 'Colonna Attivita.ChangesetOcr aggiunta.';
END
ELSE
    PRINT 'Colonna Attivita.ChangesetOcr già esistente, skip.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Attivita') AND name = 'OcrImpronta')
BEGIN
    ALTER TABLE Attivita ADD OcrImpronta VARCHAR(64) NULL;
    PRINT 'Colonna Attivita.OcrImpronta aggiunta.';
END
ELSE
    PRINT 'Colonna Attivita.OcrImpronta già esistente, skip.';
GO

-- ============================================================
-- 3. Indice delle attività da leggere (per utente: ogni postazione legge le proprie)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Attivita_OcrDaLeggere' AND object_id = OBJECT_ID('Attivita'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Attivita_OcrDaLeggere
        ON Attivita (UtenteId, Id)
        WHERE OcrImpronta IS NULL;
    PRINT 'Indice IX_Attivita_OcrDaLeggere creato.';
END
ELSE
    PRINT 'Indice IX_Attivita_OcrDaLeggere già esistente, skip.';
GO

-- ============================================================
-- 4. Attività senza screenshot: segnate subito come lette
-- ============================================================
UPDATE Attivita
SET OcrImpronta = '', NoteOcr = '', ChangesetOcr = ''
WHERE OcrImpronta IS NULL
  AND (Note IS NULL OR Note NOT LIKE '%<img%')
  AND (ChangesetCoinvolti IS NULL OR ChangesetCoinvolti NOT LIKE '%<img%');
PRINT 'Attività senza screenshot segnate come lette: ' + CAST(@@ROWCOUNT AS NVARCHAR(20));

DECLARE @DaLeggere INT = (SELECT COUNT(*) FROM Attivita WHERE OcrImpronta IS NULL);
PRINT 'Attività con screenshot da leggere (le legge l''applicazione in background): ' + CAST(@DaLeggere AS NVARCHAR(20));
GO

-- ============================================================
-- 5. Gestione Clienti: colonne di testo ricercabile e OCR
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ClientiAmbienti') AND name = 'ComeCollegarsiTesto')
BEGIN
    ALTER TABLE ClientiAmbienti ADD ComeCollegarsiTesto NVARCHAR(MAX) NULL;
    PRINT 'Colonna ClientiAmbienti.ComeCollegarsiTesto aggiunta.';
END
ELSE
    PRINT 'Colonna ClientiAmbienti.ComeCollegarsiTesto già esistente, skip.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ClientiAmbienti') AND name = 'DatiAmbienteTesto')
BEGIN
    ALTER TABLE ClientiAmbienti ADD DatiAmbienteTesto NVARCHAR(MAX) NULL;
    PRINT 'Colonna ClientiAmbienti.DatiAmbienteTesto aggiunta.';
END
ELSE
    PRINT 'Colonna ClientiAmbienti.DatiAmbienteTesto già esistente, skip.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ClientiAmbienti') AND name = 'ComeCollegarsiOcr')
BEGIN
    ALTER TABLE ClientiAmbienti ADD ComeCollegarsiOcr NVARCHAR(MAX) NULL;
    PRINT 'Colonna ClientiAmbienti.ComeCollegarsiOcr aggiunta.';
END
ELSE
    PRINT 'Colonna ClientiAmbienti.ComeCollegarsiOcr già esistente, skip.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ClientiAmbienti') AND name = 'DatiAmbienteOcr')
BEGIN
    ALTER TABLE ClientiAmbienti ADD DatiAmbienteOcr NVARCHAR(MAX) NULL;
    PRINT 'Colonna ClientiAmbienti.DatiAmbienteOcr aggiunta.';
END
ELSE
    PRINT 'Colonna ClientiAmbienti.DatiAmbienteOcr già esistente, skip.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ClientiAmbienti') AND name = 'OcrImpronta')
BEGIN
    ALTER TABLE ClientiAmbienti ADD OcrImpronta VARCHAR(64) NULL;
    PRINT 'Colonna ClientiAmbienti.OcrImpronta aggiunta.';
END
ELSE
    PRINT 'Colonna ClientiAmbienti.OcrImpronta già esistente, skip.';
GO

-- ============================================================
-- 6. Ambienti senza contenuto nei due campi: segnati subito come elaborati
--    (nessun indice: la tabella è piccola, una riga per coppia cliente/ambiente)
-- ============================================================
UPDATE ClientiAmbienti
SET OcrImpronta = '', ComeCollegarsiOcr = '', DatiAmbienteOcr = '',
    ComeCollegarsiTesto = '', DatiAmbienteTesto = ''
WHERE OcrImpronta IS NULL
  AND ISNULL(ComeCollegarsi, '') = ''
  AND ISNULL(DatiAmbiente, '') = '';
PRINT 'Ambienti cliente senza contenuto segnati come elaborati: ' + CAST(@@ROWCOUNT AS NVARCHAR(20));

DECLARE @AmbientiDaElaborare INT = (SELECT COUNT(*) FROM ClientiAmbienti WHERE OcrImpronta IS NULL);
PRINT 'Ambienti cliente da elaborare (testo e screenshot, li elabora l''applicazione in background): ' + CAST(@AmbientiDaElaborare AS NVARCHAR(20));
GO
