using AutoMapper;
using InfectionSolutions.Api.Mappings;
using InfectionSolutions.Application;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfectionSolutions.Tests.Common;

/// <summary>Los mismos perfiles que usan el panel y la API (Application + ViewModels de la API).</summary>
public static class TestMapper
{
    public static readonly MapperConfiguration Configuration = new(options =>
    {
        options.AddMaps(typeof(ApplicationServiceCollectionExtensions).Assembly);
        options.AddProfile<ApiMappingProfile>();
    }, NullLoggerFactory.Instance);

    public static readonly IMapper Instance = Configuration.CreateMapper();
}
