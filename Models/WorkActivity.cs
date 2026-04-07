using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WorkActivityTracker.Models;

/// <summary>
/// Entità principale che rappresenta un'attività lavorativa registrata nel sistema.
/// Mappata sulla tabella [Attivita] del database.
/// </summary>
[Table("Attivita")]
public class Attivita
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>
    /// ID dell'utente proprietario dell'attività (foreign key verso Utenti)
    /// </summary>
    [Required]
    public int UtenteId { get; set; }
    
    /// <summary>
    /// Tipo di attività: Lavoro (default), Permesso, Ferie
    /// </summary>
    [MaxLength(20)]
    public string TipoAttivita { get; set; } = TipiAttivita.Lavoro;

    /// <summary>
    /// Data in cui è stata svolta l'attività
    /// </summary>
    [Required]
    public DateTime Data { get; set; } = DateTime.Today;
    
    /// <summary>
    /// Descrizione breve dell'attività svolta
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Descrizione { get; set; } = string.Empty;
    
    /// <summary>
    /// URL opzionale al ticket/issue/CR/Backlog di riferimento (può contenere più righe)
    /// </summary>
    public string? UrlTicket { get; set; }

    /// <summary>
    /// Numero/i del ticket (es: "12345" oppure "12345, 67890" se sono più di uno)
    /// </summary>
    [MaxLength(200)]
    public string? NumeroTicket { get; set; }
    
    /// <summary>
    /// ID del cliente associato (foreign key verso Clienti)
    /// </summary>
    public int? ClienteId { get; set; }
    
    /// <summary>
    /// Numero di ore lavorate (supporta decimali, es: 7.5)
    /// </summary>
    [Column(TypeName = "decimal(4,2)")]
    public decimal OreLavorate { get; set; }
    
    /// <summary>
    /// Versione del software su cui si è lavorato
    /// </summary>
    [MaxLength(200)]
    public string? Versione { get; set; }
    
    /// <summary>
    /// Riferimenti esterni (es: "vedere email", "vedere chat Teams")
    /// </summary>
    [MaxLength(500)]
    public string? Vedere { get; set; }
    
    /// <summary>
    /// Descrizione dettagliata del lavoro svolto
    /// </summary>
    public string? Note { get; set; }
    
    /// <summary>
    /// Elenco dei changeset/commit coinvolti nell'attività
    /// </summary>
    public string? ChangesetCoinvolti { get; set; }
    
    /// <summary>
    /// URL della patch o del pacchetto nei rilasci (nessun limite di lunghezza: può contenere più URL lunghi)
    /// </summary>
    public string? UrlPatchRilasci { get; set; }
    
    /// <summary>
    /// Testo descrittivo del check-in (usato per auto-compilare i changeset)
    /// </summary>
    [MaxLength(500)]
    public string? TestoCheckIn { get; set; }
    
    /// <summary>
    /// Path della cartella di documentazione associata all'attività
    /// </summary>
    [MaxLength(1000)]
    public string? CartellaDocumentazione { get; set; }
    
    /// <summary>
    /// Data e ora di creazione del record
    /// </summary>
    public DateTime DataCreazione { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Data e ora dell'ultima modifica
    /// </summary>
    public DateTime DataModifica { get; set; } = DateTime.Now;
    
    // Navigation properties
    [ForeignKey("UtenteId")]
    public virtual Utente? Utente { get; set; }
    
    [ForeignKey("ClienteId")]
    public virtual Cliente? Cliente { get; set; }
    
    /// <summary>
    /// Collezione dei congelati associati a questa attività (relazione N:N)
    /// </summary>
    public virtual ICollection<AttivitaAmbiente> AttivitaAmbienti { get; set; } = new List<AttivitaAmbiente>();
}

