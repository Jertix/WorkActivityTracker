-- Migrazione v5.4: note libere sui congelati
-- Aggiunge la colonna opzionale Note ad Ambienti: testo libero per descrivere il Codice.
-- I record esistenti restano NULL.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Ambienti') AND name = 'Note'
)
BEGIN
    ALTER TABLE Ambienti ADD Note NVARCHAR(MAX) NULL;
END
