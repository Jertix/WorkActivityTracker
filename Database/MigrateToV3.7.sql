-- =============================================
-- Migration Script: v3.7 - Appunti (Knowledge Base)
-- Nuova funzionalità per salvare note/appunti utili con sistema di tag
-- =============================================

USE WorkActivityTracker;
GO

-- =============================================
-- Tabella Appunti (Knowledge Base personale)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Appunti')
BEGIN
    CREATE TABLE Appunti (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        UtenteId INT NOT NULL,
        Titolo NVARCHAR(300) NOT NULL,
        Descrizione NVARCHAR(MAX) NOT NULL,
        DataCreazione DATETIME2 NOT NULL DEFAULT GETDATE(),
        DataModifica DATETIME2 NOT NULL DEFAULT GETDATE(),
        
        CONSTRAINT FK_Appunti_Utenti FOREIGN KEY (UtenteId) 
            REFERENCES Utenti(Id)
    );

    -- Indice per ricerche per utente
    CREATE NONCLUSTERED INDEX IX_Appunti_UtenteId 
        ON Appunti(UtenteId);

    -- Indice per ordinamento per data modifica
    CREATE NONCLUSTERED INDEX IX_Appunti_DataModifica 
        ON Appunti(DataModifica DESC);

    PRINT 'Tabella Appunti creata con successo.';
END
ELSE
BEGIN
    PRINT 'Tabella Appunti già esistente.';
END
GO

-- =============================================
-- Tabella AppuntiTags (Tag per gli appunti)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AppuntiTags')
BEGIN
    CREATE TABLE AppuntiTags (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        AppuntoId INT NOT NULL,
        NomeTag NVARCHAR(100) NOT NULL,
        
        CONSTRAINT FK_AppuntiTags_Appunti FOREIGN KEY (AppuntoId) 
            REFERENCES Appunti(Id) ON DELETE CASCADE
    );

    -- Indice per ricerche per appunto
    CREATE NONCLUSTERED INDEX IX_AppuntiTags_AppuntoId 
        ON AppuntiTags(AppuntoId);

    -- Indice per ricerche per tag
    CREATE NONCLUSTERED INDEX IX_AppuntiTags_NomeTag 
        ON AppuntiTags(NomeTag);

    PRINT 'Tabella AppuntiTags creata con successo.';
END
ELSE
BEGIN
    PRINT 'Tabella AppuntiTags già esistente.';
END
GO

PRINT '';
PRINT '=== Migrazione v3.7 completata con successo! ===';
GO
