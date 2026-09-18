using Microsoft.EntityFrameworkCore;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

public sealed class AssistantMovieCatalogue : IAssistantMovieCatalogue
{
    private readonly AppDbContext _db;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorSearchEngine _vectorSearchEngine;

    public AssistantMovieCatalogue(
        AppDbContext db,
        IEmbeddingService embeddingService,
        IVectorSearchEngine vectorSearchEngine)
    {
        _db = db;
        _embeddingService = embeddingService;
        _vectorSearchEngine = vectorSearchEngine;
    }

    public async Task<IReadOnlyList<AssistantMovieCandidateDto>> GetCandidatesAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Movies.AsNoTracking()
            .Where(movie => movie.Status != "Inactive")
            .OrderByDescending(movie => movie.ReleaseDate)
            .Take(100)
            .Select(movie => new AssistantMovieCandidateDto
            {
                Id = movie.Id,
                Title = movie.Title,
                Description = movie.Description,
                Duration = movie.Duration,
                ReleaseDate = movie.ReleaseDate,
                Language = movie.Language,
                Rating = movie.Rating,
                PosterUrl = movie.PosterUrl,
                Status = movie.Status,
                Genres = movie.MovieGenres.Select(item => item.Genre.Name).OrderBy(name => name).ToList()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AssistantMovieCandidateDto>> SearchBySemanticQueryAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        var queryEmbedding = await _embeddingService.EmbedAsync(query, cancellationToken);
        if (queryEmbedding.Length == 0)
            return await GetCandidatesAsync(cancellationToken);

        var movieEmbeddings = await _db.MovieEmbeddings
            .AsNoTracking()
            .Include(e => e.Movie)
            .Where(e => e.Movie.Status != "Inactive"
                && e.Embedding.Length > 0
                && !_db.MovieEmbeddingSyncStates.Any(state => state.MovieId == e.MovieId))
            .ToListAsync(cancellationToken);

        var candidates = movieEmbeddings.Select(e => new VectorSearchCandidate
        {
            Id = e.MovieId,
            Embedding = e.Embedding,
            Metadata = e.Movie.Title
        }).ToList();

        var searchResults = _vectorSearchEngine.Search(queryEmbedding, candidates, topK);

        var movieIds = searchResults.Select(r => r.Id).ToList();
        var movies = await _db.Movies.AsNoTracking()
            .Where(m => movieIds.Contains(m.Id))
            .Select(movie => new AssistantMovieCandidateDto
            {
                Id = movie.Id,
                Title = movie.Title,
                Description = movie.Description,
                Duration = movie.Duration,
                ReleaseDate = movie.ReleaseDate,
                Language = movie.Language,
                Rating = movie.Rating,
                PosterUrl = movie.PosterUrl,
                Status = movie.Status,
                Genres = movie.MovieGenres.Select(item => item.Genre.Name).OrderBy(name => name).ToList()
            })
            .ToListAsync(cancellationToken);

        return movies.OrderBy(m => movieIds.IndexOf(m.Id)).ToList();
    }

    public async Task<IReadOnlyList<AssistantKnowledgeDocumentDto>> SearchKnowledgeDocumentsAsync(
        string query,
        int topK = 3,
        CancellationToken cancellationToken = default)
    {
        var queryEmbedding = await _embeddingService.EmbedAsync(query, cancellationToken);
        if (queryEmbedding.Length == 0)
            return [];

        var documentEmbeddings = await _db.KnowledgeDocuments
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var candidates = documentEmbeddings.Select(d => new VectorSearchCandidate
        {
            Id = d.Id,
            Embedding = d.Embedding,
            Metadata = d.Title
        }).ToList();

        var searchResults = _vectorSearchEngine.Search(queryEmbedding, candidates, topK);

        var docIds = searchResults.Select(r => r.Id).ToList();
        var docs = await _db.KnowledgeDocuments.AsNoTracking()
            .Where(d => docIds.Contains(d.Id))
            .Select(d => new AssistantKnowledgeDocumentDto
            {
                Id = d.Id,
                Title = d.Title,
                Category = d.Category,
                Content = d.Content
            })
            .ToListAsync(cancellationToken);

        return docs.OrderBy(d => docIds.IndexOf(d.Id)).ToList();
    }
}
