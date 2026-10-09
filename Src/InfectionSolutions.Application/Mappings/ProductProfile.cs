using AutoMapper;
using InfectionSolutions.Application.Dtos.Products;
using InfectionSolutions.Domain.Entities;

namespace InfectionSolutions.Application.Mappings;

/// <summary>Product -> DTOs. Los nombres coinciden, AutoMapper los resuelve solo.</summary>
public sealed class ProductProfile : Profile
{
    public ProductProfile()
    {
        CreateMap<Product, ProductResponse>();
        CreateMap<Product, ProductOption>();
    }
}
