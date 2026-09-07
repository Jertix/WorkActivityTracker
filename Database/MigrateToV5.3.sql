-- Migrazione v5.3: data di scadenza del ticket
-- Aggiunge la colonna opzionale DataScadenzaTicket ad Attivita. Le righe esistenti restano NULL
-- (nessuna scadenza impostata): la griglia le mostra senza alcuna evidenziazione.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Attivita') AND name = 'DataScadenzaTicket'
)
BEGIN
    ALTER TABLE Attivita ADD DataScadenzaTicket DATETIME NULL;
END
GO

-- Indice per il filtro "Visualizza ticket in scadenza" (solo righe con scadenza valorizzata).
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Attivita_DataScadenzaTicket' AND object_id = OBJECT_ID('Attivita')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Attivita_DataScadenzaTicket
        ON Attivita (DataScadenzaTicket)
        WHERE DataScadenzaTicket IS NOT NULL;
END
GO

-- Nota di completamento dei TODO: obbligatoria lato applicazione quando il TODO viene
-- marcato completato. I TODO già completati restano NULL (storico precedente alla modifica).
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('TodoItems') AND name = 'NotaCompletamento'
)
BEGIN
    ALTER TABLE TodoItems ADD NotaCompletamento NVARCHAR(MAX) NULL;
END
