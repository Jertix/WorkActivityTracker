-- ============================================================
-- Dati dimostrativi — WorkActivityTracker
--
-- Riempie un database APPENA CREATO con dati inventati ma realistici: utenti, clienti,
-- congelati, software, circa due mesi e mezzo di attività (con catene "Proseguo l'attività",
-- TODO aperti, ticket in scadenza, changeset, ambienti di rilascio, screenshot), TODO List,
-- appunti, segnalazioni, calendario e Gestione Clienti. Serve per provare il programma e
-- per gli screenshot del README. Nomi, aziende, server e indirizzi sono inventati.
--
-- Come usarlo:
--   1. creare un database vuoto (es. WorkActivityTracker_Demo) con CreateDatabase.sql e
--      tutte le migration MigrateToVX.Y.sql, cambiando in testa agli script il nome del
--      database (USE / CREATE DATABASE);
--   2. eseguire questo script su quel database;
--   3. puntare l'applicazione al database demo (ConnectionStrings in appsettings.json) e,
--      per coerenza con i dati, impostare "NomeProgramma": "Orbita".
--
-- L'utente principale ("Mario Rossi") è l'utente Windows di chi esegue lo script, così
-- avviando il programma le sue attività sono quelle del form. Con l'autenticazione SQL
-- impostare a mano @UtentePrincipale con il nome utente Windows (senza dominio).
--
-- Le date sono calcolate rispetto al giorno di esecuzione: i dati sembrano sempre recenti.
-- Per sicurezza lo script si ferma se nel database ci sono già attività.
-- ============================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DECLARE @UtentePrincipale NVARCHAR(100) = NULL;   -- NULL = utente Windows di chi esegue lo script

IF EXISTS (SELECT 1 FROM Attivita)
BEGIN
    RAISERROR('Il database contiene già delle attività: questo script va eseguito solo su un database demo appena creato. Nessuna modifica fatta.', 16, 1);
    RETURN;
END

