using Mechanics.Application.Customers.Requests;

namespace Mechanics.Application.Customers.Events;

public class CustomerCreatedEvent
{
    public string FullName { get; }
    public string CpfNumber { get; }
    public string Email { get; }
    public Guid CustomerId { get; }
    public bool IsAdmin { get; }

    public CustomerCreatedEvent(Guid customerId, CreateIndividualCustomerRequest request)
    {
        FullName = request.FullName;
        CpfNumber = request.CpfNumber;
        Email = request.Email;
        CustomerId = customerId;
        IsAdmin = false;
    }

    public CustomerCreatedEvent(Guid customerId, CreateBusinessCustomerRequest request, bool isAdmin)
    {
        FullName = request.ResponsibleFullName;
        CpfNumber = request.ResponsibleCpfNumber;
        Email = request.ResponsibleEmail;
        CustomerId = customerId;
        IsAdmin = isAdmin;
    }
}
