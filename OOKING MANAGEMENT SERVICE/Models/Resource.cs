namespace BookingManagementService.Models
{
    public class Resource
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; }

        public string Type { get; set; }

        public ICollection<Booking> Bookings { get; set; }= new List<Booking>();
    }
}