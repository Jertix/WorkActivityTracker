using Microsoft.EntityFrameworkCore;
using WorkActivityTracker.Models;

namespace WorkActivityTracker.Data;

/// <summary>
/// Contesto Entity Framework Core per l'accesso al database WorkActivityTracker.
/// Configura le entità e le relazioni del modello dati.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    /// <summary>Tabella delle attività lavorative</summary>
    public DbSet<Attivita> Attivita { get; set; }
    
    /// <summary>Tabella degli utenti (basati su Windows Login)</summary>
    public DbSet<Utente> Utenti { get; set; }
    
    /// <summary>Tabella dei clienti</summary>
    public DbSet<Cliente> Clienti { get; set; }
    
    /// <summary>Tabella degli ambienti di lavoro</summary>
    public DbSet<Ambiente> Ambienti { get; set; }
    
    /// <summary>Tabella ponte per la relazione N:N tra Attività e Ambienti</summary>
    public DbSet<AttivitaAmbiente> AttivitaAmbienti { get; set; }
    
    /// <summary>Tabella TODO List</summary>
    public DbSet<TodoItem> TodoItems { get; set; }
    
    /// <summary>Tabella log modifiche ambienti/congelati</summary>
    public DbSet<AmbienteLog> AmbientiLog { get; set; }
    
    /// <summary>Tabella Appunti (Knowledge Base)</summary>
    public DbSet<AppuntoItem> Appunti { get; set; }
    
    /// <summary>Tabella Tags degli Appunti</summary>
    public DbSet<AppuntoTag> AppuntiTags { get; set; }

    /// <summary>Tabella log modifiche clienti</summary>
    public DbSet<ClienteLog> ClientiLog { get; set; }

    /// <summary>Tabella segnalazioni bug/richieste</summary>
    public DbSet<Segnalazione> Segnalazioni { get; set; }

    /// <summary>Tabella risposte alle segnalazioni</summary>
    public DbSet<SegnalazioneRisposta> SegnalazioniRisposte { get; set; }

    /// <summary>Tabella tipi ambienti di rilascio (es: Test, Qualità, Pre-produzione, Produzione)</summary>
    public DbSet<TipoAmbienteRilascio> TipiAmbientiRilascio { get; set; }

    /// <summary>Tabella log modifiche tipi ambienti rilascio</summary>
    public DbSet<TipoAmbienteRilascioLog> TipiAmbientiRilascioLog { get; set; }

    /// <summary>Tabella versioni di rilascio</summary>
    public DbSet<VersioneRilascio> VersioniRilascio { get; set; }

    /// <summary>Tabella log modifiche versioni rilascio</summary>
    public DbSet<VersioneRilascioLog> VersioniRilascioLog { get; set; }

    /// <summary>Tabella coppie ambiente/versione associate alle attività</summary>
    public DbSet<AttivitaAmbienteRilascio> AttivitaAmbientiRilascio { get; set; }

    /// <summary>Tabella tipi attività personalizzati (Lavoro, Permesso, Ferie + custom)</summary>
    public DbSet<TipoAttivitaItem> TipiAttivita { get; set; }

    /// <summary>Tabella log modifiche tipi attività</summary>
    public DbSet<TipoAttivitaLog> TipiAttivitaLog { get; set; }

    /// <summary>Tabella eventi del calendario con avviso</summary>
    public DbSet<EventoCalendario> EventiCalendario { get; set; }

    /// <summary>Tabella storico versioni dei campi Note e ChangesetCoinvolti</summary>
    public DbSet<EditorHistoryEntry> EditorHistory { get; set; }

    /// <summary>
    /// Configura il modello EF Core: chiavi, indici, relazioni.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configurazione chiave composita per AttivitaAmbienti (tabella ponte N:N)
        modelBuilder.Entity<AttivitaAmbiente>()
            .HasKey(aa => new { aa.AttivitaId, aa.AmbienteId });

        // Relazione Attivita -> AttivitaAmbienti (1:N con cascade delete)
        modelBuilder.Entity<AttivitaAmbiente>()
            .HasOne(aa => aa.Attivita)
            .WithMany(a => a.AttivitaAmbienti)
            .HasForeignKey(aa => aa.AttivitaId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relazione Ambiente -> AttivitaAmbienti (1:N senza cascade)
        modelBuilder.Entity<AttivitaAmbiente>()
            .HasOne(aa => aa.Ambiente)
            .WithMany()
            .HasForeignKey(aa => aa.AmbienteId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indice unico per WindowsUsername (un utente per login Windows)
        modelBuilder.Entity<Utente>()
            .HasIndex(u => u.WindowsUsername)
            .IsUnique();

        // Indice unico per Nome cliente
        modelBuilder.Entity<Cliente>()
            .HasIndex(c => c.Nome)
            .IsUnique();

        // Indice unico per Codice ambiente
        modelBuilder.Entity<Ambiente>()
            .HasIndex(a => a.Codice)
            .IsUnique();

        // Segnalazioni: relazione con Utente
        modelBuilder.Entity<Segnalazione>()
            .HasOne(s => s.Utente)
            .WithMany()
            .HasForeignKey(s => s.UtenteId)
            .OnDelete(DeleteBehavior.Restrict);

        // SegnalazioniRisposte: relazione con Segnalazione (cascade) e Utente
        modelBuilder.Entity<SegnalazioneRisposta>()
            .HasOne(r => r.Segnalazione)
            .WithMany(s => s.Risposte)
            .HasForeignKey(r => r.SegnalazioneId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SegnalazioneRisposta>()
            .HasOne(r => r.Utente)
            .WithMany()
            .HasForeignKey(r => r.UtenteId)
            .OnDelete(DeleteBehavior.Restrict);

        // AttivitaAmbientiRilascio: relazione con Attivita (cascade delete)
        modelBuilder.Entity<AttivitaAmbienteRilascio>()
            .HasOne(r => r.Attivita)
            .WithMany()
            .HasForeignKey(r => r.AttivitaId)
            .OnDelete(DeleteBehavior.Cascade);

        // TipiAttivita: indice unico sul nome
        modelBuilder.Entity<TipoAttivitaItem>()
            .HasIndex(t => t.Nome)
            .IsUnique();

        // EventiCalendario: relazione con Utente (restrict)
        modelBuilder.Entity<EventoCalendario>()
            .HasOne(e => e.Utente)
            .WithMany()
            .HasForeignKey(e => e.UtenteId)
            .OnDelete(DeleteBehavior.Restrict);

        // EditorHistory: default SQL per DataSalvataggio
        modelBuilder.Entity<EditorHistoryEntry>()
            .Property(e => e.DataSalvataggio)
            .HasDefaultValueSql("GETDATE()");
    }
}
