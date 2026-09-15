-- =============================================================================
-- Pulizia una tantum di ClientiAmbienti_Log
-- =============================================================================
--
-- COSA FA
-- Fino alla v5.4 il log degli ambienti cliente scriveva il campo "Come collegarsi" PER INTERO
-- dentro VecchioValore e NuovoValore. Quel campo contiene HTML con gli screenshot incollati in
-- base64, quindi ogni salvataggio duplicava tutte le immagini nella tabella di log, due volte.
-- Dalla v5.5 il valore viene troncato a 200 caratteri; questo script applica lo stesso
-- troncamento alle righe GIÀ scritte, recuperando lo spazio.
--
-- COSA NON FA
-- Non tocca i dati veri: gli screenshot restano in ClientiAmbienti.ComeCollegarsi. Qui si
-- accorciano solo le copie finite nello storico delle modifiche.
--
-- PRIMA DI ESEGUIRE
-- - Fai un backup del database: l'operazione non è reversibile.
-- - Eseguilo a programma chiuso, o comunque quando nessuno sta salvando ambienti cliente.
-- - Lo script è ri-eseguibile: le righe già troncate non vengono toccate una seconda volta.
--
-- NOTA SULLO SPAZIO
-- Le pagine liberate tornano disponibili DENTRO il database e verranno riutilizzate dalle
-- scritture successive. Il file .mdf non si rimpicciolisce da solo: succede solo con uno SHRINK,
-- operazione che di norma è sconsigliata e qui non serve.
-- =============================================================================

SET NOCOUNT ON;

DECLARE @tag        NVARCHAR(20) = N'ComeCollegarsi: ';        -- 16 caratteri, spazio finale incluso
DECLARE @lenTag     INT          = 16;
DECLARE @fine       NVARCHAR(30) = N' | DirInstallazione: ';   -- campo successivo nella serializzazione
DECLARE @maxCaratteri INT        = 200;                        -- deve corrispondere a TroncaPerLog in C#
DECLARE @marcatore  NVARCHAR(30) = N'... [troncato]';         -- marca le righe già processate
DECLARE @lotto      INT          = 500;
DECLARE @righe      INT;

-- -----------------------------------------------------------------------------
-- 1) Situazione di partenza
-- -----------------------------------------------------------------------------
SELECT
    'PRIMA' AS Momento,
    COUNT(*) AS RigheLog,
    CAST(SUM(DATALENGTH(ISNULL(VecchioValore, N'')) + DATALENGTH(ISNULL(NuovoValore, N'')))
         / 1024.0 / 1024 AS DECIMAL(12,2)) AS Log_MB
FROM ClientiAmbienti_Log;

-- -----------------------------------------------------------------------------
-- 2) Troncamento di VecchioValore, a lotti
-- -----------------------------------------------------------------------------
WHILE 1 = 1
BEGIN
    UPDATE TOP (@lotto) ClientiAmbienti_Log
    SET VecchioValore =
            -- tutto fino a "ComeCollegarsi: " incluso
            SUBSTRING(VecchioValore, 1, CHARINDEX(@tag, VecchioValore) + @lenTag - 1)
            -- i primi @maxCaratteri del contenuto
          + SUBSTRING(VecchioValore, CHARINDEX(@tag, VecchioValore) + @lenTag, @maxCaratteri)
          + @marcatore
            -- da " | DirInstallazione: " in poi, invariato
          + SUBSTRING(VecchioValore,
                      CHARINDEX(@fine, VecchioValore, CHARINDEX(@tag, VecchioValore) + @lenTag),
                      2147483647)
    WHERE VecchioValore IS NOT NULL
      AND CHARINDEX(@tag, VecchioValore) > 0
      AND CHARINDEX(@fine, VecchioValore, CHARINDEX(@tag, VecchioValore) + @lenTag) > 0
      AND CHARINDEX(@fine, VecchioValore, CHARINDEX(@tag, VecchioValore) + @lenTag)
          - (CHARINDEX(@tag, VecchioValore) + @lenTag) > @maxCaratteri
      -- Esclude le righe già troncate: senza questo il ciclo non terminerebbe mai,
      -- perché dopo il troncamento il segmento misura comunque 214 caratteri (200 + marcatore).
      AND CHARINDEX(@marcatore + @fine, VecchioValore) = 0;

    SET @righe = @@ROWCOUNT;
    IF @righe = 0 BREAK;
    RAISERROR('VecchioValore: troncate %d righe', 0, 1, @righe) WITH NOWAIT;
END

-- -----------------------------------------------------------------------------
-- 3) Troncamento di NuovoValore, a lotti
-- -----------------------------------------------------------------------------
WHILE 1 = 1
BEGIN
    UPDATE TOP (@lotto) ClientiAmbienti_Log
    SET NuovoValore =
            SUBSTRING(NuovoValore, 1, CHARINDEX(@tag, NuovoValore) + @lenTag - 1)
          + SUBSTRING(NuovoValore, CHARINDEX(@tag, NuovoValore) + @lenTag, @maxCaratteri)
          + @marcatore
          + SUBSTRING(NuovoValore,
                      CHARINDEX(@fine, NuovoValore, CHARINDEX(@tag, NuovoValore) + @lenTag),
                      2147483647)
    WHERE NuovoValore IS NOT NULL
      AND CHARINDEX(@tag, NuovoValore) > 0
      AND CHARINDEX(@fine, NuovoValore, CHARINDEX(@tag, NuovoValore) + @lenTag) > 0
      AND CHARINDEX(@fine, NuovoValore, CHARINDEX(@tag, NuovoValore) + @lenTag)
          - (CHARINDEX(@tag, NuovoValore) + @lenTag) > @maxCaratteri
      -- Esclude le righe già troncate: senza questo il ciclo non terminerebbe mai,
      -- perché dopo il troncamento il segmento misura comunque 214 caratteri (200 + marcatore).
      AND CHARINDEX(@marcatore + @fine, NuovoValore) = 0;

    SET @righe = @@ROWCOUNT;
    IF @righe = 0 BREAK;
    RAISERROR('NuovoValore: troncate %d righe', 0, 1, @righe) WITH NOWAIT;
END

-- -----------------------------------------------------------------------------
-- 4) Situazione finale
-- -----------------------------------------------------------------------------
SELECT
    'DOPO' AS Momento,
    COUNT(*) AS RigheLog,
    CAST(SUM(DATALENGTH(ISNULL(VecchioValore, N'')) + DATALENGTH(ISNULL(NuovoValore, N'')))
         / 1024.0 / 1024 AS DECIMAL(12,2)) AS Log_MB
FROM ClientiAmbienti_Log;