/// <summary>
/// Rappresenta un utente del sistema, identificato dal suo Windows Username.
/// Gli utenti vengono creati automaticamente al primo accesso.
/// </summary>
[Table("Utenti")]
public class Utente
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>
    /// Username Windows dell'utente (es: "mario.rossi")
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string WindowsUsername { get; set; } = string.Empty;
    
    /// <summary>
    /// Nome completo dell'utente (opzionale)
    /// </summary>
    [MaxLength(200)]
    public string? NomeCompleto { get; set; }
    
    [MaxLength(200)]
    public string? Email { get; set; }
    
    public DateTime DataPrimoAccesso { get; set; } = DateTime.Now;
    public DateTime UltimoAccesso { get; set; } = DateTime.Now;
    
    public bool Attivo { get; set; } = true;

    /// <summary>
    /// Se true, le attività di questo utente non sono visibili ad altri in modalità Admin.
    /// Il valore viene sincronizzato all'avvio dall'appsettings.json dell'utente.
    /// </summary>
    public bool PrivacyMode { get; set; } = false;

    public virtual ICollection<Attivita> Attivita { get; set; } = new List<Attivita>();
}

/// <summary>
/// Rappresenta un cliente per cui si lavora (es: Cattolica, Aviva, Axa)
/// </summary>
[Table("Clienti")]
public class Cliente
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;
    
    public bool Attivo { get; set; } = true;
}

/// <summary>
/// Rappresenta un ambiente congelato (es: VirtualXL3250 - Versione 3.2.50)
/// </summary>
[Table("Ambienti")]
public class Ambiente
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>
    /// Nome del congelato (es: VirtualXL3250)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Codice { get; set; } = string.Empty;
    
    /// <summary>
    /// Versione del congelato (es: 3.2.50)
    /// </summary>
    [MaxLength(100)]
    public string? Descrizione { get; set; }
    
    /// <summary>
    /// Data di congelamento dell'ambiente
    /// </summary>
    public DateTime? DataCongelamento { get; set; }

    /// <summary>
    /// Data di dismissione del congelato (obbligatoria quando Attivo = false)
    /// </summary>
    public DateTime? DataDismissione { get; set; }

    public bool Attivo { get; set; } = true;
}

/// <summary>
/// Log delle modifiche agli ambienti/congelati per tracciabilità
/// </summary>
[Table("Ambienti_Log")]
public class AmbienteLog
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>
    /// Nome utente che ha eseguito l'azione
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string NomeUtente { get; set; } = string.Empty;
    
    /// <summary>
    /// Tipo di azione: Nuovo, Modifica, Elimina
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string AzioneSvolta { get; set; } = string.Empty;
    
    /// <summary>
    /// Codice dell'ambiente coinvolto
    /// </summary>
    [MaxLength(50)]
    public string? Codice { get; set; }
    
    /// <summary>
    /// Descrizione/Versione dell'ambiente coinvolto
    /// </summary>
    [MaxLength(100)]
    public string? Descrizione { get; set; }
    
    /// <summary>
    /// Data di congelamento dell'ambiente (se presente)
    /// </summary>
    public DateTime? DataCongelamento { get; set; }
    
    /// <summary>
    /// Timestamp dell'azione
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// Valore precedente alla modifica (serializzato come stringa formattata)
    /// Usato solo per azione "Modifica"
    /// </summary>
    public string? VecchioValore { get; set; }

    /// <summary>
    /// Nuovo valore dopo la modifica (serializzato come stringa formattata)
    /// Usato solo per azione "Modifica"
    /// </summary>
    public string? NuovoValore { get; set; }
}

/// <summary>
/// Tabella ponte per la relazione N:N tra Attivita e Ambienti (congelati).
/// Permette di associare più congelati a una singola attività.
/// </summary>
[Table("AttivitaAmbienti")]
public class AttivitaAmbiente
{
    public int AttivitaId { get; set; }
    public int AmbienteId { get; set; }
    
    [ForeignKey("AttivitaId")]
    public virtual Attivita? Attivita { get; set; }
    
    [ForeignKey("AmbienteId")]
    public virtual Ambiente? Ambiente { get; set; }
}

