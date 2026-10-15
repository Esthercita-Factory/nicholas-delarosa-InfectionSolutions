using AutoMapper;
using InfectionSolutions.Application.Abstractions;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Auth;
using InfectionSolutions.Application.Dtos.Customers;
using InfectionSolutions.Application.Services.Customers;
using InfectionSolutions.Application.Services.Notifications;
using InfectionSolutions.Domain.Entities;
using InfectionSolutions.Domain.Identity;

namespace InfectionSolutions.Application.Services.Auth;

public sealed class AuthService : IAuthService
{
    public const string TokenType = "Bearer";

    private const string MensajeCredencialesInvalidas = "Correo o contraseña incorrectos.";
    private const string MensajeEnlaceInvalido = "El enlace para cambiar la contraseña no es válido o ya venció. Pide uno nuevo.";

    private readonly IUserService _users;
    private readonly ITokenService _tokens;
    private readonly ICustomerRepository _customers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICustomerNotifier _notifier;
    private readonly IMapper _mapper;

    public AuthService(
        IUserService users,
        ITokenService tokens,
        ICustomerRepository customers,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICustomerNotifier notifier,
        IMapper mapper)
    {
        _users = users;
        _tokens = tokens;
        _customers = customers;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _notifier = notifier;
        _mapper = mapper;
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return Result.Failure<AuthResponse>(Error.Validation("Escribe el correo y la contraseña."));
        }

        var intento = await _users.CheckPasswordAsync(FieldRules.NormalizeEmail(request.Email), request.Password, cancellationToken);
        switch (intento.Outcome)
        {
            case SignInOutcome.LockedOut:
                return Result.Failure<AuthResponse>(Error.Unauthorized(
                    "La cuenta está bloqueada temporalmente por intentos fallidos. Intenta en unos minutos."));
            case SignInOutcome.InvalidCredentials:
                return Result.Failure<AuthResponse>(Error.Unauthorized(MensajeCredencialesInvalidas));
        }

