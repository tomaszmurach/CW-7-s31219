using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models.DTOs;

public class ClientCreateDTO
{
    [Length(1, 50)]
    public required string FirstName { get; set; }

    [Length(1, 50)]
    public required string LastName { get; set; }

    [EmailAddress]
    public required string Email { get; set; }

    [Length(11, 11)]
    public required string Pesel { get; set; }

    [Phone]
    public required string Telephone { get; set; }
}