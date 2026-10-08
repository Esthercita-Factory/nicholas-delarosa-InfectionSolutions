using InfectionSolutions.Application.Dtos.Customers;

namespace InfectionSolutions.Admin.ViewModels.Customers;

public static class CustomerMappings
{
    /// <summary>La edad ya convertida por InputParser; el formulario la trae como texto.</summary>
    public static CustomerRequest ToRequest(this CustomerFormViewModel modelo, int edad) => new()
    {
        Document = modelo.Document,
        FullName = modelo.FullName,
        Age = edad,
        Email = modelo.Email,
        Phone = modelo.Phone,
        Address = modelo.Address,
        IsActive = modelo.IsActive
    };

    public static CustomerFormViewModel ToFormViewModel(this CustomerResponse customer) => new()
    {
        Id = customer.Id,
        Document = customer.Document,
        FullName = customer.FullName,
        Age = customer.Age.ToString(),
        Email = customer.Email,
        Phone = customer.Phone,
        Address = customer.Address,
        IsActive = customer.IsActive
    };
}