/// <summary>
/// Rappresenta un elemento della TODO List per tracciare attività da completare.
/// </summary>
[Table("TodoItems")]
public class TodoItem
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>
    /// ID dell'utente proprietario del TODO (foreign key verso Utenti)
    /// </summary>
    [Required]
    public int UtenteId { get; set; }
    
    /// <summary>
    /// Titolo breve del TODO
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Titolo { get; set; } = string.Empty;
    
    /// <summary>
    /// Data di inserimento del TODO (modificabile dall'utente)
    /// </summary>
    [Required]
    public DateTime DataInserimento { get; set; } = DateTime.Today;
    
    /// <summary>
    /// Descrizione dettagliata del TODO
    /// </summary>
    [Required]
    public string Descrizione { get; set; } = string.Empty;
    
    /// <summary>
    /// Data di completamento (null se non completato)
    /// </summary>
    public DateTime? DataCompletamento { get; set; }
    
    /// <summary>
    /// Livello di urgenza da 1 (basso) a 5 (critico)
    /// </summary>
    [Range(1, 5)]
    public int LivelloUrgenza { get; set; } = 3;
    
    /// <summary>
    /// Indica se il TODO è stato completato
    /// </summary>
    public bool Completato { get; set; } = false;
    
    // Navigation property
    [ForeignKey("UtenteId")]
    public virtual Utente? Utente { get; set; }
}

/// <summary>
/// DTO per TodoItem
/// </summary>
public class TodoItemDto
{
    public int Id { get; set; }
    public int UtenteId { get; set; }
    public string Titolo { get; set; } = string.Empty;
    public DateTime DataInserimento { get; set; } = DateTime.Today;
    public string Descrizione { get; set; } = string.Empty;
    public DateTime? DataCompletamento { get; set; }
    public int LivelloUrgenza { get; set; } = 3;
    public bool Completato { get; set; } = false;
}

/// <summary>
/// Rappresenta un appunto/nota da ricordare (Knowledge Base personale).
/// </summary>
[Table("Appunti")]
public class AppuntoItem
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>ID dell'utente proprietario (FK verso Utenti)</summary>
    [Required]
    public int UtenteId { get; set; }
    
    /// <summary>Titolo breve dell'appunto</summary>
    [Required]
    [MaxLength(300)]
    public string Titolo { get; set; } = string.Empty;
    
    /// <summary>Descrizione dettagliata dell'appunto</summary>
    [Required]
    public string Descrizione { get; set; } = string.Empty;
    
    /// <summary>Data di creazione del record</summary>
    public DateTime DataCreazione { get; set; } = DateTime.Now;
    
    /// <summary>Data dell'ultima modifica</summary>
    public DateTime DataModifica { get; set; } = DateTime.Now;
    
    // Navigation properties
    [ForeignKey("UtenteId")]
    public virtual Utente? Utente { get; set; }
    
    public virtual ICollection<AppuntoTag> Tags { get; set; } = new List<AppuntoTag>();
}

/// <summary>
/// Rappresenta un tag associato ad un appunto per la categorizzazione e ricerca.
/// </summary>
[Table("AppuntiTags")]
public class AppuntoTag
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>ID dell'appunto associato (FK verso Appunti)</summary>
    [Required]
    public int AppuntoId { get; set; }
    
    /// <summary>Nome del tag (es: "IIS", "web.config", "deploy")</summary>
    [Required]
    [MaxLength(100)]
    public string NomeTag { get; set; } = string.Empty;
    
    // Navigation property
    [ForeignKey("AppuntoId")]
    public virtual AppuntoItem? Appunto { get; set; }
}

/// <summary>
/// DTO per AppuntoItem
/// </summary>
public class AppuntoItemDto
{
    public int Id { get; set; }
    public int UtenteId { get; set; }
    public string? UtenteUsername { get; set; }
    public string Titolo { get; set; } = string.Empty;
    public string Descrizione { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    /// <summary>Stringa separata da virgole per l'input dei tag nel form</summary>
    public string TagsInput { get; set; } = string.Empty;
    public DateTime DataCreazione { get; set; } = DateTime.Now;
    public DateTime DataModifica { get; set; } = DateTime.Now;
}

/// <summary>
/// DTO per Ambiente/Congelato
/// </summary>
public class AmbienteDto
{
    public int Id { get; set; }
    public string Codice { get; set; } = string.Empty;
    public string? Descrizione { get; set; }
    public DateTime? DataCongelamento { get; set; }
    public DateTime? DataDismissione { get; set; }
    public bool Attivo { get; set; } = true;
}

/// <summary>
/// Tipi di attività supportati dal sistema
/// </summary>
public static class TipiAttivita
{
    public const string Lavoro = "Lavoro";
    public const string Permesso = "Permesso";
    public const string Ferie = "Ferie";

