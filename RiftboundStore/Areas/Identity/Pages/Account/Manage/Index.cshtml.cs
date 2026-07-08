using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RiftboundStore.Models;

namespace RiftboundStore.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public IndexModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    public string? Username { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [StringLength(60, ErrorMessage = "Máximo de 60 caracteres.")]
        [Display(Name = "Nome de exibição")]
        public string? DisplayName { get; set; }

        [Phone(ErrorMessage = "Telefone inválido.")]
        [Display(Name = "Telefone")]
        public string? PhoneNumber { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        Username = user.UserName;
        Input = new InputModel
        {
            DisplayName = user.DisplayName,
            PhoneNumber = user.PhoneNumber
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        if (!ModelState.IsValid)
        {
            Username = user.UserName;
            return Page();
        }

        user.DisplayName = Input.DisplayName;
        if (Input.PhoneNumber != user.PhoneNumber)
        {
            var res = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
            if (!res.Succeeded)
            {
                StatusMessage = "Erro ao atualizar telefone.";
                return RedirectToPage();
            }
        }
        await _userManager.UpdateAsync(user);
        await _signInManager.RefreshSignInAsync(user);
        StatusMessage = "Perfil atualizado.";
        return RedirectToPage();
    }
}
