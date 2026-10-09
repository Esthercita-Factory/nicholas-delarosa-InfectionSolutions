using AutoMapper;
using InfectionSolutions.Application.Dtos.Sales;
using InfectionSolutions.Domain.Entities;

namespace InfectionSolutions.Application.Mappings;

/// <summary>
/// Sale -> DTOs. Requiere el cliente y los detalles (con su producto) cargados;
/// los repositorios los incluyen en FindByIdAsync y en los listados.
/// </summary>
public sealed class SaleProfile : Profile
{
    public SaleProfile()
    {
        CreateMap<Sale, SaleListItemResponse>()
            .ForCtorParam(nameof(SaleListItemResponse.CustomerName), opt => opt.MapFrom(s => s.Customer.FullName))
            .ForCtorParam(nameof(SaleListItemResponse.CustomerDocument), opt => opt.MapFrom(s => s.Customer.Document))
            .ForCtorParam(nameof(SaleListItemResponse.ItemCount), opt => opt.MapFrom(s => s.Details.Count));

        CreateMap<Sale, SaleResponse>()
            .ForCtorParam(nameof(SaleResponse.CustomerName), opt => opt.MapFrom(s => s.Customer.FullName))
            .ForCtorParam(nameof(SaleResponse.CustomerDocument), opt => opt.MapFrom(s => s.Customer.Document))
            .ForCtorParam(nameof(SaleResponse.CustomerEmail), opt => opt.MapFrom(s => s.Customer.Email))
            .ForCtorParam(nameof(SaleResponse.CustomerPhone), opt => opt.MapFrom(s => s.Customer.Phone))
            .ForCtorParam(nameof(SaleResponse.CustomerAddress), opt => opt.MapFrom(s => s.Customer.Address))
            .ForCtorParam(nameof(SaleResponse.Details), opt => opt.MapFrom(s => s.Details));

        CreateMap<SaleDetail, SaleDetailResponse>()
            .ForCtorParam(nameof(SaleDetailResponse.Sku), opt => opt.MapFrom(d => d.Product.Sku))
            .ForCtorParam(nameof(SaleDetailResponse.ProductName), opt => opt.MapFrom(d => d.Product.Name))
            .ForCtorParam(nameof(SaleDetailResponse.Unit), opt => opt.MapFrom(d => d.Product.Unit));
    }
}
