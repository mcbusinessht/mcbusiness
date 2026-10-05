using Microsoft.EntityFrameworkCore;

namespace MCBusiness.Services
{
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        public PagedResult(List<T> items, int totalItems, int currentPage, int pageSize)
        {
            Items = items;
            TotalItems = totalItems;
            CurrentPage = currentPage;
            PageSize = pageSize;
            TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        }
    }

    public interface IPaginationService
    {
        Task<PagedResult<T>> GetPagedAsync<T>(IQueryable<T> query, int pageNumber = 1, int pageSize = 10);
        PagedResult<T> GetPaged<T>(IEnumerable<T> source, int pageNumber = 1, int pageSize = 10);
    }

    public class PaginationService : IPaginationService
    {
        public async Task<PagedResult<T>> GetPagedAsync<T>(IQueryable<T> query, int pageNumber = 1, int pageSize = 10)
        {
            // Valider les paramètres
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 100); // Limiter � max 100 items

            var totalItems = await query.CountAsync();
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<T>(items, totalItems, pageNumber, pageSize);
        }

        public PagedResult<T> GetPaged<T>(IEnumerable<T> source, int pageNumber = 1, int pageSize = 10)
        {
            // Valider les paramètres
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var totalItems = source.Count();
            var items = source
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<T>(items, totalItems, pageNumber, pageSize);
        }
    }

    // Extension methods pour faciliter l'utilisation
    public static class QueryableExtensions
    {
        public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
            this IQueryable<T> query, 
            int pageNumber = 1, 
            int pageSize = 10)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var totalItems = await query.CountAsync();
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<T>(items, totalItems, pageNumber, pageSize);
        }

        public static PagedResult<T> ToPagedResult<T>(
            this IEnumerable<T> source, 
            int pageNumber = 1, 
            int pageSize = 10)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var totalItems = source.Count();
            var items = source
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<T>(items, totalItems, pageNumber, pageSize);
        }
    }
}
