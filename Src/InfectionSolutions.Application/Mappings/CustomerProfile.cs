using AutoMapper;
using InfectionSolutions.Application.Dtos.Customers;
using InfectionSolutions.Domain.Entities;

namespace InfectionSolutions.Application.Mappings;

public sealed class CustomerProfile : Profile
{
    public CustomerProfile()
    {
        CreateMap<Customer, CustomerResponse>()
            .ForCtorParam(nameof(CustomerResponse.HasPortalAccess), opt => opt.MapFrom(c => c.UserId != null));

        CreateMap<Customer, CustomerOption>();
    }
}
