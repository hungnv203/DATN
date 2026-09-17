using MovieBooking.Application.Common.DTOs;

namespace MovieBooking.Application.Common.Interfaces;

public interface IAssistantService
{
    Task<AssistantResponseDto> SendAsync(SendAssistantMessageRequestDto request, CancellationToken cancellationToken = default);
}

public interface IAssistantMovieCatalogue
{
    Task<IReadOnlyList<AssistantMovieCandidateDto>> GetCandidatesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssistantMovieCandidateDto>> SearchBySemanticQueryAsync(string query, int topK = 5, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssistantKnowledgeDocumentDto>> SearchKnowledgeDocumentsAsync(string query, int topK = 3, CancellationToken cancellationToken = default);
}

public interface IAiAssistantClient
{
    Task<AiAssistantResult> CompleteAsync(AiAssistantRequest request, CancellationToken cancellationToken = default);
}
