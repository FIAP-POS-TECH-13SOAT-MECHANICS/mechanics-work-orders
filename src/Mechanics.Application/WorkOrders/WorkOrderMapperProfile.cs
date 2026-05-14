using AutoMapper;
using Mechanics.Application.WorkOrders.Responses;
using Mechanics.Domain.WorkOrders;

namespace Mechanics.Application.WorkOrders;

public class WorkOrderMapperProfile : Profile
{
    public WorkOrderMapperProfile()
    {
        CreateMap<WorkOrder, GetWorkOrderResponse>()
            .ForMember(
                destination => destination.IsPaymentApproved,
                option => option.MapFrom(source => source.PaidAt.HasValue))
            .ForMember(
                destination => destination.IsReadyForDelivery,
                option => option.MapFrom(source =>
                    source.Status == WorkOrderStatus.ReadyForDelivery));
    }
}
