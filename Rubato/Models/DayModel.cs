namespace Rubato.Models;

public class DayModel
{
    public DateOnly Date { get; set; }

    public List<EntryModel> Entries { get; set; } = [];
}