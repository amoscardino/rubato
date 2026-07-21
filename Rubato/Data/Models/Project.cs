using System.ComponentModel.DataAnnotations;

namespace Rubato.Data.Models;

public class Project
{
    [Key]
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;

    public ICollection<Entry> Entries { get; } = [];
}