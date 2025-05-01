using Microsoft.AspNetCore.Mvc;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/trips")] 
public class TripsController(IDbService dbService) : ControllerBase
{
    [HttpGet] //Zwraca liste wszystkich wycieczek wraz z podstawowymi informacjami o nich
    public async Task<IActionResult> GetTrips()
    {
        var trips = await dbService.GetTripsAsync();
        return Ok(trips);
    }
}