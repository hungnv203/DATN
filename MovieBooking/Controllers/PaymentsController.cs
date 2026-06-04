using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/payments")]
public class PaymentsController : CrudController<Payment, PaymentDto>
{
    public PaymentsController(ICrudService<Payment, PaymentDto> crudService) : base(crudService) { }
}
