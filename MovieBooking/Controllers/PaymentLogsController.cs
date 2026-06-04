using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/payment-logs")]
public class PaymentLogsController : CrudController<PaymentLog, PaymentLogDto>
{
    public PaymentLogsController(ICrudService<PaymentLog, PaymentLogDto> crudService) : base(crudService) { }
}