IF @UtentePrincipale IS NULL
    SET @UtentePrincipale = CASE WHEN CHARINDEX('\', SUSER_SNAME()) > 0
                                 THEN SUBSTRING(SUSER_SNAME(), CHARINDEX('\', SUSER_SNAME()) + 1, 100)
                                 ELSE SUSER_SNAME() END;

DECLARE @Oggi DATE = CAST(GETDATE() AS DATE);
DECLARE @Adesso DATETIME = GETDATE();

BEGIN TRANSACTION;

-- ============================================================
-- 1. Utenti
-- ============================================================
INSERT INTO Utenti (WindowsUsername, NomeCompleto, Email, DataPrimoAccesso, UltimoAccesso, Attivo, PrivacyMode)
VALUES (@UtentePrincipale, N'Mario Rossi', N'mario.rossi@example.com', DATEADD(MONTH, -8, @Oggi), @Adesso, 1, 0),
       (N'lbianchi', N'Laura Bianchi', N'laura.bianchi@example.com', DATEADD(MONTH, -8, @Oggi), @Adesso, 1, 0),
       (N'gverdi', N'Giulia Verdi', N'giulia.verdi@example.com', DATEADD(MONTH, -5, @Oggi), @Adesso, 1, 1),
       (N'fneri', N'Federico Neri', N'federico.neri@example.com', DATEADD(MONTH, -3, @Oggi), @Adesso, 1, 0);

DECLARE @Mario INT = (SELECT Id FROM Utenti WHERE WindowsUsername = @UtentePrincipale);
DECLARE @Laura INT = (SELECT Id FROM Utenti WHERE WindowsUsername = N'lbianchi');
DECLARE @Giulia INT = (SELECT Id FROM Utenti WHERE WindowsUsername = N'gverdi');
DECLARE @Federico INT = (SELECT Id FROM Utenti WHERE WindowsUsername = N'fneri');

-- ============================================================
-- 2. Clienti (quelli creati da CreateDatabase.sql sono sostituiti da nomi inventati)
-- ============================================================
DELETE FROM Clienti WHERE Nome <> N'Sviluppo';
INSERT INTO Clienti (Nome, Attivo, Lingua)
VALUES (N'Aurora Assicurazioni', 1, 'IT'), (N'Banca del Borgo', 1, 'IT'), (N'Mutua Orizzonte', 1, 'IT'),
       (N'Previdenza Levante', 1, 'IT'), (N'Vela Assicurazioni', 1, 'IT'), (N'Northwind Insurance', 1, 'EN'),
       (N'Cassa Montebianco', 0, 'IT');

-- ============================================================
-- 3. Congelati
-- ============================================================
DELETE FROM Ambienti;
INSERT INTO Ambienti (Codice, Descrizione, Descrizione4, Descrizione5, DataCongelamento, DataDismissione, Attivo, Note)
VALUES (N'Orbita3262', N'3.2.62', N'4.0.14', NULL, DATEADD(DAY, -5, @Oggi), NULL, 1, N'Congelato del rilascio di novembre: contiene la nuova stampa riepilogo sinistri.'),
       (N'Orbita3261', N'3.2.61', N'4.0.12', NULL, DATEADD(DAY, -35, @Oggi), NULL, 1, N'In Produzione da Aurora Assicurazioni e Banca del Borgo.'),
       (N'Orbita3260', N'3.2.60', N'4.0.10', N'5.1.02', DATEADD(DAY, -70, @Oggi), NULL, 1, NULL),
       (N'Orbita3259', N'3.2.59', NULL, NULL, DATEADD(DAY, -105, @Oggi), NULL, 1, N'Ultima versione con il vecchio motore di stampa.'),
       (N'Orbita3258', N'3.2.58', NULL, NULL, DATEADD(DAY, -140, @Oggi), NULL, 1, NULL),
       (N'Orbita3257', N'3.2.57', NULL, NULL, DATEADD(DAY, -175, @Oggi), DATEADD(DAY, -40, @Oggi), 0, N'Dismesso: nessun cliente la usa più.');

-- ============================================================
-- 4. Software, tipi attività, versioni di rilascio
-- ============================================================
INSERT INTO Software (Nome, Ordine, Attivo)
VALUES (N'Orbita', 1, 1), (N'Orbita Web', 2, 1), (N'Portale Agenti', 3, 1), (N'Servizi di integrazione', 4, 1), (N'Orbita Legacy', 5, 0);

IF NOT EXISTS (SELECT 1 FROM TipiAttivita WHERE Nome = N'Riunione')
    INSERT INTO TipiAttivita (Nome, Ordine, Attivo) VALUES (N'Riunione', 4, 1);
IF NOT EXISTS (SELECT 1 FROM TipiAttivita WHERE Nome = N'Formazione')
    INSERT INTO TipiAttivita (Nome, Ordine, Attivo) VALUES (N'Formazione', 5, 1);

INSERT INTO VersioniRilascio (Versione, Attivo)
SELECT v, 1 FROM (VALUES (N'3.2.58'), (N'3.2.59'), (N'3.2.60'), (N'3.2.61'), (N'3.2.62')) AS x(v)
WHERE NOT EXISTS (SELECT 1 FROM VersioniRilascio WHERE Versione = x.v);

-- ============================================================
-- 5. Modelli dei lavori: ognuno dura uno o più giorni; dal secondo giorno l'attività
--    "prosegue" la precedente, come fa il pulsante Duplica. {{TODO}} diventa "--"
--    (TODO già chiuso) e torna "TODO" solo nell'ultimo giorno dei lavori ancora aperti.
-- ============================================================
DECLARE @Lavori TABLE (
    N INT IDENTITY PRIMARY KEY, Descrizione NVARCHAR(500), Ticket NVARCHAR(50), Cliente NVARCHAR(100),
    Software NVARCHAR(100), Giorni INT, NotaInizio NVARCHAR(MAX), NotaSeguito NVARCHAR(MAX),
    FileCoinvolti NVARCHAR(MAX), Congelato NVARCHAR(50), ConRilascio BIT, Vedere NVARCHAR(500));

INSERT INTO @Lavori (Descrizione, Ticket, Cliente, Software, Giorni, NotaInizio, NotaSeguito, FileCoinvolti, Congelato, ConRilascio, Vedere) VALUES
(N'Correzione calcolo del premio sulle polizze rinnovate', N'INC-4512', N'Aurora Assicurazioni', N'Orbita', 3,
 N'<div><b>Segnalazione</b>: sulle polizze rinnovate il premio annuo risulta più alto di quello del preventivatore.</div><div><br></div><div><b>Analisi</b></div><div>&nbsp;&nbsp;&nbsp;&nbsp;- riprodotto in Test con la polizza <span style="background-color: yellow;">AU-2024-118734</span></div><div>&nbsp;&nbsp;&nbsp;&nbsp;- in Produzione il ricalcolo va in timeout, schermata inviata dall''utente:</div><div><img src="data:image/png;base64,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"></div><div><span style="color: red;">Attenzione</span>: succede solo con il frazionamento semestrale.</div><div>{{TODO}} verificare con il cliente se servono ricalcoli retroattivi</div>',
 N'<div><b>Correzione</b>: al rinnovo il frazionamento veniva applicato due volte.</div><div>&nbsp;&nbsp;&nbsp;&nbsp;- aggiunto il test automatico sui frazionamenti</div><div>&nbsp;&nbsp;&nbsp;&nbsp;- <span style="background-color: orange;">da riportare anche sul congelato 3.2.61</span></div><div>{{TODO}} preparare la patch per il cliente</div>',
 N'Orbita.Polizze/CalcoloPremio.cs<br>&nbsp;&nbsp;&nbsp;&nbsp;Orbita.Polizze.Tests/FrazionamentoTests.cs', N'Orbita3261', 1, N'Mail di Paolo Ferri (Aurora) del lunedì'),
(N'Import flussi agenzie: record scartati per IBAN non valido', N'INC-4530', N'Banca del Borgo', N'Servizi di integrazione', 2,
 N'<div><b>Problema</b>: l''import notturno dei flussi agenzie scarta alcuni record.</div><div>Dal log del batch:</div><div><img src="data:image/png;base64,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"></div><div><b>Causa</b>: gli IBAN arrivano con spazi interni e il controllo di formato li rifiuta.</div>',
 N'<div><b>Correzione</b>: normalizzazione dell''IBAN (spazi e maiuscole) prima del controllo.</div><div>&nbsp;&nbsp;&nbsp;&nbsp;- rielaborati i 3 record scartati: importati correttamente</div><div>{{TODO}} chiedere alla banca di correggere il tracciato a monte</div>',
 N'Orbita.Integrazione/Flussi/ImportAgenzie.cs<br>&nbsp;&nbsp;&nbsp;&nbsp;Orbita.Integrazione/Validazioni/Iban.cs', NULL, 1, NULL),
(N'Nuova stampa riepilogo sinistri in PDF', N'CR-1180', N'Mutua Orizzonte', N'Orbita Web', 4,
 N'<div><b>Richiesta</b>: stampa riepilogativa dei sinistri per contraente, in PDF, con il logo del cliente.</div><div><br></div><div><b>Da fare</b></div><div>&nbsp;&nbsp;&nbsp;&nbsp;1. query sui sinistri aperti e chiusi nell''anno</div><div>&nbsp;&nbsp;&nbsp;&nbsp;2. modello di stampa con totali per ramo</div><div>&nbsp;&nbsp;&nbsp;&nbsp;3. pulsante nella scheda contraente</div><div>{{TODO}} farsi mandare dal cliente il logo in alta risoluzione</div>',
 N'<div>Modello di stampa completato: <b>totali per ramo</b> e per anno.</div><div>&nbsp;&nbsp;&nbsp;&nbsp;- <span style="background-color: yellow;">manca la paginazione sui contraenti con più di 200 sinistri</span></div><div>{{TODO}} provare la stampa con i dati di Produzione anonimizzati</div>',
 N'Orbita.Web/Sinistri/RiepilogoController.cs<br>&nbsp;&nbsp;&nbsp;&nbsp;Orbita.Web/Stampe/RiepilogoSinistri.rdlc', N'Orbita3262', 1, N'Documento di analisi in SharePoint'),
(N'Analisi lentezza ricerca anagrafiche', N'INC-4547', N'Previdenza Levante', N'Orbita', 2,
 N'<div><b>Segnalazione</b>: la ricerca anagrafiche per codice fiscale impiega oltre 20 secondi.</div><div>&nbsp;&nbsp;&nbsp;&nbsp;- piano di esecuzione: <span style="color: red;">scansione completa</span> della tabella Anagrafiche</div><div>&nbsp;&nbsp;&nbsp;&nbsp;- manca un indice su CodiceFiscale</div>',
 N'<div>Creato l''indice e riscritta la query senza funzioni sulla colonna: <b>da 21 s a 80 ms</b>.</div><div>{{TODO}} concordare con il DBA del cliente la finestra per creare l''indice in Produzione</div>',
 N'Database/Patch/3.2.62/IX_Anagrafiche_CodiceFiscale.sql<br>&nbsp;&nbsp;&nbsp;&nbsp;Orbita.Anagrafiche/RicercaAnagrafiche.cs', N'Orbita3261', 1, NULL),
(N'Portale agenti: accesso con autenticazione a due fattori', N'CR-1192', N'Vela Assicurazioni', N'Portale Agenti', 4,
 N'<div><b>Richiesta</b>: secondo fattore (codice via app) per gli agenti che accedono da fuori rete.</div><div>&nbsp;&nbsp;&nbsp;&nbsp;- libreria TOTP già usata nel back-office</div><div>&nbsp;&nbsp;&nbsp;&nbsp;- serve una pagina di registrazione del dispositivo</div><div>{{TODO}} definire con il cliente i codici di recupero</div>',
 N'<div>Pagina di registrazione e verifica del codice completate.</div><div>&nbsp;&nbsp;&nbsp;&nbsp;- <span style="background-color: yellow;">provato con Google Authenticator e Microsoft Authenticator</span></div><div>{{TODO}} scrivere le istruzioni per gli agenti</div>',
 N'PortaleAgenti/Account/DueFattori.cshtml<br>&nbsp;&nbsp;&nbsp;&nbsp;PortaleAgenti/Sicurezza/TotpService.cs', NULL, 1, N'Requisiti nella mail di Marco Galli (Vela)'),
(N'Aggiornamento librerie e pulizia warning di compilazione', N'DEV-310', N'Sviluppo', N'Orbita', 2,
 N'<div>Aggiornate le librerie di terze parti alle ultime versioni stabili.</div><div>&nbsp;&nbsp;&nbsp;&nbsp;- 37 warning di compilazione all''inizio</div>',
 N'<div>Warning portati a <b>zero</b>; build e test automatici verdi.</div>',
 N'Directory.Packages.props<br>&nbsp;&nbsp;&nbsp;&nbsp;Orbita.Common/Obsoleti.cs', NULL, 0, NULL),
(N'Export XLSX scadenzario: colonne mancanti', N'INC-4561', N'Aurora Assicurazioni', N'Orbita Web', 1,
 N'<div><b>Segnalazione</b>: nell''export dello scadenzario mancano le colonne Agenzia e Frazionamento.</div><div><b>Correzione</b>: aggiunte le due colonne e la larghezza automatica.</div>',
 N'<div>Verificato l''export con il cliente.</div>',
 N'Orbita.Web/Scadenzario/ExportXlsx.cs', N'Orbita3262', 1, NULL),
(N'Migrazione report da Crystal Reports al nuovo motore di stampa', N'CR-1201', N'Northwind Insurance', N'Orbita', 3,
 N'<div><b>Richiesta</b>: portare i 12 report del cliente sul nuovo motore di stampa.</div><div>&nbsp;&nbsp;&nbsp;&nbsp;- elenco dei report e priorità concordati con il cliente</div><div>&nbsp;&nbsp;&nbsp;&nbsp;- i report sono in <b>inglese</b>: etichette dal file risorse</div>',
 N'<div>Migrati altri report: <span style="background-color: yellow;">8 su 12</span>.</div><div>{{TODO}} confrontare i totali con le stampe originali</div>',
 N'Orbita.Stampe/Northwind/*.rdlc<br>&nbsp;&nbsp;&nbsp;&nbsp;Orbita.Stampe/Risorse/Etichette.en.resx', NULL, 1, NULL),
(N'Supporto al rilascio in Produzione della versione 3.2.61', N'REL-061', N'Banca del Borgo', N'Orbita', 1,
 N'<div>Supporto telefonico al cliente durante l''installazione.</div><div>&nbsp;&nbsp;&nbsp;&nbsp;- patch database eseguita senza errori</div><div>&nbsp;&nbsp;&nbsp;&nbsp;- riavviati i pool applicativi, verifiche funzionali ok</div>',
 N'<div>Verifiche del giorno dopo: nessuna segnalazione.</div>',
 NULL, NULL, 1, N'Ticket di rilascio del cliente'),
(N'Validazione del codice fiscale nei nuovi contraenti', N'INC-4575', N'Mutua Orizzonte', N'Orbita', 2,
 N'<div><b>Segnalazione</b>: è possibile salvare un contraente con codice fiscale formalmente errato.</div><div>&nbsp;&nbsp;&nbsp;&nbsp;- manca il controllo del carattere di controllo</div>',
 N'<div>Aggiunto il controllo completo (omocodie comprese) e il messaggio all''utente.</div><div>{{TODO}} verificare i contraenti già salvati con un codice errato</div>',
 N'Orbita.Anagrafiche/Validazioni/CodiceFiscale.cs<br>&nbsp;&nbsp;&nbsp;&nbsp;Orbita.Anagrafiche.Tests/CodiceFiscaleTests.cs', N'Orbita3262', 1, NULL),
(N'Servizio REST per il preventivatore online', N'CR-1215', N'Vela Assicurazioni', N'Servizi di integrazione', 3,
 N'<div><b>Richiesta</b>: esporre il calcolo del preventivo al sito del cliente.</div><div>&nbsp;&nbsp;&nbsp;&nbsp;- autenticazione con chiave API</div><div>&nbsp;&nbsp;&nbsp;&nbsp;- risposta in JSON con premio lordo, imposte e garanzie</div>',
 N'<div>Servizio completato e documentato con OpenAPI.</div><div>&nbsp;&nbsp;&nbsp;&nbsp;- <span style="background-color: orange;">limite di 10 richieste al secondo per chiave</span></div>',
 N'Orbita.Api/Preventivi/PreventiviController.cs<br>&nbsp;&nbsp;&nbsp;&nbsp;Orbita.Api/openapi.yaml', NULL, 1, N'Specifiche in "Vela - preventivatore v2.docx"'),
(N'Correzione arrotondamenti nelle quietanze', N'INC-4590', N'Previdenza Levante', N'Orbita', 1,
 N'<div><b>Segnalazione</b>: differenze di un centesimo tra quietanza e premio in polizza.</div><div><b>Correzione</b>: arrotondamento solo sul totale, non sulle singole rate.</div>',
 N'<div>Ricontrollate le quietanze del mese: nessuna differenza.</div>',
 N'Orbita.Contabilita/Quietanze.cs', N'Orbita3261', 1, NULL),
(N'Revisione dei permessi degli utenti di back-office', N'INC-4598', N'Aurora Assicurazioni', N'Orbita Web', 2,
 N'<div><b>Richiesta</b>: gli operatori di sportello vedono anche le funzioni di contabilità.</div><div>&nbsp;&nbsp;&nbsp;&nbsp;- mappa dei ruoli rivista con il responsabile del cliente</div>',
 N'<div>Nuovo ruolo <b>Sportello</b> senza le funzioni contabili; utenti spostati dal cliente.</div><div>{{TODO}} rimuovere il vecchio ruolo dopo un mese di prova</div>',
 N'Orbita.Web/Sicurezza/Ruoli.cs<br>&nbsp;&nbsp;&nbsp;&nbsp;Database/Patch/3.2.62/Ruolo_Sportello.sql', NULL, 1, NULL),
(N'Test di carico sul batch notturno', N'DEV-322', N'Sviluppo', N'Servizi di integrazione', 2,
 N'<div>Preparato un ambiente con un volume di dati pari a tre volte quello di Produzione.</div>',
 N'<div>Batch completato in <b>48 minuti</b> (limite 2 ore). Collo di bottiglia: scrittura dei log.</div>',
 NULL, NULL, 0, NULL),
(N'Allineamento anagrafiche agenti con il CRM', N'CR-1224', N'Northwind Insurance', N'Portale Agenti', 3,
 N'<div><b>Requirement</b>: keep agent records in sync with the customer''s CRM.</div><div>&nbsp;&nbsp;&nbsp;&nbsp;- sincronizzazione notturna, il CRM è la fonte di verità</div>',
 N'<div>Sincronizzazione completata; gestiti gli agenti cessati.</div><div>{{TODO}} chiedere al cliente un utente di servizio dedicato</div>',
 N'PortaleAgenti/Sincronizzazione/CrmSync.cs', NULL, 1, NULL),
(N'Documentazione tecnica del modulo sinistri', N'DEV-330', N'Sviluppo', N'Orbita', 1,
 N'<div>Aggiornata la documentazione tecnica del modulo sinistri: flussi, tabelle e servizi esposti.</div>',
 N'<div>Revisione completata con Laura.</div>',
 NULL, NULL, 0, NULL);

DECLARE @NumLavori INT = (SELECT COUNT(*) FROM @Lavori);

-- ============================================================
-- 6. Attività: per ogni utente, giorno per giorno (lunedì-venerdì), si prosegue con i
--    modelli dei lavori; in più riunioni del lunedì, un permesso, la formazione e le ferie.
-- ============================================================
DECLARE @Persone TABLE (Ordine INT, UtenteId INT, Storico INT, Partenza INT);
INSERT INTO @Persone VALUES (1, @Mario, 75, 1), (2, @Laura, 45, 7), (3, @Giulia, 30, 12);

DECLARE @Changeset INT = 48120;
DECLARE @p INT = 1;
WHILE @p <= 3
BEGIN
    DECLARE @UtenteId INT, @Storico INT, @t INT, @k INT = 0, @Ciclo INT = 0;
    DECLARE @IdPrec INT = NULL, @DataPrec DATE = NULL, @PermessoFatto BIT = 0, @FormazioneFatta BIT = 0;
    SELECT @UtenteId = UtenteId, @Storico = Storico, @t = Partenza FROM @Persone WHERE Ordine = @p;
    DECLARE @Data DATE = DATEADD(DAY, -@Storico, @Oggi);

    WHILE @Data <= @Oggi
    BEGIN
        DECLARE @GiornoSett INT = DATEDIFF(DAY, '19000101', @Data) % 7;   -- 0 = lunedì ... 6 = domenica
        DECLARE @Versione NVARCHAR(20) = CASE WHEN @Data >= DATEADD(DAY, -5, @Oggi) THEN N'3.2.63'
                                              WHEN @Data >= DATEADD(DAY, -35, @Oggi) THEN N'3.2.62' ELSE N'3.2.61' END;
        DECLARE @Sviluppo INT = (SELECT Id FROM Clienti WHERE Nome = N'Sviluppo');
        DECLARE @OreLavoro DECIMAL(5, 2) = 8;

        IF @GiornoSett < 5
        BEGIN
            -- Ferie di Laura: tre giorni circa tre settimane fa
            IF @UtenteId = @Laura AND @Data BETWEEN DATEADD(DAY, -23, @Oggi) AND DATEADD(DAY, -19, @Oggi) AND @GiornoSett <= 2
            BEGIN
                INSERT INTO Attivita (UtenteId, Data, Descrizione, OreLavorate, TipoAttivita, Note, DataCreazione, DataModifica)
                VALUES (@UtenteId, @Data, N'Ferie', 8, N'Ferie', N'<div>Ferie programmate.</div>', @Data, @Data);
                SET @Data = DATEADD(DAY, 1, @Data);
                CONTINUE;
            END

            -- Riunione settimanale del lunedì (Mario)
            IF @UtenteId = @Mario AND @GiornoSett = 0
            BEGIN
                INSERT INTO Attivita (UtenteId, Data, Descrizione, OreLavorate, ClienteId, TipoAttivita, Note, Vedere, DataCreazione, DataModifica)
                VALUES (@UtenteId, @Data, N'Riunione di avanzamento settimanale', 1, @Sviluppo, N'Riunione',
                        N'<div>Stato dei ticket aperti e priorità della settimana.</div><div>&nbsp;&nbsp;&nbsp;&nbsp;- presenti: Laura, Giulia, Federico</div>',
                        N'Verbale nella cartella del team', @Data, @Data);
                SET @OreLavoro = 7;
            END

            -- Permesso di mezza giornata (Mario), circa due settimane fa
            IF @UtenteId = @Mario AND @PermessoFatto = 0 AND @Data >= DATEADD(DAY, -15, @Oggi) AND @GiornoSett = 2
            BEGIN
                INSERT INTO Attivita (UtenteId, Data, Descrizione, OreLavorate, TipoAttivita, Note, DataCreazione, DataModifica)
                VALUES (@UtenteId, @Data, N'Permesso per visita medica', 4, N'Permesso', N'<div>Uscita alle 13:00.</div>', @Data, @Data);
                SET @OreLavoro = 4; SET @PermessoFatto = 1;
            END

            -- Formazione (Mario), circa cinque settimane fa
            IF @UtenteId = @Mario AND @FormazioneFatta = 0 AND @Data >= DATEADD(DAY, -38, @Oggi) AND @GiornoSett = 3
            BEGIN
                INSERT INTO Attivita (UtenteId, Data, Descrizione, OreLavorate, ClienteId, TipoAttivita, Note, UrlTicket, DataCreazione, DataModifica)
                VALUES (@UtenteId, @Data, N'Corso di sicurezza delle applicazioni web (OWASP Top 10)', 4, @Sviluppo, N'Formazione',
                        N'<div>Corso interno, modulo 1: <b>injection</b> e <b>autenticazione</b>.</div><div>&nbsp;&nbsp;&nbsp;&nbsp;- da rivedere i controlli sui parametri del portale agenti</div>',
                        N'https://formazione.example.com/corsi/owasp-top-10', @Data, @Data);
                SET @OreLavoro = 4; SET @FormazioneFatta = 1;
            END

            -- Oggi la giornata non è finita
            IF @Data = @Oggi SET @OreLavoro = @OreLavoro - 4;

            -- Attività del lavoro in corso
            DECLARE @Descrizione NVARCHAR(500), @TicketBase NVARCHAR(50), @Cliente NVARCHAR(100), @Software NVARCHAR(100), @Giorni INT,
                    @NotaInizio NVARCHAR(MAX), @NotaSeguito NVARCHAR(MAX), @File NVARCHAR(MAX), @Congelato NVARCHAR(50),
                    @ConRilascio BIT, @Vedere NVARCHAR(500);
            SELECT @Descrizione = Descrizione, @TicketBase = Ticket, @Cliente = Cliente, @Software = Software, @Giorni = Giorni,
                   @NotaInizio = NotaInizio, @NotaSeguito = NotaSeguito, @File = FileCoinvolti, @Congelato = Congelato,
                   @ConRilascio = ConRilascio, @Vedere = Vedere
            FROM @Lavori WHERE N = @t;

            -- Dal secondo giro i ticket sono nuovi (stesso tipo di lavoro, numero diverso)
            DECLARE @Ticket NVARCHAR(50) = LEFT(@TicketBase, CHARINDEX('-', @TicketBase))
                + CAST(CAST(SUBSTRING(@TicketBase, CHARINDEX('-', @TicketBase) + 1, 10) AS INT) + 100 * @Ciclo AS NVARCHAR(10));
            DECLARE @Numero NVARCHAR(10) = SUBSTRING(@Ticket, CHARINDEX('-', @Ticket) + 1, 10);
            DECLARE @Url NVARCHAR(MAX) = CASE WHEN @Ticket LIKE N'INC-%'
                THEN N'https://servicedesk.example.com/Detail/' + @Ticket + N'$entity'
                ELSE N'https://devops.example.com/Orbita/_workitems/edit/' + @Numero END;
            DECLARE @CheckIn NVARCHAR(500) = @Ticket + N' - ' + @Descrizione;

            DECLARE @Nota NVARCHAR(MAX) = CASE WHEN @k = 0 THEN @NotaInizio
                ELSE N'<div>**** Proseguo l''attività ' + CAST(@IdPrec AS NVARCHAR(10)) + N'-' + @Descrizione + N'-' + @Ticket
                     + N' del ' + FORMAT(@DataPrec, 'dd/MM/yyyy') + N' ****</div>' + @NotaSeguito END;
            SET @Nota = REPLACE(@Nota, N'{{TODO}}', N'--');

            DECLARE @ChangesetHtml NVARCHAR(MAX) = NULL;
            IF @File IS NOT NULL
            BEGIN
                SET @ChangesetHtml = N'<strong>Sviluppo (' + @Versione + N'):</strong><br>Changeset ' + CAST(@Changeset AS NVARCHAR(10)) + N': ' + @CheckIn
                    + N'<br>&nbsp;&nbsp;&nbsp;&nbsp;' + @File;
                IF @Congelato IS NOT NULL
                    SET @ChangesetHtml = @ChangesetHtml + N'<br><br><strong>' + @Congelato + N' (' + (SELECT Descrizione FROM Ambienti WHERE Codice = @Congelato)
                        + N'):</strong><br>Changeset ' + CAST(@Changeset + 1 AS NVARCHAR(10)) + N': ' + @CheckIn;
            END

            INSERT INTO Attivita (UtenteId, Data, Descrizione, OreLavorate, ClienteId, Versione, UrlTicket, Vedere, CartellaDocumentazione,
                                  Note, TestoCheckIn, ChangesetCoinvolti, TipoAttivita, NumeroTicket, UrlPatchRilasci, SoftwareId,
                                  DataCreazione, DataModifica)
            VALUES (@UtenteId, @Data, @Descrizione, @OreLavoro, (SELECT Id FROM Clienti WHERE Nome = @Cliente), @Versione, @Url, @Vedere,
                    CASE WHEN @Ticket LIKE N'CR-%' THEN N'C:\Documentazione\' + @Cliente + N'\' + @Ticket END,
                    @Nota, CASE WHEN @File IS NOT NULL THEN @CheckIn END, @ChangesetHtml, N'Lavoro', @Ticket,
                    CASE WHEN @ConRilascio = 1 AND @k = @Giorni - 1 AND @Cliente <> N'Sviluppo'
                         THEN N'https://rilasci.example.com/orbita/' + @Versione + N'/' + @Ticket + N'.zip' END,
                    (SELECT Id FROM Software WHERE Nome = @Software),
                    DATEADD(HOUR, 9, CAST(@Data AS DATETIME)), DATEADD(HOUR, 18, CAST(@Data AS DATETIME)));

            DECLARE @IdNuovo INT = SCOPE_IDENTITY();

            -- Congelato su cui è riportata la correzione
            IF @Congelato IS NOT NULL AND @File IS NOT NULL
                INSERT INTO AttivitaAmbienti (AttivitaId, AmbienteId) SELECT @IdNuovo, Id FROM Ambienti WHERE Codice = @Congelato;

            -- Ambienti di rilascio nell'ultimo giorno del lavoro
            IF @ConRilascio = 1 AND @k = @Giorni - 1 AND @Cliente <> N'Sviluppo'
                INSERT INTO AttivitaAmbientiRilascio (AttivitaId, Posizione, TipoAmbiente, Versione)
                VALUES (@IdNuovo, 1, N'Test', @Versione), (@IdNuovo, 2, N'Produzione', CASE WHEN @Versione = N'3.2.63' THEN N'3.2.62' ELSE N'3.2.61' END);

            SET @IdPrec = @IdNuovo; SET @DataPrec = @Data;
            SET @k = @k + 1;
            IF @k >= @Giorni
            BEGIN
                SET @k = 0; SET @IdPrec = NULL;
                SET @Changeset = @Changeset + 2;
                SET @t = @t % @NumLavori + 1;
                IF @t = 1 SET @Ciclo = @Ciclo + 1;
            END
        END

        SET @Data = DATEADD(DAY, 1, @Data);
    END

    SET @p = @p + 1;
END

-- ============================================================
-- 7. TODO aperti e scadenze: nell'ultimo giorno dei lavori recenti di Mario il TODO torna
--    aperto; ai primi quattro una scadenza ticket (domani, fra 4 giorni, scaduta, fra 12 giorni)
--    così la griglia mostra tutti i colori.
-- ============================================================
;WITH Ultimi AS (
    SELECT Id, Data, ROW_NUMBER() OVER (PARTITION BY NumeroTicket ORDER BY Data DESC) AS Recente
    FROM Attivita
    WHERE UtenteId = @Mario AND TipoAttivita = N'Lavoro' AND Data >= DATEADD(DAY, -20, @Oggi) AND Note LIKE N'%<div>-- %'
), Aperti AS (
    SELECT Id, ROW_NUMBER() OVER (ORDER BY Data DESC, Id DESC) AS Ordine FROM Ultimi WHERE Recente = 1
)
UPDATE a
SET Note = REPLACE(a.Note, N'<div>-- ', N'<div>TODO '),
    DataScadenzaTicket = CASE ap.Ordine WHEN 1 THEN DATEADD(DAY, 1, @Oggi) WHEN 2 THEN DATEADD(DAY, 4, @Oggi)
                                        WHEN 3 THEN DATEADD(DAY, -2, @Oggi) WHEN 4 THEN DATEADD(DAY, 12, @Oggi) END
FROM Attivita a JOIN Aperti ap ON ap.Id = a.Id;

-- Un TODO aperto anche per Laura, per la modalità Admin
UPDATE Attivita SET Note = REPLACE(Note, N'<div>-- ', N'<div>TODO ')
WHERE Id = (SELECT TOP 1 Id FROM Attivita WHERE UtenteId = @Laura AND Note LIKE N'%<div>-- %' ORDER BY Data DESC);

-- Testo negli screenshot: le attività senza immagini sono già "lette"; quelle con immagini
-- (OcrImpronta NULL) le legge il programma in background al primo avvio.
UPDATE Attivita SET OcrImpronta = '', NoteOcr = '', ChangesetOcr = ''
WHERE ISNULL(Note, '') NOT LIKE '%<img%' AND ISNULL(ChangesetCoinvolti, '') NOT LIKE '%<img%';

-- ============================================================
-- 8. TODO List, Appunti, Calendario (di Mario)
-- ============================================================
INSERT INTO TodoItems (UtenteId, Titolo, Descrizione, DataInserimento, LivelloUrgenza, Completato, DataCompletamento, NotaCompletamento)
VALUES (@Mario, N'Verificare il certificato SSL del portale agenti', N'Il certificato scade a breve: chiedere al sistemista del cliente il rinnovo e provarlo in Test.', DATEADD(DAY, -2, @Oggi), 5, 0, NULL, NULL),
       (@Mario, N'Preparare la patch 3.2.62 per Aurora Assicurazioni', N'Includere INC-4512 e INC-4561; script database e note di installazione in italiano.', DATEADD(DAY, -4, @Oggi), 4, 0, NULL, NULL),
       (@Mario, N'Rivedere gli indici sulle anagrafiche', N'Dopo INC-4547 controllare gli altri campi usati nelle ricerche (partita IVA, email).', DATEADD(DAY, -9, @Oggi), 3, 0, NULL, NULL),
       (@Mario, N'Aggiornare la documentazione del preventivatore', N'Aggiungere gli esempi di chiamata del nuovo servizio REST.', DATEADD(DAY, -12, @Oggi), 2, 0, NULL, NULL),
       (@Mario, N'Chiedere a Laura i dati di test per il portale agenti', N'Servono almeno 20 agenti con profili diversi.', DATEADD(DAY, -15, @Oggi), 1, 0, NULL, NULL),
       (@Mario, N'Configurare la VPN di Mutua Orizzonte', N'Nuovo client VPN richiesto dal cliente.', DATEADD(DAY, -30, @Oggi), 3, 1, DATEADD(DAY, -27, @Oggi), N'Configurata; istruzioni in Gestione Clienti, ambiente Produzione.'),
       (@Mario, N'Installare l''SDK .NET 10 sulla macchina di build', N'Necessario per l''aggiornamento delle librerie (DEV-310).', DATEADD(DAY, -40, @Oggi), 2, 1, DATEADD(DAY, -38, @Oggi), N'Installato; build e test verdi.');

DECLARE @Appunti TABLE (N INT IDENTITY, Titolo NVARCHAR(300), Descrizione NVARCHAR(MAX), Tags NVARCHAR(300), Giorni INT);
INSERT INTO @Appunti (Titolo, Descrizione, Tags, Giorni) VALUES
(N'Comandi utili per il deploy su IIS', N'Riciclare un pool:
  appcmd recycle apppool /apppool.name:"Orbita"
Fermare e riavviare il sito:
  appcmd stop site /site.name:"Orbita"
  appcmd start site /site.name:"Orbita"', N'deploy,iis,powershell', 50),
(N'Query per trovare le polizze senza quietanza', N'SELECT p.Numero, p.DataEffetto
FROM Polizze p
LEFT JOIN Quietanze q ON q.PolizzaId = p.Id
WHERE q.Id IS NULL AND p.Stato = ''Attiva''', N'sql,polizze', 35),
(N'Documentazione delle API del preventivatore', N'La documentazione OpenAPI è pubblicata qui: https://docs.example.com/preventivatore
Chiave API di prova nel vault del team.', N'api,documentazione,link', 20),
(N'Come rigenerare i certificati di sviluppo', N'dotnet dev-certs https --clean
dotnet dev-certs https --trust', N'certificati,sviluppo', 15),
(N'Convenzioni per i messaggi di check-in', N'Formato: NUMERO TICKET - Descrizione breve
Esempio: INC-4512 - Correzione calcolo del premio sulle polizze rinnovate
Il pulsante ✨ accanto al testo del check-in lo compone da solo.', N'svn,convenzioni', 10),
(N'Reperibilità durante i rilasci', N'Rilasci in Produzione il martedì sera: reperibile chi ha preparato la patch.
In caso di problemi: prima il referente IT del cliente, poi il responsabile di progetto.', N'rilasci,contatti', 5);

INSERT INTO Appunti (UtenteId, Titolo, Descrizione, DataCreazione, DataModifica)
SELECT @Mario, Titolo, Descrizione, DATEADD(DAY, -Giorni, @Oggi), DATEADD(DAY, -Giorni, @Oggi) FROM @Appunti ORDER BY N;

INSERT INTO AppuntiTags (AppuntoId, NomeTag)
SELECT ap.Id, LTRIM(RTRIM(t.value))
FROM @Appunti x
JOIN Appunti ap ON ap.UtenteId = @Mario AND ap.Titolo = x.Titolo
CROSS APPLY STRING_SPLIT(x.Tags, ',') t;

INSERT INTO EventiCalendario (UtenteId, Descrizione, DataEvento, GiorniPrimaAvviso, Risolto, DataCreazione)
VALUES (@Mario, N'Rinnovo del certificato SSL del portale agenti', DATEADD(HOUR, 10, CAST(DATEADD(DAY, 3, @Oggi) AS DATETIME)), 7, 0, DATEADD(DAY, -10, @Oggi)),
       (@Mario, N'Rilascio 3.2.62 in Produzione da Aurora Assicurazioni', DATEADD(HOUR, 20, CAST(DATEADD(DAY, 12, @Oggi) AS DATETIME)), 5, 0, DATEADD(DAY, -6, @Oggi)),
       (@Mario, N'Scadenza della password VPN di Banca del Borgo', DATEADD(HOUR, 9, CAST(DATEADD(DAY, 25, @Oggi) AS DATETIME)), 7, 0, DATEADD(DAY, -20, @Oggi)),
       (@Mario, N'Riunione trimestrale con Mutua Orizzonte', DATEADD(HOUR, 15, CAST(DATEADD(DAY, -6, @Oggi) AS DATETIME)), 3, 1, DATEADD(DAY, -30, @Oggi));

-- ============================================================
-- 9. Segnalazioni con risposte
-- ============================================================
DECLARE @Segn TABLE (N INT IDENTITY, UtenteId INT, Testo NVARCHAR(MAX), Stato NVARCHAR(50), Giorni INT);
INSERT INTO @Segn (UtenteId, Testo, Stato, Giorni) VALUES
(@Laura, N'Nella Ricerca Avanzata sarebbe utile cercare anche il testo degli screenshot incollati nelle note.', N'Risolto', 40),
(@Giulia, N'L''export XLSX della griglia non riporta la colonna Software.', N'Richiesta accettata', 12),
(@Federico, N'Con la VPN lenta a volte il salvataggio fallisce e temo di perdere quello che ho scritto.', N'Risposta', 8),
(@Mario, N'Proposta: un tema scuro per chi lavora la sera.', N'Modifica proposta accettata', 6),
(@Laura, N'Il pulsante Duplica dovrebbe avvisare se l''attività aperta ha modifiche non salvate.', N'In attesa di risposta', 2);

INSERT INTO Segnalazioni (UtenteId, TestoSegnalazione, DataRichiesta, Stato)
SELECT UtenteId, Testo, DATEADD(HOUR, 11, CAST(DATEADD(DAY, -Giorni, @Oggi) AS DATETIME)), Stato FROM @Segn ORDER BY N;

INSERT INTO Segnalazioni_Risposte (SegnalazioneId, UtenteId, TestoRisposta, NuovoStato, DataRisposta)
SELECT s.Id, r.UtenteId, r.Testo, r.Stato, DATEADD(HOUR, 16, CAST(DATEADD(DAY, -r.Giorni, @Oggi) AS DATETIME))
FROM (VALUES
        (1, @Mario, N'Ottima idea, la mettiamo nella prossima versione.', N'Richiesta accettata', 38),
        (1, @Mario, N'Fatto: spunta "📷 Testo negli screenshot" nella Ricerca Avanzata.', N'Risolto', 3),
        (2, @Mario, N'Confermo, la aggiungo insieme alle altre colonne.', N'Richiesta accettata', 11),
        (3, @Mario, N'Se il database non risponde il form viene salvato in una bozza su file: al riavvio compare il pulsante Ripristina.', N'Risposta', 7),
        (4, @Laura, N'Approvata: tema chiaro, scuro o come Windows, più il colore principale.', N'Modifica proposta accettata', 5)
     ) AS r(Numero, UtenteId, Testo, Stato, Giorni)
JOIN @Segn x ON x.N = r.Numero
JOIN Segnalazioni s ON s.TestoSegnalazione = x.Testo;

-- ============================================================
-- 10. Gestione Clienti: ambienti dei clienti (dati condivisi)
-- ============================================================
DECLARE @Amb TABLE (N INT IDENTITY, Cliente NVARCHAR(100), Ambiente NVARCHAR(100), AppServer NVARCHAR(500), DbServer NVARCHAR(500),
                    Persone NVARCHAR(MAX), ComeCollegarsi NVARCHAR(MAX), DatiAmbiente NVARCHAR(MAX), Versione NVARCHAR(100), Giorni INT, Autore NVARCHAR(100));
INSERT INTO @Amb (Cliente, Ambiente, AppServer, DbServer, Persone, ComeCollegarsi, DatiAmbiente, Versione, Giorni, Autore) VALUES
(N'Aurora Assicurazioni', N'Produzione', N'srv-app-prod01.aurora.example', N'SRV-DB-PROD02\ORBITA',
 N'Paolo Ferri (referente IT) - paolo.ferri@aurora.example
Silvia Conti (DBA) - silvia.conti@aurora.example',
 N'<div><b>Accesso</b>: VPN del cliente, poi desktop remoto sul server applicativo.</div><div>&nbsp;&nbsp;&nbsp;&nbsp;1. avviare il client VPN con le credenziali personali (nel vault, non qui)</div><div>&nbsp;&nbsp;&nbsp;&nbsp;2. verificare che lo stato sia <span style="background-color: lightgreen;">Connesso</span>:</div><div><img src="data:image/png;base64,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"></div><div>&nbsp;&nbsp;&nbsp;&nbsp;3. desktop remoto su srv-app-prod01</div><div><span style="color: red;">Interventi in Produzione solo dopo conferma scritta del referente.</span></div>',
 N'<div><b>Installationlog.ini</b></div><div>Versione=3.2.61</div><div>Server=SRV-APP-PROD01</div><div>Database=ORBITA_PROD</div><div>Pool=Orbita (64 bit)</div>',
 N'3.2.61', 20, N'MARIO'),
(N'Aurora Assicurazioni', N'Test', N'srv-app-test01.aurora.example', N'SRV-DB-TEST01\ORBITA',
 N'Paolo Ferri (referente IT) - paolo.ferri@aurora.example',
 N'<div>Stessa VPN della Produzione; desktop remoto su srv-app-test01.</div><div>Il database di Test viene riallineato alla Produzione il primo lunedì del mese.</div>',
 N'<div>Versione=3.2.62</div><div>Server=SRV-APP-TEST01</div>', N'3.2.62', 4, N'lbianchi'),
(N'Banca del Borgo', N'Produzione', N'app-orbita.bancadelborgo.example', N'DB-ORBITA-01',
 N'Ufficio sistemi informativi - helpdesk@bancadelborgo.example',
 N'<div><b>Accesso</b>: portale di accesso remoto della banca con token fisico.</div><div>&nbsp;&nbsp;&nbsp;&nbsp;- il token si ritira in sede, chiedere a Laura</div><div>&nbsp;&nbsp;&nbsp;&nbsp;- sessione massima di 2 ore</div>',
 N'<div>Versione=3.2.61</div><div>Rilascio: martedì sera</div>', N'3.2.61', 30, N'lbianchi'),
(N'Mutua Orizzonte', N'Produzione', N'orizzonte-app01', N'orizzonte-sql01',
 N'Davide Marino (IT) - davide.marino@orizzonte.example',
 N'<div><b>Accesso</b>: VPN Forticlient, profilo "Fornitori".</div><div>&nbsp;&nbsp;&nbsp;&nbsp;- configurazione del client concordata con Davide Marino</div>',
 N'<div>Versione=3.2.60</div>', N'3.2.60', 27, N'MARIO'),
(N'Previdenza Levante', N'Produzione', N'levante-web01', N'levante-db01',
 N'Chiara Esposito (responsabile applicativo) - c.esposito@levante.example',
 N'<div>Accesso solo tramite il desktop remoto pubblicato dal cliente (RD Web).</div>',
 N'<div>Versione=3.2.61</div><div>Directory=D:\Orbita</div>', N'3.2.61', 45, N'fneri'),
(N'Vela Assicurazioni', N'Collaudo', N'vela-coll-app', N'vela-coll-db',
 N'Marco Galli (progetto portale) - marco.galli@vela.example',
 N'<div>VPN del cliente; il portale agenti di collaudo è su https://collaudo-agenti.vela.example</div>',
 N'<div>Portale Agenti 2.4.0 su Orbita 3.2.62</div>', N'3.2.62', 9, N'gverdi'),
(N'Northwind Insurance', N'Production', N'nwi-app-01', N'nwi-sql-01',
 N'IT Service Desk - servicedesk@northwind-insurance.example',
 N'<div>Access through the customer''s VPN; remote desktop to nwi-app-01.</div>',
 N'<div>Version=3.2.60</div>', N'3.2.60', 60, N'fneri');

INSERT INTO ClientiAmbienti (ClienteId, Ambiente, ApplicationServer, DatabaseServer, PersoneRiferimento, ComeCollegarsi, DatiAmbiente,
                             DirectoryInstallazione, InformazioniPool, TipoVersione, NumeroVersione, DataModifica)
SELECT c.Id, a.Ambiente, a.AppServer, a.DbServer, a.Persone, a.ComeCollegarsi, a.DatiAmbiente,
       N'D:\inetpub\wwwroot\Orbita', N'64 bit, identity ORBITA_SVC', N'Versione', a.Versione,
       DATEADD(HOUR, 14, CAST(DATEADD(DAY, -a.Giorni, @Oggi) AS DATETIME2))
FROM @Amb a JOIN Clienti c ON c.Nome = a.Cliente
ORDER BY a.N;

-- Log "Nuovo" per ogni ambiente: la griglia mostra l'ultimo utente che lo ha modificato
INSERT INTO ClientiAmbienti_Log (NomeUtente, AzioneSvolta, ClienteId, Ambiente, NuovoValore, Timestamp)
SELECT CASE a.Autore WHEN N'MARIO' THEN @UtentePrincipale ELSE a.Autore END, N'Nuovo', c.Id, a.Ambiente,
       N'Cliente: ' + a.Cliente + N' | Ambiente: ' + a.Ambiente, DATEADD(HOUR, 14, CAST(DATEADD(DAY, -a.Giorni, @Oggi) AS DATETIME2))
FROM @Amb a JOIN Clienti c ON c.Nome = a.Cliente;

COMMIT TRANSACTION;

-- ============================================================
-- Riepilogo
-- ============================================================
SELECT N'Utenti' AS Tabella, COUNT(*) AS Righe FROM Utenti
UNION ALL SELECT N'Attività', COUNT(*) FROM Attivita
UNION ALL SELECT N'  di cui con TODO aperto', COUNT(*) FROM Attivita WHERE Note LIKE N'%<div>TODO %'
UNION ALL SELECT N'  di cui con screenshot', COUNT(*) FROM Attivita WHERE Note LIKE N'%<img%'
UNION ALL SELECT N'Clienti', COUNT(*) FROM Clienti
UNION ALL SELECT N'Congelati', COUNT(*) FROM Ambienti
UNION ALL SELECT N'Ambienti cliente', COUNT(*) FROM ClientiAmbienti
UNION ALL SELECT N'TODO List', COUNT(*) FROM TodoItems
UNION ALL SELECT N'Appunti', COUNT(*) FROM Appunti
UNION ALL SELECT N'Segnalazioni', COUNT(*) FROM Segnalazioni
UNION ALL SELECT N'Eventi calendario', COUNT(*) FROM EventiCalendario;
