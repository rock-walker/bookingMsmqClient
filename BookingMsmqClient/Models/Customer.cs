using System;

namespace BookingMsmqClient.Models
{
    class Customer
    {
        public Guid OrderId { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
    }
}
