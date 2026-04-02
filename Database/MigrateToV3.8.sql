-- =============================================
-- Migration Script: v3.8 - TipoAttivita
-- Aggiunge il campo TipoAttivita alla tabella Attivita
-- Valori supportati: 'Lavoro' (default), 'Permesso', 'Ferie'
-- =============================================

USE WorkActivityTracker;
GO

-- =============================================
-- Aggiunta colonna TipoAttivita alla tabella Attivita
-- =============================================
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Attivita') AND name = 'TipoAttivita'
)
BEGIN
    ALTER TABLE Attivita
    ADD TipoAttivita NVARCHAR(20) NOT NULL DEFAULT 'Lavoro';

    PRINT 'Colonna TipoAttivita aggiunta alla tabella Attivita.';
END
ELSE
BEGIN
    PRINT 'Colonna TipoAttivita già presente nella tabella Attivita.';
END
GO

PRINT '';
PRINT '=== Migrazione v3.8 completata con successo! ===';
GO
