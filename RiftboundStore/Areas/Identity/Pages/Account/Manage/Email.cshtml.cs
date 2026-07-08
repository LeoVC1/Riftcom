using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RiftboundStore.Models;

namespace RiftboundStore.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class EmailModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public EmailModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    public string? Email { get; set; }
    public bool IsEmailConfirmed { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Informe o novo e-mail.")]
        [EmailAddress(ErrorMessage = "E-mail inválido.")]
        [Display(Name = "Novo e-mail")]
        public string NewEmail { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();
        Email = await _userManager.GetEmailAsync(user);
        IsEmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);
        Input = new InputModel { NewEmail = Email ?? string.Empty };
        return Page();
    }

    public async Task<IActionResult> OnPostChangeEmailAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();
        if (!ModelState.IsValid)
        {
            Email = await _userManager.GetEmailAsync(user);
            IsEmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);
            return Page();
        }
        var currentEmail = await _userManager.GetEmailAsync(user);
        if (Input.NewEmail == currentEmail)
        {
            StatusMessage = "O e-mail não foi alterado.";
            return RedirectToPage();
        }
        var setResult = await _userManager.SetEmailAsync(user, Input.NewEmail);
        var setUser = await _userManager.SetUserNameAsync(user, Input.NewEmail);
        if (!setResult.Succeeded || !setUser.Succeeded)
        {
            foreach (var e in setResult.Errors.Concat(setUser.Errors))
                ModelState.AddModelError(string.Empty, e.Description);
            Email = currentEmail;
            IsEmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);
            return Page();
        }
        await _signInManager.RefreshSignInAsync(user);
        StatusMessage = "E-mail atualizado.";
        return RedirectToPage();
    }
}