    public static readonly string[] Tutti = { Lavoro, Permesso, Ferie };
}

/// <summary>
/// Log delle modifiche alla lista clienti per tracciabilità
/// </summary>
[Table("Clienti_Log")]
public class ClienteLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string NomeUtente { get; set; } = string.Empty;

    /// <summary>Tipo di azione: Nuovo, Modifica, Elimina</summary>
    [Required]
    [MaxLength(20)]
    public string AzioneSvolta { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? NomeCliente { get; set; }

    public string? VecchioValore { get; set; }

    public string? NuovoValore { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.Now;
}

/// <summary>DTO per Cliente (usato nell'editor clienti)</summary>
public class ClienteDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Attivo { get; set; } = true;
}

/// <summary>
/// Tipi di ambienti di rilascio (es: Test, Qualità, Pre-produzione, Produzione).
/// Lista editabile con log delle modifiche.
/// </summary>
[Table("TipiAmbientiRilascio")]
public class TipoAmbienteRilascio
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    public bool Attivo { get; set; } = true;
}

/// <summary>
/// Log delle modifiche alla lista TipiAmbientiRilascio per tracciabilità.
/// </summary>
[Table("TipiAmbientiRilascio_Log")]
public class TipoAmbienteRilascioLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string NomeUtente { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string AzioneSvolta { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? NomeValore { get; set; }

    public string? VecchioValore { get; set; }

    public string? NuovoValore { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.Now;
}

/// <summary>
/// Versioni di rilascio usate nelle coppie ambiente/versione.
/// Lista editabile con log delle modifiche.
/// </summary>
[Table("VersioniRilascio")]
public class VersioneRilascio
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Versione { get; set; } = string.Empty;

    public bool Attivo { get; set; } = true;
}

/// <summary>
/// Log delle modifiche alla lista VersioniRilascio per tracciabilità.
/// </summary>
[Table("VersioniRilascio_Log")]
public class VersioneRilascioLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string NomeUtente { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string AzioneSvolta { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ValoreVersione { get; set; }

    public string? VecchioValore { get; set; }

    public string? NuovoValore { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.Now;
}

/// <summary>
/// Associazione tra un'attività e le 3 coppie ambiente/versione di rilascio.
/// Usato solo per clienti diversi da "Sviluppo".
/// </summary>
[Table("AttivitaAmbientiRilascio")]
public class AttivitaAmbienteRilascio
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int AttivitaId { get; set; }

    /// <summary>Posizione della coppia (1, 2 o 3)</summary>
    public int Posizione { get; set; }

    /// <summary>Tipo ambiente (es: Test, Qualità, Pre-produzione, Produzione) — null = non selezionato</summary>
    [MaxLength(100)]
    public string? TipoAmbiente { get; set; }

    /// <summary>Versione di rilascio (es: 3.2.50, 4.1.10 (3.2.60)) — null = non selezionata</summary>
    [MaxLength(100)]
    public string? Versione { get; set; }

    [ForeignKey("AttivitaId")]
    public virtual Attivita? Attivita { get; set; }
}

/// <summary>
/// DTO per una coppia ambiente/versione di rilascio nel form.
/// </summary>
public class AmbienteRilascioDto
{
    public int Posizione { get; set; }
    public string? TipoAmbiente { get; set; }
    public string? Versione { get; set; }
}

/// <summary>
/// Segnalazione bug o richiesta di modifica al programma
/// </summary>
[Table("Segnalazioni")]
public class Segnalazione
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UtenteId { get; set; }

    [Required]
    public string TestoSegnalazione { get; set; } = string.Empty;

    public DateTime DataRichiesta { get; set; } = DateTime.Now;

    [MaxLength(50)]
    public string Stato { get; set; } = StatiSegnalazione.InAttesa;

    [ForeignKey("UtenteId")]
    public virtual Utente? Utente { get; set; }

