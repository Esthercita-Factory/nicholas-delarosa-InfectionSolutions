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

        var user = intento.User!;
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

        var error = CustomerRules.Validate(datos);
        if (error is not null)
        {
            return Result.Failure<AuthResponse>(error);
        }

        var documento = FieldRules.NormalizeDocument(request.Document);
        var correo = FieldRules.NormalizeEmail(request.Email);

        var existente = await _customers.FindByDocumentAsync(documento, cancellationToken);
        error = ValidarClienteExistente(existente, correo)
            ?? (existente is null && await _customers.EmailExistsAsync(correo, null, cancellationToken)
                ? Error.Conflict("Ya hay un cliente registrado con ese correo.", nameof(request.Email))
                : null);
        if (error is not null)
        {
            return Result.Failure<AuthResponse>(error);
        }

        // Cuenta y ficha de cliente en la misma transaccion: si la ficha falla
        // no queda una cuenta huerfana con rol Cliente.
        await using var transaccion = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var creacion = await _users.CreateAsync(
            new UserRegistration(correo, request.FullName.Trim(), request.Password),
            ApplicationRoles.Customer,
            cancellationToken);

        if (!creacion.Succeeded)
        {
            await transaccion.RollbackAsync(cancellationToken);
            var primero = creacion.Errors.FirstOrDefault() ?? new UserError(nameof(request.Password), "No se pudo crear la cuenta.");
            var mensaje = string.Join(" ", creacion.Errors.Where(e => e.Field == primero.Field).Select(e => e.Message));
            return Result.Failure<AuthResponse>(Error.Validation(mensaje, primero.Field));
        }

        var user = creacion.User!;
        var ahora = _clock.UtcNow;
        Customer customer;

        if (existente is not null)
        {
            // El administrador ya lo tenia como cliente: se vincula y conserva su historial.
            customer = existente;
            customer.UserId = user.Id;
            customer.UpdatedAt = ahora;
        }
        else
        {
            customer = new Customer
            {
                Document = documento,
                FullName = request.FullName.Trim(),
                Age = request.Age,
                Email = correo,
                Phone = request.Phone.Trim(),
                Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
                IsActive = true,
                UserId = user.Id,
                CreatedAt = ahora
            };
            await _customers.AddAsync(customer, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
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

    private AuthResponse EmitirToken(UserAccount user, IReadOnlyList<string> roles, Customer? customer)
    {
        var token = _tokens.CreateToken(user, roles);
        return new AuthResponse(TokenType, token.Token, token.ExpiresAt, Perfil(user, roles, customer));
    }

    private UserProfile Perfil(UserAccount user, IReadOnlyList<string> roles, Customer? customer)
        => new(user.Id, user.Email, user.FullName, roles, customer is null ? null : _mapper.Map<CustomerResponse>(customer));
}
