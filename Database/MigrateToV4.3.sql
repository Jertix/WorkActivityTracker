-- Migrazione WorkActivityTracker v4.2 → v4.3
-- Aggiunge il campo NumeroTicket alla tabella Attivita

ALTER TABLE Attivita ADD NumeroTicket NVARCHAR(200) NULL;
