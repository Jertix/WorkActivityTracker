-- Migrazione v5.5: colonne di testo ricercabile per Note e ChangesetCoinvolti
--
-- Note e ChangesetCoinvolti contengono HTML con gli screenshot incollati in base64: una singola
-- riga può pesare centinaia di KB, e la ricerca (LIKE con wildcard iniziale) deve scandirli tutti.
-- Un indice normale non è applicabile: NVARCHAR(MAX) non può essere colonna chiave (errore 1919).
--
-- Queste due colonne contengono gli stessi campi ripuliti da tag e immagini, quindi pochi KB.
-- La ricerca gira su di esse. Restano NULL finché il backfill dell'applicazione non le popola
-- (ActivityService.AggiornaTestoRicercabileAsync, eseguito in background all'avvio): fino ad
-- allora la ricerca ricade automaticamente sulle colonne originali, quindi nessun risultato perso.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Attivita') AND name = 'NoteTesto'
)
BEGIN
    ALTER TABLE Attivita ADD NoteTesto NVARCHAR(MAX) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Attivita') AND name = 'ChangesetTesto'
)
BEGIN
    ALTER TABLE Attivita ADD ChangesetTesto NVARCHAR(MAX) NULL;
END
GO

-- Indice di supporto al backfill: individua rapidamente le righe ancora da convertire.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Attivita_NoteTesto_DaConvertire' AND object_id = OBJECT_ID('Attivita')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Attivita_NoteTesto_DaConvertire
        ON Attivita (Id)
        WHERE NoteTesto IS NULL;
END
