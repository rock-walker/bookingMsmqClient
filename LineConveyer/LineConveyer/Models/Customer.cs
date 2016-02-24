using System;

namespace BookingMsmqClient.Models
{
    public class Customer
    {
        public Guid OrderId { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
    }
}