    public virtual ICollection<SegnalazioneRisposta> Risposte { get; set; } = new List<SegnalazioneRisposta>();
}

/// <summary>
/// Risposta a una segnalazione (con eventuale cambio di stato)
/// </summary>
[Table("Segnalazioni_Risposte")]
public class SegnalazioneRisposta
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int SegnalazioneId { get; set; }

    [Required]
    public int UtenteId { get; set; }

    [Required]
    public string TestoRisposta { get; set; } = string.Empty;

    [MaxLength(50)]
    public string NuovoStato { get; set; } = string.Empty;

    public DateTime DataRisposta { get; set; } = DateTime.Now;

    [ForeignKey("SegnalazioneId")]
    public virtual Segnalazione? Segnalazione { get; set; }

    [ForeignKey("UtenteId")]
    public virtual Utente? Utente { get; set; }
}

/// <summary>
/// Stati possibili di una segnalazione
/// </summary>
public static class StatiSegnalazione
{
    public const string InAttesa = "In attesa di risposta";
    public const string Risposta = "Risposta";
    public const string RichiestaAccettata = "Richiesta accettata";
    public const string RichiestaRifiutata = "Richiesta rifiutata";
    public const string ModificaAccettata = "Modifica proposta accettata";
    public const string Risolto = "Risolto";

    public static readonly string[] Tutti = { InAttesa, Risposta, RichiestaAccettata, RichiestaRifiutata, ModificaAccettata, Risolto };
}

/// <summary>
/// Tipo di attività personalizzato (es: Lavoro, Permesso, Ferie + eventuali aggiunte utente)
/// </summary>
[Table("TipiAttivita")]
public class TipoAttivitaItem
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Ordine di visualizzazione nel dropdown</summary>
    public int Ordine { get; set; } = 99;

    public bool Attivo { get; set; } = true;
}

/// <summary>
/// Log delle modifiche alla lista dei tipi di attività per tracciabilità.
/// </summary>
[Table("TipiAttivita_Log")]
public class TipoAttivitaLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string NomeUtente { get; set; } = string.Empty;

    /// <summary>Tipo di azione: Nuovo, Modifica, Elimina</summary>
    [Required]
    [MaxLength(20)]
    public string AzioneSvolta { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? NomeValore { get; set; }

    public string? VecchioValore { get; set; }

    public string? NuovoValore { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.Now;
}

/// <summary>
/// Evento del calendario con avviso configurabile
/// </summary>
[Table("EventiCalendario")]
public class EventoCalendario
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UtenteId { get; set; }

    [Required]
    [MaxLength(300)]
    public string Descrizione { get; set; } = string.Empty;

    /// <summary>Data e ora dell'evento</summary>
    [Required]
    public DateTime DataEvento { get; set; }

    /// <summary>Quanti giorni prima dell'evento mostrare la barra di avviso</summary>
    public int GiorniPrimaAvviso { get; set; } = 5;

    /// <summary>True quando l'evento è stato risolto/gestito manualmente dall'utente</summary>
    public bool Risolto { get; set; } = false;

    public DateTime DataCreazione { get; set; } = DateTime.Now;

    [ForeignKey("UtenteId")]
    public virtual Utente? Utente { get; set; }
}

/// <summary>DTO per EventoCalendario</summary>
public class EventoCalendarioDto
{
    public int Id { get; set; }
    public int UtenteId { get; set; }
    public string Descrizione { get; set; } = string.Empty;
    public DateTime DataEvento { get; set; }
    public int GiorniPrimaAvviso { get; set; } = 5;
    public bool Risolto { get; set; } = false;

    /// <summary>Giorni rimanenti all'evento (negativo = già passato)</summary>
    public double GiorniRimanenti => (DataEvento.Date - DateTime.Today).TotalDays;
}

/// <summary>DTO per Segnalazione</summary>
public class SegnalazioneDto
{
    public int Id { get; set; }
    public int UtenteId { get; set; }
    public string UtenteUsername { get; set; } = string.Empty;
    public string TestoSegnalazione { get; set; } = string.Empty;
    public DateTime DataRichiesta { get; set; }
    public string Stato { get; set; } = StatiSegnalazione.InAttesa;
    public List<SegnalazioneRispostaDto> Risposte { get; set; } = new();
}