        return await SesionAsync(intento.User!, cancellationToken);
    }

    public async Task<Result<AuthResponse>> RegisterCustomerAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return Result.Failure<AuthResponse>(Error.Validation("Las contraseñas no coinciden.", nameof(request.ConfirmPassword)));
        }

        var datos = new CustomerRequest
        {
            Document = request.Document,
            FullName = request.FullName,
            Age = request.Age,
            Email = request.Email,
            Phone = request.Phone,
            Address = request.Address,
            IsActive = true
        };

        var preparacion = await PrepararFichaAsync(datos, cancellationToken);
        if (preparacion.IsFailure)
        {
            return Result.Failure<AuthResponse>(preparacion.Error!);
        }

        // Cuenta y ficha de cliente en la misma transaccion: si la ficha falla
        // no queda una cuenta huerfana con rol Cliente.
        await using var transaccion = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var creacion = await _users.CreateAsync(
            new UserRegistration(FieldRules.NormalizeEmail(request.Email), request.FullName.Trim(), request.Password),
            ApplicationRoles.Customer,
            cancellationToken);

        if (!creacion.Succeeded)
        {
            await transaccion.RollbackAsync(cancellationToken);
            return Result.Failure<AuthResponse>(ErrorDeIdentity(creacion.Errors, nameof(request.Password), "No se pudo crear la cuenta."));
        }

        var user = creacion.User!;
        var customer = await GuardarFichaAsync(datos, preparacion.Value, user.Id, cancellationToken);
        await transaccion.CommitAsync(cancellationToken);

        await _notifier.SendWelcomeAsync(customer.Email, customer.FullName, cancellationToken);

        return Result.Success(EmitirToken(user, [ApplicationRoles.Customer], customer));
    }

    public async Task<Result<UserProfile>> GetProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<UserProfile>(Error.Unauthorized("La sesión ya no es válida. Inicia sesión de nuevo."));
        }

        var roles = await _users.GetRolesAsync(user.Id, cancellationToken);
        var customer = await _customers.FindByUserIdAsync(user.Id, cancellationToken);

        return Result.Success(Perfil(user, roles, customer));
    }

    public async Task RequestPasswordResetAsync(string email, string resetPageUrl, string requiredRole, CancellationToken cancellationToken = default)
    {
        // Sin respuesta distinta en ningun caso: quien pregunta no sabe si el correo existe.
        if (!FieldRules.IsValidEmail(email))
        {
            return;
        }

        var user = await _users.FindByEmailAsync(FieldRules.NormalizeEmail(email), cancellationToken);
        if (user is null || !(await _users.GetRolesAsync(user.Id, cancellationToken)).Contains(requiredRole))
        {
            return;
        }

        var token = await _users.CreatePasswordResetTokenAsync(user.Id, cancellationToken);
        if (token is null)
        {
            return;
        }

        var separador = resetPageUrl.Contains('?') ? '&' : '?';
        var enlace = $"{resetPageUrl}{separador}email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(token)}";

        await _notifier.SendPasswordResetAsync(user.Email, user.FullName, enlace, cancellationToken);
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return Result.Failure(Error.Validation("Las contraseñas no coinciden.", nameof(request.ConfirmPassword)));
        }

        if (string.IsNullOrWhiteSpace(request.Token) || !FieldRules.IsValidEmail(request.Email))
        {
            return Result.Failure(Error.Validation(MensajeEnlaceInvalido));
        }

        var errores = await _users.ResetPasswordAsync(FieldRules.NormalizeEmail(request.Email), request.Token.Trim(), request.Password, cancellationToken);
        if (errores.Count == 0)
        {
            return Result.Success();
        }

        // El token vencido o usado no es culpa de ningun campo: va al resumen del formulario.
        return errores.Any(e => e.Field == "Token")
            ? Result.Failure(Error.Validation(MensajeEnlaceInvalido))
            : Result.Failure(ErrorDeIdentity(errores, nameof(request.Password), "No se pudo cambiar la contraseña."));
    }

    public async Task<Result<AuthResponse>> ExternalLoginAsync(ExternalIdentity identity, CancellationToken cancellationToken = default)
    {
        if (!FieldRules.IsValidEmail(identity.Email))
        {
            return Result.Failure<AuthResponse>(Error.Validation(
                $"Tu cuenta de {identity.Provider} no tiene un correo verificado y público. Agrégalo en {identity.Provider} o regístrate con correo y contraseña."));
        }

        var correo = FieldRules.NormalizeEmail(identity.Email);
        var nombre = string.IsNullOrWhiteSpace(identity.FullName) ? correo.Split('@')[0] : identity.FullName.Trim();
        var cuenta = identity with { Email = correo, FullName = nombre };

        var user = await _users.FindByExternalLoginAsync(cuenta.Provider, cuenta.ProviderKey, cancellationToken);
        if (user is null)
        {
            // El mismo correo ya registrado (con contraseña o con otro proveedor) es la misma persona.
            user = await _users.FindByEmailAsync(correo, cancellationToken);
            if (user is not null)
            {
                await _users.AddExternalLoginAsync(user.Id, cuenta, cancellationToken);
            }
            else
            {
                var creacion = await _users.CreateExternalAsync(cuenta, ApplicationRoles.Customer, cancellationToken);
                if (!creacion.Succeeded)
                {
                    return Result.Failure<AuthResponse>(ErrorDeIdentity(creacion.Errors, "Email", $"No se pudo crear la cuenta con {cuenta.Provider}."));
                }

                user = creacion.User!;
            }
        }

        return await SesionAsync(user, cancellationToken);
    }

    public async Task<Result<AuthResponse>> CompleteProfileAsync(string userId, CompleteProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _users.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<AuthResponse>(Error.Unauthorized("La sesión ya no es válida. Inicia sesión de nuevo."));
        }

        var roles = await _users.GetRolesAsync(user.Id, cancellationToken);
        if (!roles.Contains(ApplicationRoles.Customer))
        {
            return Result.Failure<AuthResponse>(Error.Forbidden("Solo las cuentas de cliente tienen ficha de cliente."));
        }

        if (await _customers.FindByUserIdAsync(user.Id, cancellationToken) is not null)
        {
            return Result.Failure<AuthResponse>(Error.Conflict("Tus datos ya están completos."));
        }

        // El correo no se pide: es el de la cuenta (el que verifico Google o GitHub).
        var datos = new CustomerRequest
        {
            Document = request.Document,
            FullName = string.IsNullOrWhiteSpace(request.FullName) ? user.FullName : request.FullName,
            Age = request.Age,
            Email = user.Email,
            Phone = request.Phone,
            Address = request.Address,
            IsActive = true
        };

        var preparacion = await PrepararFichaAsync(datos, cancellationToken);
        if (preparacion.IsFailure)
        {
            return Result.Failure<AuthResponse>(preparacion.Error!);
        }

        var customer = await GuardarFichaAsync(datos, preparacion.Value, user.Id, cancellationToken);
        await _notifier.SendWelcomeAsync(customer.Email, customer.FullName, cancellationToken);

        return Result.Success(EmitirToken(user, roles, customer));
    }

    /// <summary>
    /// Valida los datos de la ficha y busca si la empresa ya tenia al cliente con ese
    /// documento (sin cuenta y con el mismo correo): en ese caso se vincula en vez de duplicarlo.
    /// </summary>
    private async Task<Result<Customer?>> PrepararFichaAsync(CustomerRequest datos, CancellationToken cancellationToken)
    {
        var error = CustomerRules.Validate(datos);
        if (error is not null)
        {
            return Result.Failure<Customer?>(error);
        }

        var correo = FieldRules.NormalizeEmail(datos.Email);
        var existente = await _customers.FindByDocumentAsync(FieldRules.NormalizeDocument(datos.Document), cancellationToken);

        error = ValidarClienteExistente(existente, correo)
            ?? (existente is null && await _customers.EmailExistsAsync(correo, null, cancellationToken)
                ? Error.Conflict("Ya hay un cliente registrado con ese correo.", nameof(datos.Email))
                : null);

        return error is null ? Result.Success(existente) : Result.Failure<Customer?>(error);
    }

    private async Task<Customer> GuardarFichaAsync(CustomerRequest datos, Customer? existente, string userId, CancellationToken cancellationToken)
    {
        var ahora = _clock.UtcNow;
        Customer customer;

        if (existente is not null)
        {
            // El administrador ya lo tenia como cliente: se vincula y conserva su historial.
            customer = existente;
            customer.UserId = userId;
            customer.UpdatedAt = ahora;
        }
        else
        {
            customer = new Customer
            {
                Document = FieldRules.NormalizeDocument(datos.Document),
                FullName = datos.FullName.Trim(),
                Age = datos.Age,
                Email = FieldRules.NormalizeEmail(datos.Email),
                Phone = datos.Phone.Trim(),
                Address = string.IsNullOrWhiteSpace(datos.Address) ? null : datos.Address.Trim(),
                IsActive = true,
                UserId = userId,
                CreatedAt = ahora
            };
            await _customers.AddAsync(customer, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return customer;
    }

    /// <summary>Roles y ficha de una cuenta ya verificada; un cliente desactivado no recibe token.</summary>
    private async Task<Result<AuthResponse>> SesionAsync(UserAccount user, CancellationToken cancellationToken)
    {
        var roles = await _users.GetRolesAsync(user.Id, cancellationToken);
        var customer = await _customers.FindByUserIdAsync(user.Id, cancellationToken);

        // Un cliente desactivado desde el panel no puede seguir comprando.
        if (roles.Contains(ApplicationRoles.Customer) && !roles.Contains(ApplicationRoles.Administrator)
            && customer is { IsActive: false })
        {
            return Result.Failure<AuthResponse>(Error.Forbidden(
                "Tu cuenta de cliente está inactiva. Comunícate con la empresa para reactivarla."));
        }

        return Result.Success(EmitirToken(user, roles, customer));
    }

    private static Error? ValidarClienteExistente(Customer? existente, string correo)
    {
        if (existente is null)
        {
            return null;
        }

        if (existente.UserId is not null)
        {
            return Error.Conflict("Ya existe una cuenta con ese documento. Inicia sesión.", nameof(RegisterRequest.Document));
        }

        if (!string.Equals(existente.Email, correo, StringComparison.OrdinalIgnoreCase))
        {
            return Error.Conflict(
                "Ese documento ya está registrado con otro correo. Comunícate con la empresa para actualizarlo.",
                nameof(RegisterRequest.Document));
        }

        return existente.IsActive
            ? null
            : Error.Forbidden("Ese cliente está inactivo. Comunícate con la empresa para reactivarlo.");
    }

    /// <summary>Los errores de Identity del primer campo afectado, unidos en un solo mensaje.</summary>
    private static Error ErrorDeIdentity(IReadOnlyList<UserError> errores, string campoPorDefecto, string mensajePorDefecto)
    {
        var primero = errores.FirstOrDefault() ?? new UserError(campoPorDefecto, mensajePorDefecto);
        var mensaje = string.Join(" ", errores.Where(e => e.Field == primero.Field).Select(e => e.Message));
        return Error.Validation(mensaje, primero.Field);
    }

    private AuthResponse EmitirToken(UserAccount user, IReadOnlyList<string> roles, Customer? customer)
    {
        var token = _tokens.CreateToken(user, roles);
        return new AuthResponse(TokenType, token.Token, token.ExpiresAt, Perfil(user, roles, customer));
    }

    private UserProfile Perfil(UserAccount user, IReadOnlyList<string> roles, Customer? customer)
        => new(user.Id, user.Email, user.FullName, roles, customer is null ? null : _mapper.Map<CustomerResponse>(customer));
}
