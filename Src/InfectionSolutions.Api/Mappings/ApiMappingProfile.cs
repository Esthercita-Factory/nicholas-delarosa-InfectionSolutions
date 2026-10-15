using AutoMapper;
using InfectionSolutions.Api.ViewModels.Auth;
using InfectionSolutions.Api.ViewModels.Customers;
using InfectionSolutions.Api.ViewModels.Products;
using InfectionSolutions.Api.ViewModels.Sales;
using InfectionSolutions.Application.Dtos.Auth;
using InfectionSolutions.Application.Dtos.Customers;
using InfectionSolutions.Application.Dtos.Portal;
using InfectionSolutions.Application.Dtos.Products;
using InfectionSolutions.Application.Dtos.Sales;

namespace InfectionSolutions.Api.Mappings;

/// <summary>
/// ViewModel de entrada -> DTO de Application. Los nullables de los ViewModels
/// (para que [Required] funcione) ya vienen validados cuando se mapean.
/// </summary>
public sealed class ApiMappingProfile : Profile
{
    public ApiMappingProfile()
    {
        CreateMap<ProductViewModel, ProductRequest>()
            .ForMember(d => d.Price, opt => opt.MapFrom(s => s.Price ?? 0))
            .ForMember(d => d.Stock, opt => opt.MapFrom(s => s.Stock ?? 0));

        CreateMap<CustomerViewModel, CustomerRequest>()
            .ForMember(d => d.Age, opt => opt.MapFrom(s => s.Age ?? 0));

        CreateMap<SaleLineViewModel, SaleLineRequest>()
            .ForCtorParam(nameof(SaleLineRequest.ProductId), opt => opt.MapFrom(s => s.ProductId ?? Guid.Empty))
            .ForCtorParam(nameof(SaleLineRequest.Quantity), opt => opt.MapFrom(s => s.Quantity ?? 0));

        CreateMap<SaleViewModel, SaleRequest>()
            .ForMember(d => d.CustomerId, opt => opt.MapFrom(s => s.CustomerId ?? Guid.Empty));

        CreateMap<CheckoutViewModel, CheckoutRequest>();

        CreateMap<LoginViewModel, LoginRequest>();

        CreateMap<RegisterViewModel, RegisterRequest>()
            .ForMember(d => d.Age, opt => opt.MapFrom(s => s.Age ?? 0));

        CreateMap<ResetPasswordViewModel, ResetPasswordRequest>();

        CreateMap<CompleteProfileViewModel, CompleteProfileRequest>()
            .ForMember(d => d.Age, opt => opt.MapFrom(s => s.Age ?? 0));
    }
}
