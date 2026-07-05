-- Migrazione v5.2: lingua struttura patch per cliente
-- Aggiunge la colonna Lingua a Clienti (IT/EN). Le righe esistenti (incluso "Sviluppo")
-- vengono valorizzate a 'IT' grazie al DEFAULT.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Clienti') AND name = 'Lingua'
)
BEGIN
    ALTER TABLE Clienti ADD Lingua NVARCHAR(2) NOT NULL DEFAULT 'IT';
END
