namespace BookingManagementService.Models
{
    public class Resource
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Type { get; set; }

        public ICollection<Booking> Bookings { get; set; }= new List<Booking>();
    }
}