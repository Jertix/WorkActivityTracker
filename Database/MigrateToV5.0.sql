-- =============================================
-- MigrateToV5.0.sql
-- Indici per le performance di ricerca/caricamento griglia e di salvataggio.
--
-- Contesto: la griglia non carica piu i campi pesanti Note/ChangesetCoinvolti
-- (NVARCHAR(MAX) con screenshot base64). Questi indici velocizzano il filtro per
-- utente + ordinamento per data, il recupero dell'ultima versione editor a ogni
-- salvataggio e la query batch della colonna "Ambienti".
--
-- Idempotente: puo essere eseguito piu volte senza errori.
-- =============================================

USE WorkActivityTracker;
GO

-- ---------------------------------------------------------------------------
-- 1) Attivita: indice composto (UtenteId, Data DESC)
--    Ottimizza la query principale della griglia: filtro per utente +
--    ordinamento per data decrescente. Reso possibile dal filtro data SARGable
--    (intervallo [inizio, fine) invece di YEAR()/MONTH()).
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Attivita_UtenteId_Data'
                 AND object_id = OBJECT_ID(N'[dbo].[Attivita]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Attivita_UtenteId_Data]
        ON [dbo].[Attivita] ([UtenteId] ASC, [Data] DESC);
    PRINT 'Creato indice IX_Attivita_UtenteId_Data su Attivita(UtenteId, Data DESC)';
END
ELSE
    PRINT 'Indice IX_Attivita_UtenteId_Data gia presente';
GO

-- ---------------------------------------------------------------------------
-- 2) EditorHistory: indice (AttivitaId, Campo, DataSalvataggio DESC)
--    Velocizza GetUltimoContenutoAsync, chiamato a OGNI salvataggio per
--    confrontare l'ultima versione (oggi richiede uno scan della tabella, che
--    cresce di alcuni MB a ogni salvataggio con screenshot).
-- ---------------------------------------------------------------------------
IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EditorHistory]') AND type = 'U')
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = 'IX_EditorHistory_Attivita_Campo'
                     AND object_id = OBJECT_ID(N'[dbo].[EditorHistory]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_EditorHistory_Attivita_Campo]
        ON [dbo].[EditorHistory] ([AttivitaId] ASC, [Campo] ASC, [DataSalvataggio] DESC);
    PRINT 'Creato indice IX_EditorHistory_Attivita_Campo';
END
ELSE
    PRINT 'Indice IX_EditorHistory_Attivita_Campo gia presente (o tabella assente)';
GO

-- ---------------------------------------------------------------------------
-- 3) AttivitaAmbientiRilascio: indice su AttivitaId (PK e su Id)
--    La colonna "Ambienti" della griglia fa una query batch
--    WHERE AttivitaId IN (...) AND TipoAmbiente IS NOT NULL ORDER BY AttivitaId, Posizione.
-- ---------------------------------------------------------------------------
IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AttivitaAmbientiRilascio]') AND type = 'U')
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = 'IX_AttivitaAmbientiRilascio_AttivitaId'
                     AND object_id = OBJECT_ID(N'[dbo].[AttivitaAmbientiRilascio]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AttivitaAmbientiRilascio_AttivitaId]
        ON [dbo].[AttivitaAmbientiRilascio] ([AttivitaId] ASC)
        INCLUDE ([Posizione], [TipoAmbiente]);
    PRINT 'Creato indice IX_AttivitaAmbientiRilascio_AttivitaId';
END
ELSE
    PRINT 'Indice IX_AttivitaAmbientiRilascio_AttivitaId gia presente (o tabella assente)';
GO

PRINT '=========================================';
PRINT 'Migrazione v5.0 completata: indici performance creati';
PRINT '=========================================';
GO
