using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieBooking.Application.Common.Configuration;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Constants;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services.Assistant;

public sealed class EmbeddingSyncService : IEmbeddingSyncService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> MovieLocks = new();

    private readonly AppDbContext _db;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<EmbeddingSyncService> _logger;
    private readonly MovieEmbeddingRetryOptions _retryOptions;
    private readonly TimeProvider _timeProvider;

    public EmbeddingSyncService(
        AppDbContext db,
        IEmbeddingService embeddingService,
        ILogger<EmbeddingSyncService> logger,
        IOptions<MovieEmbeddingRetryOptions> retryOptions,
        TimeProvider timeProvider)
    {
        _db = db;
        _embeddingService = embeddingService;
        _logger = logger;
        _retryOptions = retryOptions.Value;
        _timeProvider = timeProvider;
    }

    public async Task SyncMovieEmbeddingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var activeMovieIds = await _db.Movies
                .AsNoTracking()
                .Where(m => m.Status != "Inactive")
                .Select(m => m.Id)
                .ToListAsync(cancellationToken);

            var inactiveMovieIds = await _db.Movies
                .AsNoTracking()
                .Where(m => m.Status == "Inactive")
                .Where(m => _db.MovieEmbeddings.Any(e => e.MovieId == m.Id)
                    || _db.MovieEmbeddingSyncStates.Any(s => s.MovieId == m.Id))
                .Select(m => m.Id)
                .ToListAsync(cancellationToken);

            foreach (var movieId in inactiveMovieIds)
            {
                await RemoveMovieEmbeddingAsync(movieId, cancellationToken);
            }

            foreach (var movieId in activeMovieIds)
            {
                await SyncMovieEmbeddingAsync(movieId, cancellationToken: cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync movie embeddings. Semantic search will fallback to traditional catalogue.");
        }
    }

    public async Task<MovieEmbeddingSyncResult> SyncMovieEmbeddingAsync(
        Guid movieId,
        bool isRetryAttempt = false,
        CancellationToken cancellationToken = default)
    {
        var movieLock = MovieLocks.GetOrAdd(movieId, static _ => new SemaphoreSlim(1, 1));
        await movieLock.WaitAsync(cancellationToken);

        try
        {
            var movie = await LoadMovieAsync(movieId, cancellationToken);
            if (movie is null || movie.Status == "Inactive")
            {
                await RemoveMovieEmbeddingCoreAsync(movieId, cancellationToken);
                return new MovieEmbeddingSyncResult(MovieEmbeddingSyncStatuses.Ready);
            }

            var embeddedText = MovieEmbeddingContentBuilder.Build(movie);
            var contentHash = MovieEmbeddingContentBuilder.ComputeHash(embeddedText);
            var existing = await _db.MovieEmbeddings
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.MovieId == movieId, cancellationToken);

            if (existing is not null
                && existing.Embedding.Length > 0
                && existing.ContentHash == contentHash)
            {
                await RemoveSyncStateAsync(movieId, cancellationToken);
                return new MovieEmbeddingSyncResult(MovieEmbeddingSyncStatuses.Ready, contentHash);
            }

            try
            {
                await MarkPendingAsync(movieId, contentHash, isRetryAttempt, cancellationToken);
                var embedding = await _embeddingService.EmbedAsync(embeddedText, cancellationToken);
                if (embedding.Length == 0)
                {
                    throw new InvalidOperationException("Embedding provider returned an empty vector.");
                }

                var currentMovie = await LoadMovieAsync(movieId, cancellationToken);
                if (currentMovie is null || currentMovie.Status == "Inactive")
                {
                    await RemoveMovieEmbeddingCoreAsync(movieId, cancellationToken);
                    return new MovieEmbeddingSyncResult(MovieEmbeddingSyncStatuses.Ready);
                }

                var currentText = MovieEmbeddingContentBuilder.Build(currentMovie);
                var currentHash = MovieEmbeddingContentBuilder.ComputeHash(currentText);
                if (currentHash != contentHash)
                {
                    const string staleMessage = "Movie content changed while its embedding was being generated.";
                    await RecordFailureAsync(movieId, currentHash, staleMessage, false, cancellationToken);
                    _logger.LogInformation(
                        "Discarded stale embedding result for movie {MovieId}; current hash is {ContentHash}.",
                        movieId,
                        currentHash);
                    return new MovieEmbeddingSyncResult(
                        MovieEmbeddingSyncStatuses.Pending,
                        currentHash,
                        staleMessage);
                }

                var trackedEmbedding = await _db.MovieEmbeddings
                    .FirstOrDefaultAsync(item => item.MovieId == movieId, cancellationToken);
                if (trackedEmbedding is null)
                {
                    trackedEmbedding = new MovieEmbedding { MovieId = movieId };
                    _db.MovieEmbeddings.Add(trackedEmbedding);
                }

                trackedEmbedding.EmbeddedText = currentText;
                trackedEmbedding.ContentHash = currentHash;
                trackedEmbedding.Embedding = embedding;
                trackedEmbedding.LastUpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

                var syncState = await _db.MovieEmbeddingSyncStates
                    .FirstOrDefaultAsync(item => item.MovieId == movieId, cancellationToken);
                if (syncState is not null)
                {
                    _db.MovieEmbeddingSyncStates.Remove(syncState);
                }

                await _db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation(
                    "Movie embedding sync succeeded for {MovieId} with {Dimensions} dimensions and hash {ContentHash}.",
                    movieId,
                    embedding.Length,
                    currentHash);

                return new MovieEmbeddingSyncResult(MovieEmbeddingSyncStatuses.Ready, currentHash);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                DetachFailedAddedEntities();
                var currentMovie = await LoadMovieAsync(movieId, cancellationToken);
                if (currentMovie is null || currentMovie.Status == "Inactive")
                {
                    await RemoveMovieEmbeddingCoreAsync(movieId, cancellationToken);
                    return new MovieEmbeddingSyncResult(MovieEmbeddingSyncStatuses.Ready);
                }

                var currentHash = MovieEmbeddingContentBuilder.ComputeHash(
                    MovieEmbeddingContentBuilder.Build(currentMovie));
                var result = await RecordFailureAsync(
                    movieId,
                    currentHash,
                    exception.Message,
                    isRetryAttempt,
                    cancellationToken);

                _logger.LogWarning(
                    exception,
                    "Movie embedding sync {Status} for {MovieId} at retry attempt {AttemptCount}.",
                    result.Status,
                    movieId,
                    result.AttemptCount);

                return new MovieEmbeddingSyncResult(result.Status, currentHash, result.Error);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            DetachFailedAddedEntities();
            _logger.LogError(
                exception,
                "Movie embedding sync could not persist retry state for {MovieId}; returning Pending.",
                movieId);
            return new MovieEmbeddingSyncResult(
                MovieEmbeddingSyncStatuses.Pending,
                Error: TruncateError(exception.Message));
        }
        finally
        {
            movieLock.Release();
        }
    }

    public async Task RemoveMovieEmbeddingAsync(Guid movieId, CancellationToken cancellationToken = default)
    {
        var movieLock = MovieLocks.GetOrAdd(movieId, static _ => new SemaphoreSlim(1, 1));
        await movieLock.WaitAsync(cancellationToken);
        try
        {
            await RemoveMovieEmbeddingCoreAsync(movieId, cancellationToken);
        }
        finally
        {
            movieLock.Release();
        }
    }

    public async Task SyncKnowledgeDocumentsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var existingTitles = await _db.KnowledgeDocuments
                .Select(d => d.Title)
                .ToHashSetAsync(cancellationToken);

            var faqDocuments = GetFaqDocuments();
            var documentsToAdd = faqDocuments.Where(d => !existingTitles.Contains(d.Title)).ToList();

            if (documentsToAdd.Count == 0) return;

            foreach (var doc in documentsToAdd)
            {
                var embeddingText = $"{doc.Title} {doc.Content}";
                doc.Embedding = await _embeddingService.EmbedAsync(embeddingText, cancellationToken);
            }

            _db.KnowledgeDocuments.AddRange(documentsToAdd);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Seeded {Count} FAQ knowledge documents.", documentsToAdd.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync knowledge documents. FAQ search will be temporarily unavailable.");
        }
    }

    private Task<Movie?> LoadMovieAsync(Guid movieId, CancellationToken cancellationToken)
    {
        return _db.Movies
            .AsNoTracking()
            .Include(movie => movie.MovieGenres)
                .ThenInclude(item => item.Genre)
            .FirstOrDefaultAsync(movie => movie.Id == movieId, cancellationToken);
    }

    private async Task RemoveMovieEmbeddingCoreAsync(Guid movieId, CancellationToken cancellationToken)
    {
        var embedding = await _db.MovieEmbeddings
            .FirstOrDefaultAsync(item => item.MovieId == movieId, cancellationToken);
        if (embedding is not null)
        {
            _db.MovieEmbeddings.Remove(embedding);
        }

        var syncState = await _db.MovieEmbeddingSyncStates
            .FirstOrDefaultAsync(item => item.MovieId == movieId, cancellationToken);
        if (syncState is not null)
        {
            _db.MovieEmbeddingSyncStates.Remove(syncState);
        }

        if (embedding is not null || syncState is not null)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Removed embedding state for movie {MovieId}.", movieId);
        }
    }

    private async Task RemoveSyncStateAsync(Guid movieId, CancellationToken cancellationToken)
    {
        var syncState = await _db.MovieEmbeddingSyncStates
            .FirstOrDefaultAsync(item => item.MovieId == movieId, cancellationToken);
        if (syncState is null) return;

        _db.MovieEmbeddingSyncStates.Remove(syncState);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<FailureRecordResult> RecordFailureAsync(
        Guid movieId,
        string requestedContentHash,
        string error,
        bool isRetryAttempt,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var syncState = await _db.MovieEmbeddingSyncStates
            .FirstOrDefaultAsync(item => item.MovieId == movieId, cancellationToken);

        if (syncState is null)
        {
            syncState = new MovieEmbeddingSyncState { MovieId = movieId };
            _db.MovieEmbeddingSyncStates.Add(syncState);
        }

        syncState.AttemptCount = isRetryAttempt ? syncState.AttemptCount + 1 : 0;
        syncState.LastAttemptAt = now;
        syncState.RequestedContentHash = requestedContentHash;
        syncState.LastError = TruncateError(error);

        var maxAttempts = Math.Max(1, _retryOptions.MaxAttempts);
        if (isRetryAttempt && syncState.AttemptCount >= maxAttempts)
        {
            syncState.Status = MovieEmbeddingSyncStatuses.Failed;
            syncState.NextAttemptAt = null;
        }
        else
        {
            syncState.Status = MovieEmbeddingSyncStatuses.Pending;
            syncState.NextAttemptAt = now.Add(CalculateRetryDelay(syncState.AttemptCount));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new FailureRecordResult(syncState.Status, syncState.AttemptCount, syncState.LastError);
    }

    private async Task MarkPendingAsync(
        Guid movieId,
        string requestedContentHash,
        bool isRetryAttempt,
        CancellationToken cancellationToken)
    {
        var syncState = await _db.MovieEmbeddingSyncStates
            .FirstOrDefaultAsync(item => item.MovieId == movieId, cancellationToken);
        if (syncState is null)
        {
            syncState = new MovieEmbeddingSyncState { MovieId = movieId };
            _db.MovieEmbeddingSyncStates.Add(syncState);
        }

        if (!isRetryAttempt)
        {
            syncState.AttemptCount = 0;
        }

        syncState.Status = MovieEmbeddingSyncStatuses.Pending;
        syncState.RequestedContentHash = requestedContentHash;
        syncState.LastError = string.Empty;
        syncState.NextAttemptAt = _timeProvider.GetUtcNow().Add(CalculateRetryDelay(syncState.AttemptCount));
        await _db.SaveChangesAsync(cancellationToken);
    }

    private TimeSpan CalculateRetryDelay(int attemptCount)
    {
        var baseDelaySeconds = Math.Max(1, _retryOptions.BaseDelaySeconds);
        var maxDelaySeconds = Math.Max(baseDelaySeconds, _retryOptions.MaxDelaySeconds);
        var exponent = Math.Clamp(attemptCount, 0, 20);
        var delaySeconds = Math.Min(maxDelaySeconds, baseDelaySeconds * Math.Pow(2, exponent));
        return TimeSpan.FromSeconds(delaySeconds);
    }

    private static string TruncateError(string error)
    {
        var normalized = string.IsNullOrWhiteSpace(error) ? "Embedding provider failed." : error.Trim();
        return normalized.Length <= 512 ? normalized : normalized[..512];
    }

    private void DetachFailedAddedEntities()
    {
        var failedEntries = _db.ChangeTracker.Entries()
            .Where(entry => entry.State == EntityState.Added
                && (entry.Entity is MovieEmbedding || entry.Entity is MovieEmbeddingSyncState))
            .ToList();
        foreach (var entry in failedEntries)
        {
            entry.State = EntityState.Detached;
        }
    }

    private sealed record FailureRecordResult(string Status, int AttemptCount, string Error);

    private static List<KnowledgeDocument> GetFaqDocuments()
    {
        return
        [
            new KnowledgeDocument
            {
                Title = "Quy định độ tuổi xem phim",
                Category = "Policy",
                Content = """
                    Quy định độ tuổi xem phim tại rạp:
                    - P (Phổ thông): Phim được mọi lứa tuổi xem.
                    - K (Kids): Phim dành cho trẻ em dưới 13 tuổi cần có người lớn đi kèm.
                    - T13: Phim dành cho khán giả từ 13 tuổi trở lên.
                    - T16: Phim dành cho khán giả từ 16 tuổi trở lên.
                    - T18: Phim dành cho khán giả từ 18 tuổi trở lên.
                    Nhân viên có quyền yêu cầu khách hàng xuất trình CMND/CCCD để xác nhận độ tuổi.
                    """
            },
            new KnowledgeDocument
            {
                Title = "Chính sách vé học sinh sinh viên",
                Category = "Policy",
                Content = """
                    Chính sách vé ưu đãi:
                    - Vé học sinh/sinh viên: Giảm giá so với vé thường, áp dụng cho suất chiếu trước 17:00 hàng ngày.
                    - Yêu cầu: Xuất trình thẻ học sinh/sinh viên hợp lệ khi mua vé và vào phòng chiếu.
                    - Vé trẻ em: Áp dụng cho trẻ em dưới 1m3身高, giá ưu đãi hơn vé người lớn.
                    - Không áp dụng đồng thời với các chương trình khuyến mãi khác.
                    """
            },
            new KnowledgeDocument
            {
                Title = "Quy định mang thức ăn nước uống",
                Category = "Policy",
                Content = """
                    Quy định về thức ăn và đồ uống:
                    - Không được mang thức ăn và đồ uống từ ngoài vào phòng chiếu.
                    - Khách hàng có thể mua thức ăn và đồ uống tại quầy bar của rạp.
                    - Nước uống đóng chai và bỏng ngô là các sản phẩm phổ biến nhất tại rạp.
                    - Việc tuân thủ quy định giúp giữ gìn vệ sinh chung và trải nghiệm tốt nhất cho tất cả khán giả.
                    """
            },
            new KnowledgeDocument
            {
                Title = "Chính sách đổi trả vé",
                Category = "Policy",
                Content = """
                    Chính sách đổi trả vé:
                    - Vé đã mua có thể được đổi sang suất chiếu khác trước giờ chiếu 60 phút.
                    - Phí đổi vé: 10.000 VNĐ/vé.
                    - Hoàn tiền chỉ áp dụng khi rạp hủy suất chiếu, hoàn 100% giá vé.
                    - Không hoàn tiền trong trường hợp khách hàng không đến xem.
                    - Vé mua online có thể đổi/trả qua ứng dụng hoặc tại quầy vé.
                    """
            },
            new KnowledgeDocument
            {
                Title = "Tiện ích phòng chiếu đặc biệt",
                Category = "Facility",
                Content = """
                    Các loại phòng chiếu đặc biệt:
                    - IMAX: Màn hình khổng lồ, âm thanh vòm 12 kênh, trải nghiệm điện ảnh đỉnh cao.
                    - 3D: Công nghệ hình ảnh 3 chiều, kính 3D được cung cấp tại rạp.
                    - 2D: Phòng chiếu tiêu chuẩn với màn hình và âm thanh chất lượng cao.
                    - Dolby Atmos: Hệ thống âm thanh vòm tiên tiến, mang đến trải nghiệm âm thanh 360 độ.
                    - Gold Class: Phòng chiếu cao cấp với ghế da sang trọng, phục vụ đồ ăn và đồ uống tại chỗ.
                    """
            },
            new KnowledgeDocument
            {
                Title = "Giá vé và giờ chiếu",
                Category = "Pricing",
                Content = """
                    Thông tin về giá vé:
                    - Giá vé thay đổi tùy theo loại phòng chiếu, khung giờ và ngày trong tuần.
                    - Suất chiếu buổi sáng (trước 12:00): Giá ưu đãi nhất.
                    - Suất chiếu buổi chiều (12:00 - 17:00): Giá trung bình.
                    - Suất chiếu buổi tối (sau 17:00): Giá cao nhất.
                    - Cuối ngày lễ, Tết: Giá vé có thể tăng thêm 10-20%.
                    - Xem giá vé chi tiết tại ứng dụng hoặc website của rạp.
                    """
            }
        ];
    }
}
