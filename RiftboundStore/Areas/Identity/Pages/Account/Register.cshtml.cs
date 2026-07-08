using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RiftboundStore.Data;
using RiftboundStore.Models;

namespace RiftboundStore.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class RegisterModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<RegisterModel> _logger;

    public RegisterModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<RegisterModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }
    public IList<AuthenticationScheme> ExternalLogins { get; set; } = new List<AuthenticationScheme>();

    public class InputModel
    {
        [Required(ErrorMessage = "Informe seu nome.")]
        [StringLength(60, ErrorMessage = "Máximo de 60 caracteres.")]
        [Display(Name = "Nome")]
        public string DisplayName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe seu e-mail.")]
        [EmailAddress(ErrorMessage = "E-mail inválido.")]
        [Display(Name = "E-mail")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe uma senha.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "A senha precisa ter pelo menos {2} caracteres.")]
        [DataType(DataType.Password)]
        [Display(Name = "Senha")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirmar senha")]
        [Compare(nameof(Password), ErrorMessage = "As senhas não conferem.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        if (!ModelState.IsValid) return Page();

        var user = new ApplicationUser
        {
            UserName = Input.Email,
            Email = Input.Email,
            DisplayName = Input.DisplayName,
            EmailConfirmed = true // no SMTP configured; can be toggled later
        };
        var result = await _userManager.CreateAsync(user, Input.Password);
        if (result.Succeeded)
        {
            _logger.LogInformation("Usuário {Email} criado.", Input.Email);
            await _userManager.AddToRoleAsync(user, SeedData.CustomerRole);
            await _signInManager.SignInAsync(user, isPersistent: false);
            return LocalRedirect(returnUrl);
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, TranslateError(error));
        }
        return Page();
    }

    private static string TranslateError(IdentityError error) => error.Code switch
    {
        "DuplicateUserName" => "Este e-mail já está em uso.",
        "DuplicateEmail" => "Este e-mail já está em uso.",
        "InvalidEmail" => "E-mail inválido.",
        "PasswordTooShort" => "A senha é muito curta.",
        "PasswordRequiresDigit" => "A senha precisa conter pelo menos um dígito.",
        "PasswordRequiresLower" => "A senha precisa conter pelo menos uma letra minúscula.",
        "PasswordRequiresUpper" => "A senha precisa conter pelo menos uma letra maiúscula.",
        "PasswordRequiresNonAlphanumeric" => "A senha precisa conter um caractere especial.",
        "PasswordRequiresUniqueChars" => "A senha precisa ter caracteres variados.",
        _ => error.Description
    };
}
