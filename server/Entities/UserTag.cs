namespace API.Entities;

public class UserTag
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
}
