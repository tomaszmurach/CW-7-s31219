using Microsoft.AspNetCore.Mvc;
using WebApplication1.Services;
using WebApplication1.Exceptions;
using WebApplication1.Models.DTOs;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/clients")]
public class ClientsController(IDbService dbService) : ControllerBase
{
    [HttpGet("{id}/trips")] // Zwraca liste wycieczek przypisanych do klienta o podanym id
    public async Task<IActionResult> GetClientTrips([FromRoute] int id)
    {
        try
        {
            var trips = await dbService.GetTripsByClientIdAsync(id);
            return Ok(trips);
        }
        catch (NotFoundException e)
        {
            return NotFound(e.Message);
        }
    }
    
    [HttpPost] //Tworzy nowego klienta na podstawie danych podanych w body zapytania
    public async Task<IActionResult> CreateClient([FromBody] ClientCreateDTO dto)
    {
        var client = await dbService.CreateClientAsync(dto);
        return Created($"api/clients/{client.IdClient}", client);
    }
    
    
    [HttpPut("{id}/trips/{tripId}")] //Rejestruje klienta o podanym id na wycieczke o podanym id, jesli wystapi jakis blad zwroci odpowiedni komunikat
    public async Task<IActionResult> RegisterClientToTrip(int id, int tripId)
    {
        try
        {
            await dbService.RegisterClientToTripAsync(id, tripId);
            return NoContent();
        }
        catch (NotFoundException e)
        {
            return NotFound(e.Message);
        }
        catch (InvalidOperationException e)
        {
            return Conflict(e.Message);
        }
    }
    
    
    [HttpDelete("{id}/trips/{tripId}")] // Usuwa klienta z wycieczki, jezeli taki istnieje
    public async Task<IActionResult> RemoveClientFromTrip([FromRoute] int id, [FromRoute] int tripId)
    {
        try
        {
            await dbService.RemoveClientFromTripAsync(id, tripId);
            return NoContent();
        }
        catch (NotFoundException e)
        {
            return NotFound(e.Message);
        }
    }



}