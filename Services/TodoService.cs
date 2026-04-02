using Microsoft.EntityFrameworkCore;
using WorkActivityTracker.Data;
using WorkActivityTracker.Models;

namespace WorkActivityTracker.Services;

/// <summary>
/// Servizio per la gestione della TODO List.
/// Ogni utente ha la propria lista di TODO.
/// </summary>
public class TodoService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly UserService _userService;

    public TodoService(IDbContextFactory<AppDbContext> contextFactory, UserService userService)
    {
        _contextFactory = contextFactory;
        _userService = userService;
    }

    /// <summary>
    /// Recupera tutti i TODO dell'utente corrente.
    /// </summary>
    /// <param name="includiCompletati">Se true, include anche i TODO completati</param>
    /// <returns>Lista dei TODO ordinata per urgenza decrescente e data inserimento crescente</returns>
    public async Task<List<TodoItemDto>> GetTodosAsync(bool includiCompletati = false)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var query = context.TodoItems
            .Where(t => t.UtenteId == currentUser.Id);

        if (!includiCompletati)
        {
            query = query.Where(t => !t.Completato);
        }

        var results = await query
            .OrderByDescending(t => t.LivelloUrgenza)
            .ThenBy(t => t.DataInserimento)
            .ToListAsync();

        return results.Select(t => new TodoItemDto
        {
            Id = t.Id,
            UtenteId = t.UtenteId,
            Titolo = t.Titolo,
            DataInserimento = t.DataInserimento,
            Descrizione = t.Descrizione,
            DataCompletamento = t.DataCompletamento,
            LivelloUrgenza = t.LivelloUrgenza,
            Completato = t.Completato
        }).ToList();
    }

    /// <summary>
    /// Conta i TODO non completati dell'utente corrente.
    /// </summary>
    public async Task<int> GetTodoCountAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        return await context.TodoItems
            .Where(t => t.UtenteId == currentUser.Id && !t.Completato)
            .CountAsync();
    }

    /// <summary>
    /// Aggiunge un nuovo TODO.
    /// </summary>
    public async Task<TodoItemDto> AddTodoAsync(TodoItemDto dto)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var todo = new TodoItem
        {
            UtenteId = currentUser.Id,
            Titolo = dto.Titolo,
            DataInserimento = dto.DataInserimento,
            Descrizione = dto.Descrizione,
            LivelloUrgenza = dto.LivelloUrgenza,
            Completato = false
        };

        context.TodoItems.Add(todo);
        await context.SaveChangesAsync();

        dto.Id = todo.Id;
        dto.UtenteId = todo.UtenteId;

        return dto;
    }

    /// <summary>
    /// Aggiorna un TODO esistente.
    /// </summary>
    public async Task<TodoItemDto> UpdateTodoAsync(TodoItemDto dto)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var todo = await context.TodoItems
            .FirstOrDefaultAsync(t => t.Id == dto.Id && t.UtenteId == currentUser.Id);

        if (todo == null)
            throw new InvalidOperationException("TODO non trovato o non autorizzato alla modifica");

        todo.Titolo = dto.Titolo;
        todo.DataInserimento = dto.DataInserimento;
        todo.Descrizione = dto.Descrizione;
        todo.LivelloUrgenza = dto.LivelloUrgenza;
        todo.Completato = dto.Completato;

        // Imposta la data di completamento se è stato completato
        if (dto.Completato && !todo.DataCompletamento.HasValue)
        {
            todo.DataCompletamento = DateTime.Now;
        }
        else if (!dto.Completato)
        {
            todo.DataCompletamento = null;
        }

        await context.SaveChangesAsync();

        dto.DataCompletamento = todo.DataCompletamento;
        return dto;
    }

    /// <summary>
    /// Segna un TODO come completato o non completato.
    /// </summary>
    public async Task ToggleCompletedAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var todo = await context.TodoItems
            .FirstOrDefaultAsync(t => t.Id == id && t.UtenteId == currentUser.Id);

        if (todo == null)
            throw new InvalidOperationException("TODO non trovato");

        todo.Completato = !todo.Completato;
        todo.DataCompletamento = todo.Completato ? DateTime.Now : null;

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Elimina un TODO.
    /// </summary>
    public async Task DeleteTodoAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var todo = await context.TodoItems
            .FirstOrDefaultAsync(t => t.Id == id && t.UtenteId == currentUser.Id);

        if (todo != null)
        {
            context.TodoItems.Remove(todo);
            await context.SaveChangesAsync();
        }
    }
}
