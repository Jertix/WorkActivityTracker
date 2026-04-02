# CLAUDE.md — WorkActivityTracker

Istruzioni per Claude Code su questo progetto.

## Stack Tecnologico
- **.NET MAUI Blazor Hybrid** — app desktop Windows con UI WebView
- **Blazor** — componenti `.razor` per la UI (HTML + C# nello stesso file)
- **Entity Framework Core** — ORM per accesso dati (IDbContextFactory)
- **SQL Server** — database relazionale (stringa di connessione in `appsettings.json`)
- **Bootstrap 5.3** — framework CSS caricato via CDN in `wwwroot/index.html`
- **ClosedXML 0.102.2** — generazione file XLSX per export

## Struttura Progetto

```
WorkActivityTracker/
├── Components/Pages/Home.razor      # Pagina principale (~2700 righe)
├── Components/                      # Modali Blazor (SearchModal, TodoListModal, AppuntiModal,
│                                    #   CongelatiEditorModal, ClientiEditorModal, SegnalazioniModal,
│                                    #   TipiAttivitaEditorModal, CalendarioModal)
├── Data/AppDbContext.cs             # DbContext EF Core
├── Database/                        # Script SQL migration (MigrateToVX.Y.sql)
├── Models/WorkActivity.cs           # Tutti i modelli EF + DTO + TipiAttivita + StatiSegnalazione
├── Services/                        # ActivityService, AmbienteService, AmbientiRilascioService,
│                                    #   ClienteService, SegnalazioneService, TodoService,
│                                    #   AppuntiService, UserService, AppConfigService, UnsavedChangesService,
│                                    #   TipiAttivitaService, CalendarioService
├── wwwroot/css/app.css              # CSS custom (sovrascrive Bootstrap)
├── wwwroot/index.html               # Entry point HTML + script JS custom
├── App.xaml.cs                      # Avviso chiusura con modifiche non salvate (WinUI)
├── appsettings.json                 # Configurazione (connection string, opzioni)
└── MauiProgram.cs                   # Composizione DI e configurazione app
```

## Convenzioni e Pattern

- **DTO separati dal model EF**: `WorkActivityDto` per UI/service, `Attivita` per DB.
- **Migration manuale**: non usa EF migrations, solo script SQL in `Database/MigrateToVX.Y.sql`.
- **JS Interop**: usato sparingly, definito in `wwwroot/index.html` come `window.nomeHelper`.
- **Font-size base**: 13px (`0.8125rem`) definito globalmente in `app.css`.
- **Commenti in italiano**: tutti i commenti, label e messaggi UI sono in italiano.
- **HTML nei campi ricchi**: `Note` e `ChangesetCoinvolti` salvano HTML (`innerHTML`) nel DB per persistere il grassetto. Usare `HtmlToPlainText()` per confronti stringa, export e clipboard.

## Note Importanti

- `TipiAttivita` è una classe statica in `WorkActivity.cs` con costanti stringa (Lavoro/Permesso/Ferie). La lista nel dropdown è però caricata dalla tabella DB `TipiAttivita` via `TipiAttivitaService`. Il pulsante ✏️ accanto alla combo apre `TipiAttivitaEditorModal`.
- `StatiSegnalazione` è una classe statica in `WorkActivity.cs` con gli stati delle segnalazioni.
- Il contenteditable div del changeset editor è gestito con `changesetEditorHelper` JS (`setContent`, `getHtml`, `getText`, `indent`, `rimuoviRigheVuote`).
- Il contenteditable div del Note editor è gestito con `noteEditorHelper` JS (stessa interfaccia).
- `eseguiBold(el)` in `index.html`: applica/rimuove grassetto con `document.execCommand('bold')`.
- `eseguiHighlight(el, color)` in `index.html`: evidenzia la selezione con `document.execCommand('hiliteColor', false, color)`. Colori usati: `'yellow'`, `'orange'`.
- Le toolbar degli editor (Note e Changeset) sono duplicate **sopra e sotto** ogni editor: bottoni ⇥→, ⇤←, G (bold), A giallo, A arancione, S barrato, A rosso, ✕ rimuovi formattazione, 🕐 timestamp, ─ separatore, █ spesso, █ rosso (separatore HTML colorato), ⊟ rimuovi righe vuote, **TODO** (sostituisce TODO con "--").
- `rimuoviRigheVuote(el)` in entrambi gli helper JS: rimuove i nodi `<div>`/`<p>` vuoti (solo whitespace o `&nbsp;`), poi collassa `<br>` multipli. Opera sempre sull'intero editor (senza modalità selezione). Metodi C#: `NoteRimuoviRigheVuote()`, `ChangesetRimuoviRigheVuote()`.
- `sostituisciTodoNeiNodi(el)` in `index.html`: traversa i text node dell'editor e sostituisce `TODO` (parola isolata, case-insensitive, stesso pattern di `TodoRegex`) con `"--"`, preservando tutta la struttura HTML. Metodi C#: `NoteRimuoviTodo()`, `ChangesetRimuoviTodo()`.
- La funzione `indent` in entrambi gli helper JS: trova il nodo DIV/P che contiene il cursore e aggiunge/rimuove `&nbsp;&nbsp;&nbsp;&nbsp;` al suo `innerHTML`. Quando il cursore è su contenuto BR-based (nodo direttamente nell'editor), opera solo sui nodi elemento figli (filtra text node e BR per evitare `undefined` nell'innerHTML).
- I bottoni delle toolbar **non rubano il focus** al contenteditable grazie a un listener `mousedown` globale in `index.html` che chiama `e.preventDefault()` per tutti i target dentro `.editor-toolbar`.
- `inserisciTestoACursore(el, text)` in `index.html`: inserisce testo al cursore in un contenteditable via `document.execCommand('insertText')`.
- `inserisciHtmlACursore(el, html)` in `index.html`: inserisce HTML al cursore via `document.execCommand('insertHTML')`. Usato per il separatore rosso (span colorato).
- `SegnalazioniModal`: tasto "Rispondi" sempre visibile (anche per chi ha originato la segnalazione). Filtri per Stato e Utente nella griglia con pulsante "Reset filtri".
- `SegnalazioniModal`: "Utente segnalante" è bloccato all'utente corrente (input readonly, non select). Le segnalazioni "Risolto" sono barrate e nascoste di default; checkbox "Mostra risolte" per visualizzarle.
- Pulsante 🐛 Segnala: mostra badge con contatore segnalazioni non risolte (sparisce se tutte risolte). `segnalazioniCount` aggiornato a avvio e chiusura modale.
- `VersioneSviluppoPlaceholder` in Home.razor: placeholder dinamico per "Versione di sviluppo" calcolato da `CalcolaVersioneSviluppoSuccessiva()` (versione massima congelati +1 sul patch).
- `registraCtrlS(dotNetRef)` in `index.html`: registra shortcut Ctrl+S globale che chiama `[JSInvokable] SalvaViaShortcut()` sul componente Home.
- Dopo ogni modifica ai modelli EF, creare uno script `MigrateToVX.Y.sql` nella cartella `Database/`.
- La connessione al DB è SQL Server (non SQLite).
- Non usare `git add -A` — aggiungere file specifici.
- La conferma eliminazione usa una **modale Blazor custom** (non il `confirm()` nativo del browser che in MAUI mostra "0.0.0.1 says").
- `PrivacyMode` (bool) in `appsettings.json`: se true, le attività dell'utente non sono visibili agli admin delle altre postazioni. Leggibile solo da file, nessuna UI.
- `TodoRegex` in Home.razor: usa regex `(?<![a-zA-Z])TODO(?![a-zA-Z])` per trovare TODO come parola isolata (esclude "metodo", ecc.).
- La barra di stato in fondo mostra: ultimo salvataggio, ultimo export, modalità admin, privacy mode, versione app.
- `copyToClipboard` JS in `index.html`: copia testo negli appunti con fallback `execCommand` per MAUI.
- `UnsavedChangesService`: singleton condiviso tra App.xaml.cs (layer MAUI) e Home.razor (layer Blazor). App.xaml.cs intercetta `AppWindow.Closing` (cancellabile) e mostra dialogo.
- Export file nella cartella `Export/` (nella directory dell'eseguibile, creata automaticamente). XLSX via ClosedXML.
- `HtmlToPlainText()` in Home.razor: strip tag HTML + decodifica entità. Gestisce `<br>`, `</p>`, `</div>` come newline (i contenteditable usano `<div>` per gli a capo). Chiamato in `ContieneTodo()`, `CampoHaTodo()`, `AggiungiBloccoCongelato()`, `GeneraTestoAttivita()`, `GeneraTestoAttivitaPlain()`.
- `SincronizzaTestoEditorAsync()` in Home.razor: salva l'**HTML** (via `getHtml`) del changeset editor in `AttivitaCorrente.ChangesetCoinvolti` — NON il plain text — per preservare la formattazione quando si spunta/dèspunta un congelato. Viene chiamato su `@onblur` e prima di `AggiungiBloccoCongelato()`.
- `AggiungiBloccoCongelato()` in Home.razor: confronta il testo esistente tramite `HtmlToPlainText()`, e se il blocco non è già presente appende il nuovo blocco come HTML (`<strong>heading</strong><br>Changeset XXX: testo`) all'HTML esistente, preservando tutta la formattazione. `ToggleAmbiente` imposta `changesetEditorNeedsUpdate = true` dopo la chiamata per aggiornare il DOM.
- **Combo Giorno**: quando Anno e Mese sono entrambi selezionati, mostra ogni giorno con il nome abbreviato italiano (es. "1 - lun", "2 - mar") e nasconde i giorni non validi per il mese selezionato. Quando anno o mese non sono selezionati, mostra solo i numeri 1-31 (comportamento precedente). Emoji prefisso: 🟢 per il giorno odierno (ha la precedenza), 🟠 per sabato e domenica (es. "🟠 6 - sab").
- **URL Ticket clickabili**: il campo `UrlTicket` mostra un'anteprima cliccabile sotto il textarea. `ParseUrlSegmenti()` divide il testo in segmenti URL/testo; `ApriUrl(url)` apre via `Process.Start` con `UseShellExecute=true`. Regex: `https?://[^\s]+`. Affiancato da `NumeroTicket` (campo testo, `col-md-4`).
- **Visibilità campi per tipo attività**: URL Ticket, Numero Ticket, Cartella Documentazione e Vedere sono visibili per Lavoro e per tutti i tipi **personalizzati** (custom); sono nascosti solo per Permesso e Ferie. Il messaggio informativo per i tipi personalizzati non include "Ticket" nell'elenco dei campi non applicabili.
- **GetTipoBadge()**: usa pattern matching su `TipiAttivita.Lavoro/Permesso/Ferie`; il caso `_` (default) mostra `📌 {tipo}` (badge grigio) per i tipi personalizzati. Usa `System.Net.WebUtility.HtmlEncode()` per sicurezza.
- **Filtri anno/mese rapidi**: pulsanti `@DateTime.Now.Year` accanto alla combo Anno (azzera mese+giorno) e `MMM` accanto a Mese (imposta anno+mese corrente, azzera giorno).
- **Ambienti di rilascio** (v4.1): quando cliente != "Sviluppo", nel form compaiono 3 coppie (tipo ambiente, versione) con `<input list>` + `<datalist>`. Tabelle: `TipiAmbientiRilascio`, `VersioniRilascio`, `AttivitaAmbientiRilascio` + log. Servizio: `AmbientiRilascioService`. I valori nuovi inseriti vengono aggiunti automaticamente alle liste di suggerimento al salvataggio.
- `SelezionaAttivita()` è `async Task`: carica le coppie AmbientiRilascio dal DB al momento della selezione.
- **Tipi Attività dinamici** (v4.2): tabella `TipiAttivita` + `TipiAttivita_Log`. Servizio: `TipiAttivitaService`. Pulsante ✏️ accanto alla combo apre `TipiAttivitaEditorModal`. `EnsureTipiBaseAsync()` garantisce i 3 tipi base all'avvio.
- **Calendario eventi** (v4.2): tabella `EventiCalendario`. Servizio: `CalendarioService`. Icona 📅 accanto al campo Data apre `CalendarioModal` (griglia mensile + form evento). Barra colorata in cima alla pagina mostra l'evento più urgente non risolto. Colori: verde (>5gg), arancione (<=5gg), rosso (<=2gg o oggi), grigio (scaduto). Per far sparire la barra: aprire il calendario, selezionare l'evento, spuntare "Risolto".
- **Popup errore DB** (v4.2): `MostraMessaggioConPopup()` in Home.razor mostra, oltre all'alert standard, una modale con OK per errori critici (salvataggio, connessione). Variabili: `mostraPopupErroreDb`, `messaggioPopupErroreDb`.
- **NumeroTicket** (v4.3): campo `NVARCHAR(200) NULL` in `Attivita`. Affianca il campo URL Ticket nel form (col-md-4). Tooltip: "Inserire il numero del ticket, usare la virgola se sono più di uno". Incluso nella ricerca full-text. Copiato correttamente in `DuplicaAttivita()`.
- **UrlPatchRilasci** (v4.4): nessun `[MaxLength]` — colonna `NVARCHAR(MAX)` nel DB per supportare testi con URL multipli lunghi.
- **DataDismissione** (v4.4): campo `DATETIME NULL` in `Ambienti`. Obbligatorio quando `Attivo = false`. Mostrato nella griglia e nel form del `CongelatiEditorModal`. Incluso in `VecchioValore`/`NuovoValore` del log `Ambienti_Log`.
- **Griglia attività — colonna Patch**: mostra ✔ verde se `att.UrlPatchRilasci` non è vuoto. Nessuna query aggiuntiva (campo già nel DTO).
- **Griglia attività — colonna Ambienti**: mostra i nomi degli ambienti di rilascio compilati, separati da virgola (es. "Test, Produzione"), senza versione. Calcolato in `ActivityService.GetActivitiesAsync()` tramite query batch su `AttivitaAmbientiRilascio` e salvato in `WorkActivityDto.AmbientiRilascioNomi`.
- Versione attuale: **4.4** (aggiornare in `appsettings.json` a ogni release).
