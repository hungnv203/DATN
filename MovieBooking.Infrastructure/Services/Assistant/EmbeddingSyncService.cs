using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services.Assistant;

public sealed class EmbeddingSyncService : IEmbeddingSyncService
{
    private readonly AppDbContext _db;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<EmbeddingSyncService> _logger;

    public EmbeddingSyncService(
        AppDbContext db,
        IEmbeddingService embeddingService,
        ILogger<EmbeddingSyncService> logger)
    {
        _db = db;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task SyncMovieEmbeddingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var movies = await _db.Movies
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .Where(m => m.Status != "Inactive")
                .ToListAsync(cancellationToken);

            var existingEmbeddings = await _db.MovieEmbeddings
                .ToDictionaryAsync(e => e.MovieId, cancellationToken);

            foreach (var movie in movies)
            {
                var embeddedText = BuildMovieEmbeddedText(movie);
                var contentHash = ComputeHash(embeddedText);

                if (existingEmbeddings.TryGetValue(movie.Id, out var existing))
                {
                    if (existing.ContentHash == contentHash) continue;

                    _logger.LogInformation("Re-embedding movie {MovieId} due to content change.", movie.Id);
                    var embedding = await _embeddingService.EmbedAsync(embeddedText, cancellationToken);
                    if (embedding.Length > 0)
                    {
                        existing.EmbeddedText = embeddedText;
                        existing.ContentHash = contentHash;
                        existing.Embedding = embedding;
                        existing.LastUpdatedAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    _logger.LogInformation("Creating embedding for new movie {MovieId}.", movie.Id);
                    var embedding = await _embeddingService.EmbedAsync(embeddedText, cancellationToken);
                    if (embedding.Length > 0)
                    {
                        _db.MovieEmbeddings.Add(new MovieEmbedding
                        {
                            MovieId = movie.Id,
                            EmbeddedText = embeddedText,
                            ContentHash = contentHash,
                            Embedding = embedding,
                            LastUpdatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync movie embeddings. Semantic search will fallback to traditional catalogue.");
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

    private static string BuildMovieEmbeddedText(Movie movie)
    {
        var sb = new StringBuilder();
        sb.Append(movie.Title);
        sb.Append(" - ");
        sb.Append(movie.Description);
        sb.Append(" - Duration: ").Append(movie.Duration).Append(" minutes");
        sb.Append(" - Language: ").Append(movie.Language);
        sb.Append(" - Rating: ").Append(movie.Rating);

        if (movie.MovieGenres?.Count > 0)
        {
            sb.Append(" - Genres: ");
            sb.Append(string.Join(", ", movie.MovieGenres.Select(g => g.Genre.Name)));
        }

        return sb.ToString();
    }

    private static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }

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
