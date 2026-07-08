using Microsoft.EntityFrameworkCore;
using RiftboundStore.Data;
using RiftboundStore.Models;

namespace RiftboundStore.Services;

public class CartService : ICartService
{
    private readonly ApplicationDbContext _db;

    public CartService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CartItem>> GetItemsAsync(string userId)
    {
        return await _db.CartItems
            .Include(c => c.Card)
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.AddedAt)
            .ToListAsync();
    }

    public async Task<int> GetTotalCountAsync(string userId)
    {
        return await _db.CartItems
            .Where(c => c.UserId == userId)
            .SumAsync(c => (int?)c.Quantity) ?? 0;
    }

    public async Task<Dictionary<int, int>> GetQuantitiesAsync(string userId)
    {
        return await _db.CartItems
            .Where(c => c.UserId == userId)
            .Select(c => new { c.CardId, c.Quantity })
            .ToDictionaryAsync(x => x.CardId, x => x.Quantity);
    }

    public async Task IncrementAsync(string userId, int cardId)
    {
        await AddAsync(userId, cardId, 1);
    }

    public async Task DecrementAsync(string userId, int cardId)
    {
        var item = await _db.CartItems.FirstOrDefaultAsync(c => c.UserId == userId && c.CardId == cardId);
        if (item is null) return;
        item.Quantity -= 1;
        if (item.Quantity <= 0)
        {
            _db.CartItems.Remove(item);
        }
        await _db.SaveChangesAsync();
    }

    public async Task AddAsync(string userId, int cardId, int quantity = 1)
    {
        if (quantity < 1) quantity = 1;
        var card = await _db.Cards.FirstOrDefaultAsync(c => c.Id == cardId);
        if (card is null) throw new InvalidOperationException("Carta não encontrada.");

        var existing = await _db.CartItems.FirstOrDefaultAsync(c => c.UserId == userId && c.CardId == cardId);
        if (existing is null)
        {
            var toAdd = Math.Min(quantity, Math.Max(card.Stock, 0));
            if (toAdd <= 0) return;
            _db.CartItems.Add(new CartItem
            {
                UserId = userId,
                CardId = cardId,
                Quantity = toAdd
            });
        }
        else
        {
            var newQty = existing.Quantity + quantity;
            existing.Quantity = Math.Min(newQty, Math.Max(card.Stock, 0));
        }

        await _db.SaveChangesAsync();
    }

    public async Task RemoveAsync(string userId, int cartItemId)
    {
        var item = await _db.CartItems.FirstOrDefaultAsync(c => c.Id == cartItemId && c.UserId == userId);
        if (item is null) return;
        _db.CartItems.Remove(item);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateQuantityAsync(string userId, int cartItemId, int quantity)
    {
        if (quantity < 1) quantity = 1;
        var item = await _db.CartItems.Include(c => c.Card)
            .FirstOrDefaultAsync(c => c.Id == cartItemId && c.UserId == userId);
        if (item is null) return;

        var maxAllowed = Math.Max(item.Card?.Stock ?? 0, 0);
        item.Quantity = Math.Min(quantity, maxAllowed);
        if (item.Quantity <= 0)
        {
            _db.CartItems.Remove(item);
        }
        await _db.SaveChangesAsync();
    }

    public async Task ClearAsync(string userId)
    {
        var items = _db.CartItems.Where(c => c.UserId == userId);
        _db.CartItems.RemoveRange(items);
        await _db.SaveChangesAsync();
    }
}