/// <summary>DTO per SegnalazioneRisposta</summary>
public class SegnalazioneRispostaDto
{
    public int Id { get; set; }
    public int SegnalazioneId { get; set; }
    public int UtenteId { get; set; }
    public string UtenteUsername { get; set; } = string.Empty;
    public string TestoRisposta { get; set; } = string.Empty;
    public string NuovoStato { get; set; } = string.Empty;
    public DateTime DataRisposta { get; set; }
}

/// <summary>
/// DTO (Data Transfer Object) per la comunicazione tra UI e Service layer.
/// Contiene una rappresentazione "piatta" dell'attività con dati già risolti.
/// </summary>
public class WorkActivityDto
{
    public int Id { get; set; }
    public DateTime Data { get; set; } = DateTime.Today;

    /// <summary>
    /// Tipo di attività: Lavoro (default), Permesso, Ferie
    /// </summary>
    public string TipoAttivita { get; set; } = TipiAttivita.Lavoro;
    public string Descrizione { get; set; } = string.Empty;
    public string? UrlTicket { get; set; }

    /// <summary>
    /// Numero/i del ticket (es: "12345" oppure "12345, 67890" se sono più di uno)
    /// </summary>
    public string? NumeroTicket { get; set; }

    public int? ClienteId { get; set; }
    public string? ClienteNome { get; set; }
    public decimal OreLavorate { get; set; }
    public string? Versione { get; set; }
    public List<int> AmbientiSelezionatiIds { get; set; } = new();
    public string? Vedere { get; set; }
    public string? Note { get; set; }
    public string? ChangesetCoinvolti { get; set; }
    
    /// <summary>
    /// URL della patch o del pacchetto nei rilasci
    /// </summary>
    public string? UrlPatchRilasci { get; set; }
    
    /// <summary>
    /// Testo descrittivo del check-in
    /// </summary>
    public string? TestoCheckIn { get; set; }
    
    /// <summary>
    /// Path della cartella di documentazione
    /// </summary>
    public string? CartellaDocumentazione { get; set; }
    
    /// <summary>
    /// Username dell'utente proprietario (usato in modalità admin)
    /// </summary>
    public string? UtenteUsername { get; set; }
    
    /// <summary>
    /// ID dell'utente proprietario
    /// </summary>
    public int UtenteId { get; set; }

    /// <summary>
    /// Le 3 coppie ambiente/versione di rilascio (visibili solo per cliente != Sviluppo)
    /// </summary>
    public List<AmbienteRilascioDto> AmbientiRilascio { get; set; } = new()
    {
        new AmbienteRilascioDto { Posizione = 1 },
        new AmbienteRilascioDto { Posizione = 2 },
        new AmbienteRilascioDto { Posizione = 3 }
    };

    /// <summary>
    /// Nomi degli ambienti di rilascio compilati, separati da virgola (per la colonna griglia)
    /// </summary>
    public string? AmbientiRilascioNomi { get; set; }
}

/// <summary>
/// Voce dello storico versioni di un campo editor (Note o ChangesetCoinvolti).
/// Ogni salvataggio di un'attività produce una riga in questa tabella.
/// </summary>
[Table("EditorHistory")]
public class EditorHistoryEntry
{
    [Key]
    public int Id { get; set; }

    /// <summary>ID dell'attività a cui appartiene questa voce di storico</summary>
    [Required]
    public int AttivitaId { get; set; }

    /// <summary>ID dell'utente che ha eseguito il salvataggio</summary>
    [Required]
    public int UtenteId { get; set; }

    /// <summary>Nome del campo: 'Note' oppure 'ChangesetCoinvolti'</summary>
    [Required]
    [MaxLength(20)]
    public string Campo { get; set; } = string.Empty;

    /// <summary>Contenuto HTML del campo al momento del salvataggio</summary>
    public string? Contenuto { get; set; }

    /// <summary>Data e ora del salvataggio</summary>
    public DateTime DataSalvataggio { get; set; } = DateTime.Now;
}
