namespace Site.Services
{
    public interface IBookLookupService
    {
        Task<BookLookupResult?> LookupByIsbnAsync(string isbn);
        Task<IReadOnlyList<BookLookupResult?>> LookupByIsbnsAsync(
            IReadOnlyList<string> isbns);
    }

    public interface IBookCandidateLookupService
    {
        Task<BookLookupCandidates> LookupCandidatesByIsbnAsync(string isbn);
        Task<IReadOnlyList<BookLookupCandidates>> LookupCandidatesByIsbnsAsync(
            IReadOnlyList<string> isbns);
    }

    public class BookLookupCandidates
    {
        public IReadOnlyList<BookLookupResult> Results { get; init; } = [];
        public bool NdlLookupFailed { get; init; }
    }

    public class BookLookupResult
    {
        public string? Title { get; set; }
        public string? Creator { get; set; }
        public string? Publisher { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? ISBN { get; set; }
        public int? PageCount { get; set; }
    }
}
