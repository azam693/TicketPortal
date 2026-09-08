using Catalog.Entities;

namespace Catalog.Dtos;

public record VenueDto(
    Guid Id,
    string Name,
    string City,
    string Address,
    IReadOnlyList<SeatMapSectionDto> Sections)
{
    public static VenueDto Create(Venue venue) =>
        new(
            venue.Id,
            venue.Name,
            venue.City,
            venue.Address,
            venue.Sections.Select(SeatMapSectionDto.Create).ToList());
}

public record SeatMapSectionDto(
    Guid Id,
    string Name,
    int RowsCount,
    int SeatsPerRow,
    string Category)
{
    public static SeatMapSectionDto Create(SeatMapSection section) =>
        new(
            section.Id,
            section.Name,
            section.RowsCount,
            section.SeatsPerRow,
            section.Category.ToString());
}
