-- Migrazione v4.5
-- 1. Aggiunge la tabella EditorHistory per lo storico versioni dei campi Note e ChangesetCoinvolti
--    Ogni salvataggio di un'attività produce una riga per ciascuno dei due campi.

CREATE TABLE EditorHistory (
    Id               INT           IDENTITY(1,1) NOT NULL,
    AttivitaId       INT           NOT NULL,
    UtenteId         INT           NOT NULL,
    Campo            NVARCHAR(20)  NOT NULL,   -- 'Note' o 'ChangesetCoinvolti'
    Contenuto        NVARCHAR(MAX) NULL,
    DataSalvataggio  DATETIME      NOT NULL    DEFAULT GETDATE(),
    CONSTRAINT PK_EditorHistory PRIMARY KEY (Id)
);

CREATE INDEX IX_EditorHistory_AttivitaId_Campo
    ON EditorHistory (AttivitaId, Campo);
