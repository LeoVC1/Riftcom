using RiftboundStore.Models;

namespace RiftboundStore.Services;

public interface ICartService
{
    Task<IReadOnlyList<CartItem>> GetItemsAsync(string userId);
    Task<int> GetTotalCountAsync(string userId);
    Task<Dictionary<int, int>> GetQuantitiesAsync(string userId);
    Task AddAsync(string userId, int cardId, int quantity = 1);
    Task IncrementAsync(string userId, int cardId);
    Task DecrementAsync(string userId, int cardId);
    Task RemoveAsync(string userId, int cartItemId);
    Task UpdateQuantityAsync(string userId, int cartItemId, int quantity);
    Task ClearAsync(string userId);
}
