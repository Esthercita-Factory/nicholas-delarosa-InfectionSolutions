using InfectionSolutions.Admin.Common;
using InfectionSolutions.Admin.ViewModels.Account;
using InfectionSolutions.Application.Dtos.Auth;
using InfectionSolutions.Application.Services.Auth;
using InfectionSolutions.Domain.Identity;
using InfectionSolutions.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InfectionSolutions.Admin.Controllers;

public class AccountController : Controller
{
    private const string MensajeCredencialesInvalidas = "Correo o contraseña incorrectos.";

    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuthService _auth;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAuthService auth,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _auth = auth;
        _logger = logger;
    }

    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.IsInRole(ApplicationRoles.Administrator))
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel modelo)
    {
        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        var user = await _userManager.FindByEmailAsync(modelo.Email.Trim());
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, MensajeCredencialesInvalidas);
            return View(modelo);
        }

        // Primero la contrasena (con bloqueo por intentos) y despues el rol: asi
        // no se revela si un correo es de cliente sin conocer su clave.
        var verificacion = await _signInManager.CheckPasswordSignInAsync(user, modelo.Password, lockoutOnFailure: true);
        if (verificacion.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "La cuenta está bloqueada temporalmente por intentos fallidos. Intenta en unos minutos.");
            return View(modelo);
        }

        if (!verificacion.Succeeded)
        {
            ModelState.AddModelError(string.Empty, MensajeCredencialesInvalidas);
            return View(modelo);
        }

        if (!await _userManager.IsInRoleAsync(user, ApplicationRoles.Administrator))
        {
            _logger.LogWarning("Usuario sin rol administrador intentó entrar al panel: {Email}", user.Email);
            ModelState.AddModelError(string.Empty,
                "Tu cuenta es de cliente. El panel administrativo es solo para administradores: ingresa desde el portal de clientes.");
            return View(modelo);
        }

        await _signInManager.SignInAsync(user, modelo.RememberMe);
        _logger.LogInformation("Administrador {Email} inició sesión.", user.Email);

        return Url.IsLocalUrl(modelo.ReturnUrl)
            ? LocalRedirect(modelo.ReturnUrl)
            : RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    /// <summary>
    /// Llega aqui un usuario autenticado sin rol Administrador (por ejemplo un
    /// Cliente con una cookie valida): se cierra su sesion y se le explica.
    /// </summary>
    [AllowAnonymous]
    public async Task<IActionResult> AccessDenied()
    {
        if (User.Identity?.IsAuthenticated == true && !User.IsInRole(ApplicationRoles.Administrator))
        {
            await _signInManager.SignOutAsync();
        }

        return View();
    }

    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    /// <summary>
    /// Envia el enlace solo si el correo es de un administrador, pero responde igual en todos
    /// los casos: la pantalla no revela que correos tienen cuenta.
    /// </summary>
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel modelo, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        var pagina = Url.Action(nameof(ResetPassword), "Account", null, Request.Scheme)!;
        await _auth.RequestPasswordResetAsync(modelo.Email, pagina, ApplicationRoles.Administrator, cancellationToken);

        TempData[TempDataKeys.Mensaje] =
            $"Si {modelo.Email.Trim()} es la cuenta de un administrador, te enviamos un enlace para cambiar la contraseña. Vence en una hora.";
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult ResetPassword(string? email, string? token)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            TempData[TempDataKeys.Error] = "El enlace para cambiar la contraseña está incompleto. Pide uno nuevo.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(new ResetPasswordViewModel { Email = email, Token = token });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel modelo, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        var resultado = await _auth.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email = modelo.Email,
            Token = modelo.Token,
            Password = modelo.Password,
            ConfirmPassword = modelo.ConfirmPassword
        }, cancellationToken);

        if (resultado.IsFailure)
        {
            ModelState.AddResultError(resultado.Error!);
            return View(modelo);
        }

        _logger.LogInformation("Contraseña restablecida para {Email}.", modelo.Email);
        TempData[TempDataKeys.Mensaje] = "Tu contraseña se cambió. Ya puedes iniciar sesión con la nueva.";
        return RedirectToAction(nameof(Login));
    }

    public IActionResult Register()
    {
        return View(new RegisterAdminViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterAdminViewModel modelo)
    {
        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        var email = modelo.Email.Trim().ToLowerInvariant();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = modelo.FullName.Trim()
        };

        var creacion = await _userManager.CreateAsync(user, modelo.Password);
        if (!creacion.Succeeded)
        {
            foreach (var error in creacion.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(modelo);
        }

        await _userManager.AddToRoleAsync(user, ApplicationRoles.Administrator);

        TempData[TempDataKeys.Mensaje] = $"El administrador {user.FullName} se creó correctamente.";
        return RedirectToAction("Index", "Home");
    }
}
