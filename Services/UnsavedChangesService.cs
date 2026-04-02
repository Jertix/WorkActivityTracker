namespace WorkActivityTracker.Services;

/// <summary>
/// Servizio singleton per tracciare se ci sono modifiche non salvate nel form principale.
/// Usato per avvisare l'utente prima di chiudere l'applicazione.
/// </summary>
public class UnsavedChangesService
{
    /// <summary>
    /// True se ci sono modifiche non salvate nel form principale.
    /// </summary>
    public bool HasUnsavedChanges { get; set; } = false;
}
